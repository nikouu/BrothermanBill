using BrothermanBill.Services;
using CliWrap;
using Discord;
using Discord.Audio;
using Discord.Audio.Streams;
using Discord.Commands;
using Discord.WebSocket;
using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Pipes;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipelines;

namespace BrothermanBill.Modules
{
    public class GeneralModule : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        public Task PingAsync()
            => ReplyAsync($"Current Ping {Context.Client.Latency}ms");

        [Command("userinfo")]
        public async Task UserInfoAsync(IUser user = null)
        {
            user ??= Context.User;
            await ReplyAsync(user.ToString());
        }

        [Command("setgame")]
        public async Task GameAsync([Remainder] string setgame)  // [Remainder] takes all arguments as one
        {
            await Context.Client.SetGameAsync(setgame);
            await ReplyAsync("Set game succeeded");
        }

        [Command("kkona")]
        [Summary("Echoes a message.")]
        public Task KkonaAsync() => ReplyAsync("KKona brother <:Kkona:917645359633813545>");

        [Command("cum")]
        public Task CumAsync() => ReplyAsync("Cum");
    }
}
