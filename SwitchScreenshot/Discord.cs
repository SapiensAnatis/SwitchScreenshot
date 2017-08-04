using System;
using System.Threading.Tasks;
using System.Reflection;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;

public class DiscordBot
{
    private CommandService _Commands;
    private DiscordSocketClient _Client;
    private IServiceProvider _Services;

    public static void Init() => new DiscordBot().Start().GetAwaiter().GetResult();

    public async Task Start()
    {
        _Client = new DiscordSocketClient();
        _Commands = new CommandService();

        string Token = System.IO.File.ReadAllLines(
            "/home/jay/Programming/SwitchScreenshot/SwitchScreenshot/DiscordToken.txt"
        )[0]; // First line of file to avoid trailing newlines. Also hardcoded path cause this isn't a public bot

        _Services = new ServiceCollection().BuildServiceProvider();

        await InstallCommands();

        _Client.Log += Log;

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
            await Log(
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

    private Task Log(LogMessage message)
    {
        string TimeString = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.Local)
            .ToString("HH:mm:ss");
        Console.WriteLine($"[Discord | {TimeString}] ({message.Severity}) {message.Source}: {message.Message}");
        return Task.CompletedTask;
    }
}

public class CommandModule : ModuleBase
{
    [Command("register"), Summary("Register a Twitter account to the Discord account in use to enable screenshot mirroring.")]
    public async Task Register([Summary("The @username to register as the Twitter account")] string username)
    {
        // TODO
        await ReplyAsync($"Hello {username}");
    }
}