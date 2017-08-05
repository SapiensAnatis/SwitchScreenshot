using System;
using TweetSharp;

namespace SwitchScreenshot.Twitter
{
    class TwitterBot
    {
        private static string _ConsumerKey = "";
        private static string _ConsumerSecretKey = "";
        private static string _AccessToken = "";
        private static string _AccessSecretToken = "";

        private static TwitterService _Service;

        // Can't use constructors because we have to have a method which we explicitly control outside
        // in order to delegate to a new thread. We don't need control over instance members from Main anyway.
        static void Init() => new TwitterBot().Start();
        

        private void Start()
        {
            // Retrieve API keys.
            string[] TokenLines = System.IO.File.ReadAllLines("/home/jay/Programming/SwitchScreenshot/SwitchScreenshot/TwitterTokens.txt");
            _ConsumerKey = TokenLines[0];
            _ConsumerSecretKey = TokenLines[1];
            _AccessToken = TokenLines[2];
            _AccessSecretToken = TokenLines[3];

            _Service = new TwitterService(
                _ConsumerKey,
                _ConsumerSecretKey,
                _AccessToken,
                _AccessSecretToken,
            )
        }
    }
}