using BrothermanBill.Services;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Victoria;

namespace BrothermanBill.Modules
{
    public class MemeModule : ModuleBase<SocketCommandContext>
    {
        private readonly LavaNode _lavaNode;
        private readonly MemeService _memeService;
        public MemeModule(LavaNode lavaNode, MemeService memeService)
        {
            _lavaNode = lavaNode;
            _memeService = memeService;
        }

        [Command("meme")]
        public async Task PlayRandomMeme()
        {
            var memeUrl = await _memeService.GetRandomMeme();
            
        }
    }
}
