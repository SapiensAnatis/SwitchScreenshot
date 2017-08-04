using System;
using System.Collections.Generic;
using System.Threading;

using MySql.Data.MySqlClient;

// Controller
namespace SwitchScreenshot.Main
{
    class Program
    {

        private static string _Credentials = @"server=localhost;userid=DTBridgeBot;password=GOOD password;database=DTBridgeDB;SslMode=None";

        static void Main(string[] args)
        {
            // Initialize database
            // Connect
            MySqlConnection Connection = null;

            try {
                Console.WriteLine("Initializing database...");
                Connection = new MySqlConnection(_Credentials);
                Connection.Open();
                Console.WriteLine($"Connection opened. MySQL version: {Connection.ServerVersion}");

                // Get tables setup if not already
                MySqlCommand Command = new MySqlCommand("CREATE DATABASE IF NOT EXISTS DTBridge;");
                Command.Connection = Connection;
                Command.ExecuteNonQuery();
                Command = new MySqlCommand("CREATE TABLE IF NOT EXISTS DiscordUsers(Id BIGINT UNSIGNED NOT NULL PRIMARY KEY);");
                Command.ExecuteNonQuery();
                Command = new MySqlCommand("CREATE TABLE IF NOT EXISTS TwitterUsers(Id BIGINT UNSIGNED NOT NULL PRIMARY KEY);");
                Command.ExecuteNonQuery();
                // Intermediary table
                Command = new MySqlCommand("CREATE TABLE IF NOT EXISTS DiscordTwitterUsers(DiscordId BIGINT UNSIGNED NOT NULL, TwitterId BIGINT UNSIGNED NOT NULL);");
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

        static List<ulong> GetSubscribedUsers(ulong twitterId)
        {
            MySqlConnection Connection = null;
            MySqlDataReader Reader = null;
            List<ulong> Results = new List<ulong>();

            try {
                Connection = new MySqlConnection(_Credentials);
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
    }

    // Static class which both namespaces can access and use.

    // The Discord side needs to update it with new registrations. 
    // Twitter side does not need to use it as it's just reading tweets and sending signals.
    // This namespace needs to read it to convert twitter usernames to Discord user IDs
    static class Data
    {
        // Indexed by Twitter username you'll find a list of discord user IDs (ulong)
        public static Dictionary<ulong, List<ulong>> Registrations = new Dictionary<ulong, List<ulong>>();
    }
}
