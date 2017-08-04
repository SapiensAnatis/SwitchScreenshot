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
        
        // So, we don't really need a command prefix for this bot; because it only really has a few commands
        // and operates almost entirely through DMs. 
        var Context = new CommandContext(_Client, Message);
        // Exec command. Use 0 instead of starting after prefix because there is no prefix
        var Result = await _Commands.ExecuteAsync(Context, 0, _Services);
        if (!Result.IsSuccess)
            await Context.Channel.SendMessageAsync(Result.ErrorReason);
    }

    private Task Log(LogMessage message)
    {
        Console.WriteLine($"({message.Severity}) {message.Message}");
        return Task.CompletedTask;
    }
}

public class CommandModule : ModuleBase
{
    [Command("register"), Summary("Register a Twitter account to the Discord account in use to enable screenshot mirroring.")]
    public async Task Register([Summary("The @username to register as the Twitter account")] string username)
    {
        // TODO
    }
}