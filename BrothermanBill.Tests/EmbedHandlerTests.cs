using Discord;

namespace BrothermanBill.Tests
{
    public class QueueEmbedTests
    {
        private readonly EmbedHandler _embedHandler = new();

        private static List<string> Tracks(int count, int titleLength = 60) =>
            Enumerable.Range(1, count)
                .Select(i => $"Track {i} " + new string('x', titleLength))
                .ToList();

        [Fact]
        public void ShowsTenTracksByDefaultAndCountsTheRest()
        {
            var embed = _embedHandler.CreateQueueEmbed("Now Playing Song", Tracks(50), printFullQueue: false);

            Assert.Contains("`10.`", embed.Description);
            Assert.DoesNotContain("`11.`", embed.Description);
            Assert.Contains("and 40 more", embed.Description);
        }

        [Fact]
        public void FullQueueStaysWithinTheDescriptionLimit()
        {
            var embed = _embedHandler.CreateQueueEmbed("Now Playing Song", Tracks(400), printFullQueue: true);

            Assert.True(embed.Description.Length <= EmbedBuilder.MaxDescriptionLength);
            Assert.Contains("too many to display", embed.Description);
        }

        [Fact]
        public void AnOverlongTitleCannotPushTheDescriptionOverTheLimit()
        {
            var embed = _embedHandler.CreateQueueEmbed(new string('X', 5000), Tracks(400), printFullQueue: true);

            Assert.True(embed.Description.Length <= EmbedBuilder.MaxDescriptionLength);
        }

        [Fact]
        public void ReportsAnEmptyQueue()
        {
            var embed = _embedHandler.CreateQueueEmbed("Now Playing Song", Array.Empty<string>());

            Assert.Contains("Queue empty", embed.Description);
        }

        [Fact]
        public void OmitsTheNowPlayingLineWhenNothingIsPlaying()
        {
            var embed = _embedHandler.CreateQueueEmbed("", Tracks(3));

            Assert.DoesNotContain("Now playing", embed.Description);
            Assert.Contains("`1.`", embed.Description);
        }

        [Fact]
        public void ShowsEveryTrackWhenTheQueueFitsComfortably()
        {
            var embed = _embedHandler.CreateQueueEmbed("Now Playing Song", Tracks(25), printFullQueue: true);

            Assert.Contains("`25.`", embed.Description);
            Assert.DoesNotContain("more...", embed.Description);
        }
    }
}
