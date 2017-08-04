using System;
using System.Threading;


namespace SwitchScreenshot
{
    class Program
    {
        static void Main(string[] args)
        {
            // Start the Discord bot on a new thread
            Thread DiscordThread = new Thread(DiscordBot.Init);
            DiscordThread.Start();
        }
    }
}
