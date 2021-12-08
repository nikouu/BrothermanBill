using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrothermanBill.Modules
{
    public class MiscModule : ModuleBase<SocketCommandContext>
    {
        [Command("kkona")]
        [Summary("Echoes a message.")]
        public Task KkonaAsync() => ReplyAsync("KKona brother <:Kkona:917645359633813545>");

        [Command("cum")]
        public Task CumAsync() => ReplyAsync("Cum");

    }
}