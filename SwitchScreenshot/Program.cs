using System;
using System.Collections.Generic;
using System.Threading;

using MySql.Data.MySqlClient;

// Controller
namespace SwitchScreenshot.Main
{
    class Program
    {
        static void Main(string[] args)
        {
            // Initialize database
            // Connect
            MySqlConnection Connection = null;

            try {
                Console.WriteLine("Initializing database...");
                Connection = new MySqlConnection(Data.Credentials);
                Connection.Open();
                Console.WriteLine($"Connection opened. MySQL version: {Connection.ServerVersion}");

                // Get tables setup if not already
                MySqlCommand Command = new MySqlCommand("CREATE DATABASE IF NOT EXISTS DTBridge;");
                Command.Connection = Connection;
                Command.ExecuteNonQuery();
                Command.CommandText = "CREATE TABLE IF NOT EXISTS DiscordUsers(Id BIGINT UNSIGNED NOT NULL PRIMARY KEY);";
                Command.ExecuteNonQuery();
                Command.CommandText = "CREATE TABLE IF NOT EXISTS TwitterUsers(Id BIGINT UNSIGNED NOT NULL PRIMARY KEY);";
                Command.ExecuteNonQuery();
                // Intermediary table
                Command.CommandText = "CREATE TABLE IF NOT EXISTS DiscordTwitterUsers(DiscordId BIGINT UNSIGNED NOT NULL, TwitterId BIGINT UNSIGNED NOT NULL);";
                Command.ExecuteNonQuery();
            } catch (MySqlException e) {
                Console.WriteLine($"SQLError {e.ToString()}");
                return;
            } finally {
                if (Connection != null) {
                    Connection.Close();
                    Console.WriteLine("Connection closed.");
                }

                Console.WriteLine("Database initialization complete.");
            }
            
            
            
            // Start the Discord bot on a new thread
            Thread DiscordThread = new Thread(SwitchScreenshot.Discord.DiscordBot.Init);
            DiscordThread.Start();
        }
    }
    static class Data 
    {
        public static string Credentials = @"server=localhost;userid=DTBridgeBot;password=GOOD password;database=DTBridgeDB;SslMode=None";

        public static List<ulong> GetSubscribedUsers(ulong twitterId)
        {
            MySqlConnection Connection = null;
            MySqlDataReader Reader = null;
            List<ulong> Results = new List<ulong>();

            try {
                Connection = new MySqlConnection(Credentials);
                Connection.Open();
                MySqlCommand Command = new MySqlCommand("SELECT DiscordId FROM DiscordTwitterUsers WHERE TwitterId=@twitterId", Connection);
                Command.Prepare();

                Command.Parameters.AddWithValue("@twitterId", twitterId);
                Reader = Command.ExecuteReader();

                while (Reader.Read()) {
                    Results.Add(Reader.GetUInt64(0)); // Only one column
                }
            } catch (MySqlException e) {
                Console.WriteLine($"Error occured while looking up Twitter ID for relevant Discord users: {e.ToString()}");
            } finally {
                if (Reader != null) Reader.Close();
                if (Connection != null) Connection.Close();
            }

            return Results;
            
            // Can select additional data from DiscordUsers with this ID if we want, but not for now.
        }

        public static void SubscribeUser(ulong DiscordUserId, string TwitterUsername)
        {
            // TODO: query Twitter side for twitter ID from @whatever
            // Until then placeholders for my theoretical SQL
            ulong TwitterUserId = 0032309376230967;
            
            MySqlConnection Connection = null;
            try {
                Connection = new MySqlConnection(Credentials);
                Connection.Open();
                MySqlCommand Command = new MySqlCommand("INSERT INTO DiscordUsers(Id) VALUES(@Id)");
                Command.Connection = Connection;
                Command.Prepare();
                Command.Parameters.AddWithValue("@Id", DiscordUserId);
                Command.ExecuteNonQuery();

                Command.CommandText = "INSERT INTO TwitterUsers(Id) VALUES (@Id)";
                Command.Prepare();
                Command.Parameters.AddWithValue("@Id", TwitterUserId);
                Command.ExecuteNonQuery();

                Command.CommandText = "INSERT INTO DiscordTwitterUsers(DiscordId, TwitterId) VALUES(@DiscordId, @TwitterId)";
                Command.Prepare();
                Command.Parameters.AddWithValue("@DiscordId", DiscordUserId);
                Command.Parameters.AddWithValue("@TwitterId", TwitterUserId);
                Command.ExecuteNonQuery();
            } catch (MySqlException e) {
                Console.WriteLine($"Error occured while updating DB records for recently registered user: {e.ToString()}");
            } finally {
                if (Connection != null) Connection.Close();
            }
        }
    }
}
