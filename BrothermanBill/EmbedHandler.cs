using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrothermanBill
{
    public class EmbedHandler
    {
        private Color MusicColour => Color.DarkPurple;

        public async Task<Embed> CreateBasicEmbed(string title, string description, Color color)
        {
            var embed = await Task.Run(() => (new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithColor(color)
                .Build()));
            return embed;
        }

        public async Task<Embed> CreatePlayEmbed(string title, string artist, string url, string art)
        {
            var embed = await Task.Run(() => new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(artist)
                .WithColor(MusicColour)
                .WithUrl(url)
                .WithThumbnailUrl(art)
                .Build());
            return embed;
        }

        public async Task<Embed> CreateNowPlayingEmbed(string title, string artist, string url, string art, string duration)
        {
            var embed = await Task.Run(() => new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(artist)
                .WithColor(MusicColour)
                .WithUrl(url)
                .WithImageUrl(art)
                .WithFooter(duration)
                .Build());
            return embed;
        }

        public async Task<Embed> CreateQueueEmbed(string title, string artist, string url, string art, string duration)
        {
            var embed = await Task.Run(() => new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(artist)
                .WithColor(MusicColour)
                .WithUrl(url)
                .WithImageUrl(art)
                .WithFooter(duration)
                .Build());
            return embed;
        }

        public async Task<Embed> CreateErrorEmbed(string source, string error)
        {
            var embed = await Task.Run(() => new EmbedBuilder()
                .WithTitle($"ERROR OCCURED FROM - {source}")
                .WithDescription($"**Error Deaitls**: \n{error}")
                .WithColor(Color.DarkRed)
                .WithCurrentTimestamp().Build());
            return embed;
        }
    }
}
