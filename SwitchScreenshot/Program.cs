using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

// Controller
namespace SwitchScreenshot.Main
{
    class Program
    {
        public static SwitchScreenshot.Discord.DiscordBot DiscordBotInstance;
        public static SwitchScreenshot.Twitter.TwitterBot TwitterBotInstance;

        static void Main(string[] args)
        {
            Utils.MainLog("Application started", "Info", "Main");
            // Now that I do no initial setup...no point opening a connection just to close it
            DiscordBotInstance = new SwitchScreenshot.Discord.DiscordBot();

            // Start the Discord bot on a new thread, after initializing the non-async stuff here
            DiscordBotInstance.Init();
            var DiscordThread = new Thread(DiscordBotInstance.Start().GetAwaiter().GetResult);
            DiscordThread.Start();

            TwitterBotInstance = new SwitchScreenshot.Twitter.TwitterBot();
            var TwitterThread = new Thread(TwitterBotInstance.Init().GetAwaiter().GetResult);
            TwitterThread.Start();
        }
    }
    public class Data 
    {
        /* 
         * SQL structure explanation:
         * 
         * We have three tables in our DB: Discord users, TwitterUsers, and DiscordTwitterUsers. 
         * The first two are traditional tables with primary keys. They each contain one column for the ids of respective users.
         * The last one is an intermediary table which stores the relationships between them, as they share a many to many relationship.
         * That is, I could subscribe to five Twitter accounts if I was a pro stalker, and five Discord accounts could be subscribed to one Twitter
         * account of a pro Splatoon 2 player.
         * 
         * Because of this many to many relationship, naturally a table without any primary keys is required /somewhere/.
         * However, if we had a single table with no primary key, and if we later decided we wanted to store more info about
         * either type of user later on, we'd end up wasting a large amount of storage space with our non-unique relationship entries, 
         * as in a single table setup we'd expect that for each entry, which describes a relationship, there'd also be all the data we want.
         *
         * While we currently don't want to store additional data on the users, it's better to do some additional setup now rather than be faced with
         * a bit of a rewrite later. 
         */

        public MySqlConnection Connection { get; set; }
        public DbDataReader Reader { get; set; }

        public Data()
        {
            // DI magic
            this.Connection = new MySqlConnection(Credentials);
            Connection.Open();
        }

        // Again this assumes a certain setup...sorry. Good password though
        public static string Credentials = @"server=localhost;userid=SwitchScreenshotsBot;database=SwitchScreenshotsDB;password=GOOD password;SslMode=None";

        // Method to return a list of Discord user IDs that are subscribed to a given twitter user. Used in determining who to PM screenshots to when
        // they are detected.
        public async Task<List<ulong>> GetSubscribedUsers(long twitterId)
        {
            List<ulong> Results = new List<ulong>();

            try {
                if (Connection.State == ConnectionState.Closed ) await Connection.OpenAsync();

                MySqlCommand Command = new MySqlCommand("SELECT DiscordId FROM DiscordTwitterUsers WHERE TwitterId=@twitterId", Connection);
                Command.Prepare();

                Command.Parameters.AddWithValue("@twitterId", twitterId);
                Reader = await Command.ExecuteReaderAsync();

                while (await Reader.ReadAsync()) {
                    Results.Add((ulong)Reader.GetInt64(0)); // Only one column
                }
            } catch (MySqlException e) {
                Utils.MainLog(
                    $"MySqlException occured while looking up Twitter ID for relevant Discord users: {e.ToString()}",
                    "Error",
                    "GetSubscribedUsers"
                );
            } finally {
                // Clean up
                if (Reader != null) Reader.Close();
                if (Connection != null) Connection.Close();
            }

            return Results;
            
            // Can select additional data from DiscordUsers with this ID if we want, but not for now.
        }

        // Overload to get subbed users from only username - used in Discord bot to affirm user is subbed
        // where we only have the username, and don't want to directly interact with the twitter bot
        public async Task<List<ulong>> GetSubscribedUsers(string twitterUsername)
        {
            long TwitterUserId = await Program.TwitterBotInstance.GetUserId(twitterUsername);
            return await GetSubscribedUsers(TwitterUserId);
        }

        // Method we use to list a discord user's subscriptions
        public async Task<List<string>> GetSubscriptions(ulong discordId)
        {
            // Twitter IDs are always longs -- while they're stored as UNSIGNED BIGINTS I could get them as ulongs 
            // but I'll keep them as longs for consistency's sake
            
            // This is a list of Ids, which aren't necessarily human-readable. We will parse to usernames later.
            List<long> IdResults = new List<long>();

            try {
                if (Connection.State == ConnectionState.Closed ) await Connection.OpenAsync();

                MySqlCommand Command = new MySqlCommand("SELECT TwitterId FROM DiscordTwitterUsers WHERE DiscordId=@D_ID", Connection);
                Command.Prepare();
                Command.Parameters.AddWithValue("@D_ID", discordId);
                Reader = await Command.ExecuteReaderAsync();
                while (await Reader.ReadAsync())
                {
                    // Read all the twitter ids that match and add them to list
                    IdResults.Add(Reader.GetInt64(0));
                }
            } catch (MySqlException e) {
                Utils.MainLog(
                    $"MySqlException occured while getting subscriptions for a Discord user: {e.ToString()}",
                    "Error",
                    "GetSubscriptions"
                );
            } finally {
                if (Reader != null) Reader.Close();
                if (Connection != null) await Connection.CloseAsync();
            }

            // Internal function to convert enumerable as LINQ doesn't accept async predicates
            async Task<List<string>> IdsToUsernames(List<long> IdList)
            {
                IEnumerable<Task<string>> TaskList = IdList.Select(id => Program.TwitterBotInstance.GetUsername(id));
                var Usernames = await Task.WhenAll(TaskList);
                return Usernames.ToList();
            }

            // Convert all entries to usernames using Twitter bot's lookup
            List<string> UsernameResults = await IdsToUsernames(IdResults);
            return UsernameResults;
        }

