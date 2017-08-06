using System;
using System.Linq;
using System.Collections.Generic;
using Tweetinvi;
using Tweetinvi.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Tweetinvi.Events;

namespace SwitchScreenshot.Twitter
{
    class TwitterBot
    {

        private static string _ConsumerKey = "";
        private static string _ConsumerSecretKey = "";
        private static string _AccessToken = "";
        private static string _AccessSecretToken = "";

        private static ServiceCollection _ServiceCollection;
        private static IServiceProvider _Services;

        private static IUserStream _Stream;

        private SwitchScreenshot.Main.Data _DataInstance;


        // Can't use constructors because we have to have a method which we explicitly control outside
        // in order to delegate to a new thread. We don't need control over instance members from Main anyway.
        public static void Init() => new TwitterBot().Start();
        

        private void Start()
        {  
            // Initialize data instance
            _DataInstance = new SwitchScreenshot.Main.Data();

            // Retrieve API keys.
            string[] TokenLines = System.IO.File.ReadAllLines("/home/jay/Programming/SwitchScreenshot/SwitchScreenshot/TwitterTokens.txt");
            _ConsumerKey = TokenLines[0];
            _ConsumerSecretKey = TokenLines[1];
            _AccessToken = TokenLines[2];
            _AccessSecretToken = TokenLines[3];

            // "log in"
            Auth.SetUserCredentials(_ConsumerKey, _ConsumerSecretKey, _AccessToken, _AccessSecretToken);
            var AuthenticatedUser = User.GetAuthenticatedUser();
            Utils.TwitterLog($"Logged into Twitter as {AuthenticatedUser.Name}", "Info", "Init");;
            _Stream = Tweetinvi.Stream.CreateUserStream();
            _Stream.StartStream();
            // Stream events. Pretty much the bulk of the code
            _Stream.StreamStopped += (sender, args) => {
                Utils.TwitterLog($"Stream halted: {args.DisconnectMessage} ({args.Exception}",
                "Error", "Source unknown");
            };

            _Stream.TweetCreatedByFriend += OnFollowerTweet;
            
        }

        private async void OnFollowerTweet(object sender, TweetReceivedEventArgs args)
        {
            // Preconditions
            var Tweet = args.Tweet;
            // If it doesn't contain the right hashtag
            if (!Tweet.Text.Contains("#NintendoSwitch")) return;
            // If it doesn't contain exactly one picture (you can only post one at a time)
            if (Tweet.Media.Count != 1) return;

            // Once we're sure it's (probably) a screenshot, send it through
            await _DataInstance.PassScreenshot(args.Tweet.CreatedBy.Id, Tweet.Media.FirstOrDefault().MediaURLHttps);
        }

        public static void SubscribeToUser(string username, string discordName)
        {
            // Follow a user when they're subscribed to.
            var RelevantUser = User.GetUserFromScreenName(username);
            Console.WriteLine($"{RelevantUser.Name}");
            bool Result = User.FollowUser(RelevantUser);
            if (!Result) 
                Utils.TwitterLog($"Following user returned false.", "Error?", "SubscribeToUser");
            
            // TODO: Reply to this message with <something> to stop this stalker from doing it
            Tweet.PublishTweet($"@{username}: Per their request, I will now be sending all screenshots you tweet from your Switch to {discordName}.");
        }

        
    }

    public static partial class Utils
    {
        public static void TwitterLog(string message, string severity, string source)
        {
            string TimeString = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.Local)
                .ToString("HH:mm:ss");
            Console.WriteLine($"[Twitter | {TimeString}] ({severity}) {source}: {message}");
        }
    }
}