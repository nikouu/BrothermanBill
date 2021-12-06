// See https://aka.ms/new-console-template for more information
using BrothermanBill;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

// this could also be the new .net6 ConfigurationManager
var config = new ConfigurationBuilder()
                 .SetBasePath(Directory.GetCurrentDirectory())
                 .AddJsonFile("appsettings.json")
                 .AddUserSecrets<Program>()
                 .Build();

// https://andrewlock.net/exploring-dotnet-6-part-10-new-dependency-injection-features-in-dotnet-6/
await using var services = new ServiceCollection()
    .AddSingleton<DiscordSocketClient>()
    .AddSingleton<CommandService>()
    .AddSingleton<CommandHandler>()
    .AddSingleton<InfoModule>()
    .BuildServiceProvider();

var commands = services.GetRequiredService<CommandService>();
var socketClient = services.GetRequiredService<DiscordSocketClient>();
var commandHandler = services.GetRequiredService<CommandHandler>();

//await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);

await commandHandler.InstallCommandsAsync();


socketClient.Log += (LogMessage msg) => {
    Console.WriteLine(msg.ToString());
    return Task.CompletedTask;
};
//var discord = new DiscordWrapper();
//await discord.Run();

await socketClient.LoginAsync(TokenType.Bot, config["DiscordBotToken"]);
await socketClient.StartAsync();


Console.WriteLine("Hello, World!");


await Task.Delay(Timeout.Infinite);