using System.Text.RegularExpressions;
using System.Xml;

namespace MadWizard.Desomnia.Configuration
{
    /// <summary>
    /// Normalizes user-friendly configuration value variations into the formats
    /// expected by the standard .NET type converters. Applied type-aware by the
    /// <see cref="Binding.StrictConfigurationBinder"/> (formerly rewritten blindly
    /// on the XML attribute level by the configuration provider).
    /// </summary>
    public static partial class ValueVariations
    {
        /// <summary>
        /// Accepts "90s", "5min", "2h30min", "7 days", "500ms" as well as
        /// ISO 8601 durations ("PT5M") and returns the constant TimeSpan format.
        /// Unrecognized values are returned unchanged.
        /// </summary>
        public static string NormalizeTimeSpan(string value)
        {
            var trimmed = WhitespacePattern().Replace(value, "");

            if (ISO8601TimeSpanPattern().IsMatch(trimmed))
            {
                TimeSpan time = XmlConvert.ToTimeSpan(trimmed);

                return time.ToString();
            }

            else if (TimeSpanPattern().Match(trimmed) is Match match && match.Success)
            {
                TimeSpan time = TimeSpan.Zero;
                if (match.Groups.TryGetValue("days", out var days) && days.Success)
                    time += TimeSpan.FromDays(int.Parse(days.Value));
                if (match.Groups.TryGetValue("hours", out var hours) && hours.Success)
                    time += TimeSpan.FromHours(int.Parse(hours.Value));
                if (match.Groups.TryGetValue("minutes", out var minutes) && minutes.Success)
                    time += TimeSpan.FromMinutes(int.Parse(minutes.Value));
                if (match.Groups.TryGetValue("seconds", out var seconds) && seconds.Success)
                    time += TimeSpan.FromSeconds(int.Parse(seconds.Value));
                if (match.Groups.TryGetValue("milliseconds", out var milliseconds) && milliseconds.Success)
                    time += TimeSpan.FromMilliseconds(int.Parse(milliseconds.Value));

                return time.ToString();
            }

            return value;
        }

        /// <summary>
        /// Accepts "Host|Service" (pipes as flag separators) and "magic-packet"
        /// (dashes inside member names) and returns the comma-separated format
        /// understood by <see cref="System.ComponentModel.EnumConverter"/>.
        /// Purely numeric values are returned unchanged.
        /// </summary>
        public static string NormalizeEnum(string value)
        {
            if (!value.Any(char.IsLetter))
                return value;

            var parts = value.Split(['|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return string.Join(',', parts.Select(part => part.Replace("-", "")));
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespacePattern();
        [GeneratedRegex(@"^P(?=\d|T\d)(\d+Y)?(\d+M)?(\d+D)?(T(\d+H)?(\d+M)?(\d+S)?)?$")]
        private static partial Regex ISO8601TimeSpanPattern();
        [GeneratedRegex(@"^(?=.*\d+(?:days|day|d|h|min|s|ms))(?:(?<days>\d+)(?:days|day|d))?(?:(?<hours>\d+)h)?(?:(?<minutes>\d+)min)?(?:(?<seconds>\d+)s)?(?:(?<milliseconds>\d+)ms)?$")]
        private static partial Regex TimeSpanPattern();
    }
}
