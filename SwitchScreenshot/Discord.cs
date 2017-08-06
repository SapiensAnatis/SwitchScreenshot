using System;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace SwitchScreenshot.Discord 
{
    public class DiscordBot
    {
        private CommandService _Commands;
        private DiscordSocketClient _Client;
        private ServiceCollection _ServiceCollection;
        private IServiceProvider _Services;

        public void Init() => new DiscordBot().Start().GetAwaiter().GetResult();

        public async Task Start()
        {
            _Client = new DiscordSocketClient();
            _Commands = new CommandService();

            string Token = System.IO.File.ReadAllLines(
                "/home/jay/Programming/SwitchScreenshot/SwitchScreenshot/DiscordToken.txt"
            )[0]; // First line of file to avoid trailing newlines. Also hardcoded path cause this isn't a public bot

            _ServiceCollection = new ServiceCollection();
            // Registering DI services. Add this to allow writing to the database
            _ServiceCollection.AddScoped<SwitchScreenshot.Main.Data>();


            _Services = _ServiceCollection.BuildServiceProvider();

            await InstallCommands();

            _Client.Log += Utils.DiscordLog;

            await _Client.LoginAsync(TokenType.Bot, Token);
            await _Client.StartAsync();

            await Task.Delay(-1); // Block the thread so the bot runs until process terminated
        }

        public async Task InstallCommands()
        {
            _Client.MessageReceived += HandleCommand;
            await _Commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }


        public async Task HandleCommand(SocketMessage messageIn)
        {
            var Message = messageIn as SocketUserMessage;
            if (Message == null) return; // Return if it was a system message or other message not written by a user

            if (Message.Author.Id == _Client.CurrentUser.Id) return; // No recursion pls

            if (!(Message.Channel is SocketDMChannel channel)) return; // Not in guilds pls. Mostly because no prefix and so can be triggered in normal conversation
            
            // So, we don't really need a command prefix for this bot; because it only really has a few commands
            // and operates almost entirely through DMs. 
            var Context = new CommandContext(_Client, Message);
            // Exec command. Use 0 instead of starting after prefix because there is no prefix
            var Result = await _Commands.ExecuteAsync(Context, 0, _Services);
            if (!Result.IsSuccess) {
                await Utils.DiscordLog(
                    new LogMessage(
                        LogSeverity.Info, "Commands", 
                        $"Failed to parse command message '{Message.Content}': {Result.ErrorReason}"
                    )
                );

                switch (Result.Error) {
                    case CommandError.BadArgCount:
                        await Context.Channel.SendMessageAsync("You provided the wrong number of arguments for that command.");
                        break;
                    case CommandError.UnknownCommand:
                        await Context.Channel.SendMessageAsync("I don't know that command. Valid commands are: register, unregister");
                        break;
                    default:
                        await Context.Channel.SendMessageAsync(Result.ErrorReason);
                        break;
                }
                
            }
            
        }

        public async Task SendScreenshot(ulong recipientUserId, string url)
        {
            var User = _Client.GetUser(recipientUserId);
            await User.SendMessageAsync(url);
        }

    }


    public class CommandModule : ModuleBase
    {
        private SwitchScreenshot.Main.Data _SQLService; 
        public CommandModule(SwitchScreenshot.Main.Data SQLService)
        {
            _SQLService = SQLService;
        }

        [Command("register"), Summary("Register a Twitter account to the Discord account in use to enable screenshot mirroring.")]
        public async Task Register([Remainder, Summary("The @username to register as the Twitter account")] string username)
        {
            /* Input validation
            * Twitter usernames cannot:
            *
            * - Contain the words Twitter or Admin unless official accounts (I'll let this one slide, because those usernames could be 
            *   posting screenshots for whatever reason)
            * - Be longer than 15 characters
            * - Contain non-alphanumeric characters that aren't underscores
            *
            */
            
            if (username.Length > 15) {
                await ReplyAsync("That's an invalid Twitter username; it's too long.");
                return;
            } else if (!username.All(c => char.IsLetterOrDigit(c) || c == '_')) { // Asserts that all characters in string are either alphanumeric or underscores.
                await ReplyAsync("That's an invalid Twitter username; it contains disallowed characters.");
                return;
            }
            
            IUser Author = Context.Message.Author; // Shorthand

            await Utils.DiscordLog(
                new LogMessage(LogSeverity.Info, "RegisterCommand", $"Twitter username validated - updating DB by adding" +  
                $" Discord user {Author.Username}#{Author.Discriminator} (ID: {Author.Id}) under username @{username}")
            );

            await ReplyAsync($"I'll follow {username}");
            _SQLService.SubscribeUser(Author.Id, username, $"{Context.User.Username}#{Context.User.Discriminator}");
        }
    }

    public static partial class Utils
    {
        public static Task DiscordLog(LogMessage message)
        {
            string TimeString = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.Local)
                .ToString("HH:mm:ss");
            Console.WriteLine($"[Discord | {TimeString}] ({message.Severity}) {message.Source}: {message.Message}");
            return Task.CompletedTask;
        }
    }
}