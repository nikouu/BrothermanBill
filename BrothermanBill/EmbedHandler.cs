using Discord;
using System.Text;

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

        public async Task<Embed> CreateQueueEmbed(string nowPlaying, IEnumerable<string> queue, bool printFullQueue = false)
        {
            var maxQueueDisplay = 10;
            var stringBuilder = new StringBuilder();

            if (nowPlaying != null)
            {
                stringBuilder.AppendLine($"**Now playing:** {nowPlaying}");
            }

            if (queue.Any())
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("**Up next:**");

                for (int i = 0; i < queue.Count() && (i < maxQueueDisplay || printFullQueue); i++)
                {
                    stringBuilder.AppendLine($"`{i + 1}.` {queue.ElementAt(i)}");
                }

                if (!printFullQueue && queue.Count() > maxQueueDisplay)
                {
                    stringBuilder.AppendLine($"*and {queue.Count() - maxQueueDisplay} more... use /queue full*");
                }
            }
            else
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("*Queue empty*");
            }

            var embed = await Task.Run(() => new EmbedBuilder()
                .WithColor(MusicColour)
                .WithDescription(stringBuilder.ToString())
                .Build());
            return embed;
        }
    }
}
