// See https://aka.ms/new-console-template for more information
using BrothermanBill;
using BrothermanBill.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Victoria;

await StartLavalinkAsync();

// this could also be the new .net6 ConfigurationManager
var config = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
             .AddJsonFile("appsettings.json")
             .AddUserSecrets<Program>(optional: true) // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-6.0
             .Build();

// https://dsharpplus.github.io/natives/index.html
// https://docs.microsoft.com/en-us/dotnet/core/deploying/single-file
// https://andrewlock.net/exploring-dotnet-6-part-10-new-dependency-injection-features-in-dotnet-6/
// https://stackoverflow.com/questions/63585782/inject-a-service-with-parameters-in-asp-net-core-where-one-parameter-is-a-neste
// perhaps give recognizer hints by getting together all the commands
await using var services = new ServiceCollection()
    .AddSingleton<DiscordSocketClient>()
    .AddSingleton<CommandService>()
    .AddSingleton<CommandHandlerService>()
    .AddSingleton<AudioService>()
    .AddSingleton<MemeService>()
    .AddSingleton<EmbedHandler>()
    .AddSingleton<StatusService>()
    .AddSingleton<UptimeService>()
    .Configure<CommandServiceConfig>(x => new CommandServiceConfig
    {
        CaseSensitiveCommands = true,
        LogLevel = LogSeverity.Debug
    })
    .AddLavaNode(x => x.SelfDeaf = false)
    .AddLogging(builder => builder.AddConsole())
    .BuildServiceProvider();

var commands = services.GetRequiredService<CommandService>();
var socketClient = services.GetRequiredService<DiscordSocketClient>();
var commandHandler = services.GetRequiredService<CommandHandlerService>();
var lavaNode = services.GetRequiredService<LavaNode>();
var loggerFactory = services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<Program>();
var statusService = services.GetRequiredService<StatusService>();
var upTimeService = services.GetRequiredService<UptimeService>();

await commandHandler.InstallCommandsAsync();

await statusService.SetStatus("Starting up");

socketClient.Log += (LogMessage msg) =>
{
    logger.LogInformation(msg.ToString());
    return Task.CompletedTask;
};

commands.Log += (LogMessage msg) =>
{
    logger.LogInformation(msg.ToString());
    return Task.CompletedTask;
};

socketClient.UserVoiceStateUpdated += async (user, before, after) =>
{
    var currentUser = socketClient.CurrentUser.Username;
    if (after.VoiceChannel is null && before.VoiceChannel.Users.Any(x => x.Username == currentUser))
    {
        var hasOtherUsers = before.VoiceChannel.Users.Any(x => x.Username != currentUser);
        if (!hasOtherUsers)
        {
            logger.LogInformation($"Leaving {before.VoiceChannel} as the last user, {user.Username}, has left.");
            await before.VoiceChannel.DisconnectAsync();
        }
    }
};

await socketClient.LoginAsync(TokenType.Bot, config["DiscordBotToken"]);
await socketClient.StartAsync();

socketClient.Ready += async () =>
{
    if (!lavaNode.IsConnected)
    {
        await statusService.SetStatus("Waiting for LavaLink connection");
        await lavaNode.ConnectAsync();
    }

    return;
};

// https://github.com/d4n3436/Fergun/blob/58fceda8463ee67a49708547fc20f928a8748361/src/FergunClient.cs#L232
static Task StartLavalinkAsync()
{
    // written knowing full well there might be other java processes running, I'm developing this for my Raspi
    var processList = Process.GetProcessesByName("java");
    if (processList.Length == 0)
    {
        string lavalinkFile = Path.Combine(AppContext.BaseDirectory, "Lavalink", "Lavalink.jar");
        if (!File.Exists(lavalinkFile)) return Task.CompletedTask;

        var process = new ProcessStartInfo
        {
            FileName = "java",
            Arguments = $"-jar \"{Path.Combine(AppContext.BaseDirectory, "Lavalink")}/Lavalink.jar\"",
            WorkingDirectory = Path.Combine(AppContext.BaseDirectory, "Lavalink"),
            UseShellExecute = true,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Minimized
        };

        Process.Start(process);
    }

    return Task.CompletedTask;
}


await Task.Delay(Timeout.Infinite);