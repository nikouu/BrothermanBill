using BrothermanBill;
using BrothermanBill.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Lavalink4NET.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Always load user secrets (not just in Development)
builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddSingleton(sp => new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildVoiceStates | GatewayIntents.MessageContent
}));

builder.Services.AddSingleton(new CommandService(new CommandServiceConfig
{
    CaseSensitiveCommands = false,
    LogLevel = LogSeverity.Debug,
}));

builder.Services.AddSingleton<CommandHandlerService>();
builder.Services.AddSingleton<AudioService>();
builder.Services.AddSingleton<MemeService>();
builder.Services.AddSingleton<EmbedHandler>();
builder.Services.AddSingleton<StatusService>();
builder.Services.AddSingleton<UptimeService>();
builder.Services.AddHttpClient();

builder.Services.AddHostedService<BotHostedService>();
builder.Services.AddLavalink();
builder.Services.ConfigureLavalink(options =>
{
    options.BaseAddress = new Uri("http://localhost:2333");
    options.Passphrase = "youshallnotpass";
});

var host = builder.Build();
await host.RunAsync();