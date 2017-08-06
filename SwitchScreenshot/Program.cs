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
            // Start the Discord bot on a new thread
            Thread DiscordThread = new Thread(DiscordBotInstance.Init);
            DiscordThread.Start();

            TwitterBotInstance = new SwitchScreenshot.Twitter.TwitterBot();
            Thread TwitterThread = new Thread(TwitterBotInstance.Init);
            TwitterThread.Start();
        }
    }
    public class Data 
    {
        public MySqlConnection Connection { get; set; }

        public Data()
        {
            // DI magic
            this.Connection = new MySqlConnection(Credentials);
            Connection.Open();
        }

        public static string Credentials = @"server=localhost;userid=SwitchScreenshotsBot;database=SwitchScreenshotsDB;password=GOOD password;SslMode=None";

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
                await Program.DiscordBotInstance.SendScreenshot(UserId, screenshotUrl);
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
