using BrothermanBill;

namespace BrothermanBill.Tests
{
    public class SeekTimeTests
    {
        [Theory]
        // Bare numbers are total seconds. Single digits and values past 59 were both
        // rejected before: "s" was read as a standard specifier, and "ss" caps at 59.
        [InlineData("0", 0)]
        [InlineData("5", 5)]
        [InlineData("05", 5)]
        [InlineData("30", 30)]
        [InlineData("59", 59)]
        [InlineData("60", 60)]
        [InlineData("90", 90)]
        [InlineData("300", 300)]
        [InlineData("3600", 3600)]
        // Colon forms
        [InlineData("1:30", 90)]
        [InlineData("01:30", 90)]
        [InlineData("10:30", 630)]
        [InlineData("1:30:00", 5400)]
        [InlineData("10:00:00", 36000)]
        public void ParsesValidSeekTimes(string input, int expectedSeconds)
        {
            Assert.True(TimeParsing.TryParseSeekTime(input, out var duration));
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), duration);
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("1:2:3:4")]
        [InlineData("99999999999")]   // overflows int, must not wrap
        [InlineData(" 30")]
        [InlineData("30 ")]
        public void RejectsInvalidSeekTimes(string input)
        {
            Assert.False(TimeParsing.TryParseSeekTime(input, out _));
        }

        [Fact]
        public void SeekTimeParsingDoesNotDependOnCurrentCulture()
        {
            var original = Thread.CurrentThread.CurrentCulture;
            try
            {
                foreach (var name in new[] { "en-US", "de-DE", "fi-FI", "ar-SA" })
                {
                    Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(name);

                    Assert.True(TimeParsing.TryParseSeekTime("1:30:00", out var duration));
                    Assert.Equal(TimeSpan.FromSeconds(5400), duration);
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }

    public class YouTubeTimeTests
    {
        [Theory]
        [InlineData("https://www.youtube.com/watch?v=abc&t=90", 90)]
        [InlineData("https://www.youtube.com/watch?v=abc&t=90s", 90)]
        [InlineData("https://www.youtube.com/watch?v=abc&t=1m30s", 90)]
        [InlineData("https://youtu.be/abc?t=1h2m3s", 3723)]
        [InlineData("https://youtu.be/abc?t=2m", 120)]
        [InlineData("https://youtu.be/abc?t=1h", 3600)]
        public void ReadsStartPositionFromUrl(string url, int expectedSeconds)
        {
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), TimeParsing.GetUrlParameterTime(url));
        }

        [Theory]
        [InlineData("https://www.youtube.com/watch?v=abc")]          // no t parameter
        [InlineData("https://www.youtube.com/watch?v=abc&t=0")]
        [InlineData("https://www.youtube.com/watch?v=abc&t=1m30")]   // trailing unitless number
        [InlineData("https://www.youtube.com/watch?v=abc&t=abc")]
        [InlineData("not a url at all")]
        public void ReturnsZeroWhenThereIsNoUsableStartPosition(string url)
        {
            Assert.Equal(TimeSpan.Zero, TimeParsing.GetUrlParameterTime(url));
        }

        [Theory]
        // A negative or absurd t= previously reached SeekAsync unchecked.
        [InlineData("https://www.youtube.com/watch?v=abc&t=-30")]
        [InlineData("https://www.youtube.com/watch?v=abc&t=999999999")]
        public void RejectsOutOfRangeStartPositions(string url)
        {
            Assert.Equal(TimeSpan.Zero, TimeParsing.GetUrlParameterTime(url));
        }
    }
}