        public async Task SubscribeUser(ulong discordUserId, string twitterUsername, string discordUsername)
        {
            var TwitterUserId = Program.TwitterBotInstance.GetUserId(twitterUsername);

            try {
                if (Connection.State == ConnectionState.Closed ) await Connection.OpenAsync();
                // INSERT IGNORE: we are dealing with primary keys in these databases, so if a user subscribes to multiple accounts we
                // run into an exception, as they're supposed to be unique. IGNORE ignores that because we don't care.
                MySqlCommand Command = new MySqlCommand("INSERT IGNORE INTO DiscordUsers(Id) VALUES(@Id)", Connection);
                Command.Prepare();
                Command.Parameters.AddWithValue("@Id", discordUserId);
                await Command.ExecuteNonQueryAsync();

                Command.CommandText = "INSERT IGNORE INTO TwitterUsers(Id) VALUES (@Id2)";
                Command.Prepare();
                Command.Parameters.AddWithValue("@Id2", TwitterUserId);
                await Command.ExecuteNonQueryAsync();

                Command.CommandText = "INSERT IGNORE INTO DiscordTwitterUsers(DiscordId, TwitterId) VALUES(@DiscordId, @TwitterId)";
                Command.Prepare();
                Command.Parameters.AddWithValue("@DiscordId", discordUserId);
                Command.Parameters.AddWithValue("@TwitterId", TwitterUserId);
                await Command.ExecuteNonQueryAsync();

                // Follow the user so that we can narrow down our events (not looking at the whole of Twitter)
                await Program.TwitterBotInstance.FollowUser(twitterUsername, discordUsername);
            } catch (MySqlException e) {
                Utils.MainLog(
                    $"MySqlException occured while updating DB records for recently registered user: {e.ToString()}",
                    "Error", "SubscribeUser");
            } finally {
                if (Connection != null) await Connection.CloseAsync();
            }
        }

        public async Task UnsubscribeUser(ulong discordUserId, string twitterUsername, string discordUsername)
        {
            long twitterUserId = await Program.TwitterBotInstance.GetUserId(twitterUsername);

            try {
                if (Connection.State == ConnectionState.Closed ) await Connection.OpenAsync();
                // First delete the relationship, as we know that's gone
                MySqlCommand Command = new MySqlCommand("DELETE FROM DiscordTwitterUsers WHERE DiscordId=@D_ID AND TwitterId=@T_ID;", Connection);
                Command.Prepare();
                Command.Parameters.AddWithValue("@D_ID", discordUserId);
                Command.Parameters.AddWithValue("@T_ID", twitterUserId);
                await Command.ExecuteNonQueryAsync();

                // Cleanup:
                // If that was the only relationship they had, now remove the discord/twitter users from their standalone tables
                // If statements in pure SQL weren't playing nice so I do them in the code
                Command.CommandText = "SELECT 1 FROM DiscordTwitterUsers WHERE DiscordId=@D_ID;";
                Reader = await Command.ExecuteReaderAsync();
                if (!Reader.Read()) { // If empty result
                    Reader.Close(); // Close off before doing more commands
                    Command.CommandText = "DELETE FROM DiscordUsers WHERE Id=@D_ID;";
                    await Command.ExecuteNonQueryAsync();
                }

                // Then do the same for out-of-use Twitter accounts
                Command.CommandText = "SELECT 1 FROM DiscordTwitterUsers WHERE TwitterId=@T_ID;";
                Reader = Command.ExecuteReader();
                if (!Reader.Read()) {
                    Reader.Close();
                    Command.CommandText = "DELETE FROM TwitterUsers WHERE Id=@T_ID;";
                    await Command.ExecuteNonQueryAsync();
                    // If nobody is interested in this twitter account, unfollow
                    await Program.TwitterBotInstance.UnfollowUser(twitterUserId, discordUsername);
                }
            } catch (MySqlException e) {
                Utils.MainLog(
                    $"MySqlException occured while unsubscribing users: {e.ToString()}",
                    "Error", "UnsubscribeUser");
            } finally {
                if (Reader != null) Reader.Close();
                if (Connection != null) await Connection.CloseAsync();
            }
        }

        
        public async Task PassScreenshot(long twitterId, string screenshotUrl)
        {
            // Find out what discord user(s) to send it to
            var UserIds = await GetSubscribedUsers(twitterId);
            foreach (ulong UserId in UserIds)
            {
                await Program.DiscordBotInstance.SendScreenshot(UserId, screenshotUrl);
            }
        }
    }

    public static partial class Utils
    {
        // Was tempted to make my own enums and types (e.g. a copycat LogSeverity from D.NET) but it's probably not worth it
        public static void MainLog(string message, string severity, string source)
        {
            string TimeString = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.Local)
                .ToString("HH:mm:ss");
            Console.WriteLine($"[Main | {TimeString}] ({severity}) {source}: {message}");
        }
    }
}
