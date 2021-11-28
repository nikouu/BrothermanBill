// See https://aka.ms/new-console-template for more information
using BrothermanBill;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
                 .SetBasePath(Directory.GetCurrentDirectory())
                 .AddJsonFile("appsettings.json")
                 .AddUserSecrets<Program>()
                 .Build();

Console.WriteLine("Hello, World!");

var discord = new DiscordWrapper("");
await discord.Run();