// See https://aka.ms/new-console-template for more information
using BrothermanBill;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var config = new ConfigurationBuilder()
                 .SetBasePath(Directory.GetCurrentDirectory())
                 .AddJsonFile("appsettings.json")
                 .AddUserSecrets<Program>()
                 .Build();

// https://andrewlock.net/exploring-dotnet-6-part-10-new-dependency-injection-features-in-dotnet-6/
var services = new ServiceCollection().AddSingleton<InfoModule>();



Console.WriteLine("Hello, World!");

var discord = new DiscordWrapper(config["DiscordBotToken"]);
await discord.Run();