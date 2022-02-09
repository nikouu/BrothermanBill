using Discord;
using System.Text;

namespace BrothermanBill
{
    public class EmbedHandler
    {
        private Color MusicColour => Color.DarkPurple;

        private readonly string[] QueueEmojis = new[] { "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟" };

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
                stringBuilder.AppendLine($"🎵 {nowPlaying}");
            }

            if (queue.Any())
            {              
                for (int i = 0; i < queue.Count() && (i < maxQueueDisplay || printFullQueue); i++)
                {
                    stringBuilder.AppendLine($"{QueueEmojis.ElementAtOrDefault(i, "#️⃣")} {queue.ElementAt(i)}");
                }

                if (!printFullQueue && queue.Count() > maxQueueDisplay)
                {
                    stringBuilder.AppendLine($"#️⃣ *and {queue.Count() - maxQueueDisplay} more... see all with !queue full*");
                }
            }
            else
            {
                stringBuilder.AppendLine($"*️⃣ *Queue empty*");
            }

            var embed = await Task.Run(() => new EmbedBuilder()
                .WithColor(MusicColour)
                .WithDescription(stringBuilder.ToString())
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
