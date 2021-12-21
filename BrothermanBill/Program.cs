// See https://aka.ms/new-console-template for more information
using BrothermanBill;
using BrothermanBill.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Reflection;
using Victoria;

// run brothermanbill on a rasp pi pogu with a little logo and stuff on it, though no access to windows speech recognition
// have it check github for new releases? 
// have local server for logs

StartLavalinkAsync();



// this could also be the new .net6 ConfigurationManager
var config = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
             .AddJsonFile("appsettings.json")
             .AddUserSecrets<Program>()
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
    .AddSingleton<PingService>()
    .AddSingleton<AudioService>()
    .AddSingleton<SimpleAudioService>()
    .Configure<InstanceId>(x => x.Id = Guid.NewGuid())
    .Configure<CommandServiceConfig>(x => new CommandServiceConfig
    {
        CaseSensitiveCommands = true,
        LogLevel = LogSeverity.Debug
    })
    .AddLavaNode(x => x.SelfDeaf = false)
    .BuildServiceProvider();


var commands = services.GetRequiredService<CommandService>();
var socketClient = services.GetRequiredService<DiscordSocketClient>();
var commandHandler = services.GetRequiredService<CommandHandlerService>();
var lavaNode = services.GetRequiredService<LavaNode>();

await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);

await commandHandler.InstallCommandsAsync();

Console.WriteLine($"Instance ID: {commandHandler.InstanceId}");

socketClient.Log += (LogMessage msg) =>
{
    Console.WriteLine(msg.ToString());
    return Task.CompletedTask;
};

commands.Log += (LogMessage msg) =>
{
    Console.WriteLine(msg.ToString());
    return Task.CompletedTask;
};
//var discord = new DiscordWrapper();
//await discord.Run();


await socketClient.LoginAsync(TokenType.Bot, config["DiscordBotToken"]);
await socketClient.StartAsync();

socketClient.Ready += async () =>
{
    var guild = socketClient.Guilds.FirstOrDefault();

    var categoryId = guild.Channels.FirstOrDefault(x => x.Name == "Brother Bill's House")?.Id ?? (ulong)0;

    if (categoryId == 0)
    {
        var newCategory = await guild.CreateCategoryChannelAsync("Brother Bill's House");
        categoryId = newCategory.Id;
    }

    var roomId = guild.Channels.FirstOrDefault(x => x.Name == "kkona-truck")?.Id ?? (ulong)0;


    if (roomId == 0)
    {
        var newRoom = await guild.CreateTextChannelAsync("kkona-truck", channelOptions =>
        {
            channelOptions.CategoryId = categoryId;
            channelOptions.Position = 1;
        });
        roomId = newRoom.Id;
    }

    var channel = socketClient.GetChannel(roomId) as IMessageChannel;
    await channel.SendMessageAsync($"ping {services.GetRequiredService<CommandHandlerService>().InstanceId}");

    if (!lavaNode.IsConnected)
    {
        await lavaNode.ConnectAsync();
    }


    return;
};

// https://github.com/d4n3436/Fergun/blob/58fceda8463ee67a49708547fc20f928a8748361/src/FergunClient.cs#L232
static void StartLavalinkAsync()
{
    var processList = Process.GetProcessesByName("java");
    if (processList.Length == 0)
    {
        string lavalinkFile = Path.Combine(AppContext.BaseDirectory, "Lavalink", "Lavalink.jar");
        if (!File.Exists(lavalinkFile)) return;

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

    return;
}


await Task.Delay(Timeout.Infinite);