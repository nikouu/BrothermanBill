using Discord;
using System.Text;

namespace BrothermanBill
{
    public class EmbedHandler
    {
        private Color MusicColour => Color.DarkPurple;

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
            const int maxQueueDisplay = 10;
            // Headroom for the trailing "and N more" line.
            var descriptionBudget = EmbedBuilder.MaxDescriptionLength - 100;

            // Materialise once. The source is a lazy projection over the player queue,
            // and the old Count()/ElementAt() loop walked it again on every iteration.
            var tracks = queue as IReadOnlyList<string> ?? queue.ToList();

            var stringBuilder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(nowPlaying))
            {
                stringBuilder.AppendLine($"**Now playing:** {nowPlaying}");
            }

            if (tracks.Count == 0)
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("*Queue empty*");
            }
            else
            {
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("**Up next:**");

                var limit = printFullQueue ? tracks.Count : Math.Min(maxQueueDisplay, tracks.Count);
                var shown = 0;

                for (var i = 0; i < limit; i++)
                {
                    var line = $"`{i + 1}.` {tracks[i]}";

                    // Over MaxDescriptionLength the EmbedBuilder throws, which would
                    // leave the deferred /queue command with no reply at all.
                    if (stringBuilder.Length + line.Length + Environment.NewLine.Length > descriptionBudget)
                    {
                        break;
                    }

                    stringBuilder.AppendLine(line);
                    shown++;
                }

                var remaining = tracks.Count - shown;
                if (remaining > 0)
                {
                    stringBuilder.AppendLine(shown < limit
                        ? $"*and {remaining} more... too many to display*"
                        : $"*and {remaining} more... use /queue full*");
                }
            }

            var description = stringBuilder.ToString();
            if (description.Length > EmbedBuilder.MaxDescriptionLength)
            {
                description = description[..EmbedBuilder.MaxDescriptionLength];
            }

            var embed = await Task.Run(() => new EmbedBuilder()
                .WithColor(MusicColour)
                .WithDescription(description)
                .Build());
            return embed;
        }
    }
}
