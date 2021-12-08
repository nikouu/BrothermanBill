using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
