// See https://aka.ms/new-console-template for more information
using BrothermanBill;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

// run brothermanbill on a rasp pi pogu with a little logo and stuff on it, though no access to windows speech recognition
// have it check github for new releases? 
// have local server for logs

// this could also be the new .net6 ConfigurationManager
var config = new ConfigurationBuilder()
                 .SetBasePath(Directory.GetCurrentDirectory())
                 .AddJsonFile("appsettings.json")
                 .AddUserSecrets<Program>()
                 .Build();
// https://docs.microsoft.com/en-us/dotnet/core/deploying/single-file
// https://andrewlock.net/exploring-dotnet-6-part-10-new-dependency-injection-features-in-dotnet-6/
// https://stackoverflow.com/questions/63585782/inject-a-service-with-parameters-in-asp-net-core-where-one-parameter-is-a-neste
await using var services = new ServiceCollection()
    .AddSingleton<DiscordSocketClient>()
    .AddSingleton<CommandService>()
    .AddSingleton<CommandHandler>()
    .AddSingleton<InfoModule>()
    .Configure<InstanceId>(x => x.Id = Guid.NewGuid())
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

Console.WriteLine($"Instance ID: {commandHandler.InstanceId}");
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


    if (roomId == 0) {
        var newRoom = await guild.CreateTextChannelAsync("kkona-truck", channelOptions => { 
            channelOptions.CategoryId = categoryId;
            channelOptions.Position = 1;
        });
        roomId = newRoom.Id;
    }

    var channel = socketClient.GetChannel(roomId) as IMessageChannel;
    await channel.SendMessageAsync($"ping {services.GetRequiredService<CommandHandler>().InstanceId}");
    

    return;
};




//Console.WriteLine("Sending ping");


await Task.Delay(Timeout.Infinite);