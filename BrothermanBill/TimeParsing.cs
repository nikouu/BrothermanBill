using System.Globalization;
using System.Web;

namespace BrothermanBill
{
    /// <summary>
    /// Parsing for the time values users and YouTube URLs supply. Pure functions, kept
    /// out of the command module so they can be tested without a Discord context.
    /// </summary>
    internal static class TimeParsing
    {
        private static readonly string[] TimeFormats =
        {
            @"m\:ss",
            @"mm\:ss",
            @"h\:mm\:ss"
        };

        /// <summary>
        /// Parses a seek time. A bare number is total seconds; otherwise "m:ss",
        /// "mm:ss" or "h:mm:ss".
        /// </summary>
        public static bool TryParseSeekTime(string time, out TimeSpan duration)
        {
            // Total seconds first. The "ss" specifier caps at 59, so anything larger
            // ("/seek 300") matches no format and would otherwise be rejected.
            if (int.TryParse(time, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            {
                duration = TimeSpan.FromSeconds(seconds);
                return true;
            }

            return TimeSpan.TryParseExact(time, TimeFormats, CultureInfo.InvariantCulture, out duration);
        }

        /// <summary>Reads the start position from a URL's "t" parameter.</summary>
        public static TimeSpan GetUrlParameterTime(string searchQuery)
        {
            if (!Uri.TryCreate(searchQuery, UriKind.Absolute, out var uri))
            {
                return TimeSpan.Zero;
            }

            var queryString = HttpUtility.ParseQueryString(uri.Query);
            var value = queryString.Get("t");
            if (string.IsNullOrEmpty(value))
            {
                return TimeSpan.Zero;
            }

            return ParseYouTubeTime(value);
        }

        /// <summary>
        /// Parses a YouTube "t" parameter: plain seconds ("90") or the unit form
        /// ("90s", "1m30s", "1h2m3s"). Returns <see cref="TimeSpan.Zero"/> for anything
        /// it does not recognise.
        /// </summary>
        public static TimeSpan ParseYouTubeTime(string value)
        {
            const long maxSeconds = 24 * 60 * 60;

            long totalSeconds = 0;
            long current = 0;
            var sawUnit = false;

            foreach (var character in value)
            {
                if (char.IsAsciiDigit(character))
                {
                    current = (current * 10) + (character - '0');
                    if (current > maxSeconds)
                    {
                        return TimeSpan.Zero;
                    }

                    continue;
                }

                var multiplier = char.ToLowerInvariant(character) switch
                {
                    'h' => 3600,
                    'm' => 60,
                    's' => 1,
                    _ => 0
                };

                if (multiplier == 0)
                {
                    return TimeSpan.Zero;
                }

                totalSeconds += current * multiplier;
                current = 0;
                sawUnit = true;
            }

            // A trailing number with no unit is only valid when there were no units at
            // all ("90"). Mixed forms like "1m30" are not YouTube syntax.
            if (sawUnit && current != 0)
            {
                return TimeSpan.Zero;
            }

            totalSeconds += sawUnit ? 0 : current;

            return totalSeconds is > 0 and <= maxSeconds
                ? TimeSpan.FromSeconds(totalSeconds)
                : TimeSpan.Zero;
        }
    }
}
