using System;
using System.Collections.Generic;
using System.Data;
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
            // Now that I do no initial setup...no point opening a connection just to close it
            DiscordBotInstance = new SwitchScreenshot.Discord.DiscordBot();

            // Start the Discord bot on a new thread, after initializing the non-async stuff here
            DiscordBotInstance.Init();
            DiscordThread = new Thread(DiscordBotInstance.Start().GetAwaiter().GetResult);
            DiscordThread.Start();

            TwitterBotInstance = new SwitchScreenshot.Twitter.TwitterBot();
            Thread TwitterThread = new Thread(TwitterBotInstance.Init);
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
        public List<ulong> GetSubscribedUsers(long twitterId)
        {
            MySqlDataReader Reader = null;
            List<ulong> Results = new List<ulong>();

            try {
                if (Connection.State == ConnectionState.Closed ) Connection.Open();

                MySqlCommand Command = new MySqlCommand("SELECT DiscordId FROM DiscordTwitterUsers WHERE TwitterId=@twitterId", Connection);
                Command.Prepare();

                Command.Parameters.AddWithValue("@twitterId", twitterId);
                Reader = Command.ExecuteReader();

                while (Reader.Read()) {
                    Results.Add(Reader.GetUInt64(0)); // Only one column
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

        public void SubscribeUser(ulong discordUserId, string twitterUsername, string discordUsername)
        {
            var TwitterUserId = Program.TwitterBotInstance.GetUserId(twitterUsername);

            try {
                // INSERT IGNORE: we are dealing with primary keys in these databases, so if a user subscribes to multiple accounts we
                // run into an exception, as they're supposed to be unique. IGNORE ignores that because we don't care.
                MySqlCommand Command = new MySqlCommand("INSERT IGNORE INTO DiscordUsers(Id) VALUES(@Id)");
                Command.Connection = Connection;
                Command.Prepare();
                Command.Parameters.AddWithValue("@Id", discordUserId);
                Command.ExecuteNonQuery();

                Command.CommandText = "INSERT IGNORE INTO TwitterUsers(Id) VALUES (@Id2)";
                Command.Prepare();
                Command.Parameters.AddWithValue("@Id2", TwitterUserId);
                Command.ExecuteNonQuery();

                Command.CommandText = "INSERT IGNORE INTO DiscordTwitterUsers(DiscordId, TwitterId) VALUES(@DiscordId, @TwitterId)";
                Command.Prepare();
                Command.Parameters.AddWithValue("@DiscordId", discordUserId);
                Command.Parameters.AddWithValue("@TwitterId", TwitterUserId);
                Command.ExecuteNonQuery();

                // Follow the user so that we can narrow down our events (not looking at the whole of Twitter)
                Program.TwitterBotInstance.FollowUser(twitterUsername, discordUsername);
            } catch (MySqlException e) {
                Utils.MainLog(
                    $"MySqlException occured while updating DB records for recently registered user: {e.ToString()}",
                    "Error", "SubscribeUser");
            } finally {
                if (Connection != null) Connection.Close();
            }
        }

        
        public async Task PassScreenshot(long twitterId, string screenshotUrl)
        {
            // Find out what discord user(s) to send it to
            var UserIds = GetSubscribedUsers(twitterId);
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
