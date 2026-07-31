using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MadWizard.Desomnia.Configuration
{
    [TypeConverter(typeof(DelayedActionInfoConverter))]
    public class DelayedActionInfo : ActionInfo
    {
        public DelayedActionInfo(string name, Arguments? args = null) : base(name, args) { }

        public DelayedActionInfo(Uri url) : base(url) { }

        public virtual bool HasDelay => false;
    }

    public partial class DelayedActionInfoConverter : ActionInfoConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type type)
        {
            return type == typeof(string);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string str)
            {
                // ORDER MATTERS (§6.4): the schedule suffix is split off FIRST — '+' is a
                // legal URL character, so the structure test must never see the suffix
                string head = ExtractSchedule(str, out TimeSpan? delay, out uint? times);

                Uri? url = TryParseURL(head, out Uri? parsed) ? parsed : null;

                Arguments? args = null;
                if (url == null)
                    head = ExtractArguments(head, out args);

                if (times is uint t)
                    return url is null ? new ThrottledActionInfo(head, args, t) : new ThrottledActionInfo(url, t);

                if (delay is TimeSpan d)
                    return url is null ? new ScheduledActionInfo(head, args, d) : new ScheduledActionInfo(url, d);

                return url is null ? new DelayedActionInfo(head, args) : new DelayedActionInfo(url);
            }

            return base.ConvertFrom(context, culture, value);
        }

        /// <summary>Validated last-'+' split (§6.4/§9.3): '+' is RESERVED as the schedule
        /// separator — the tail must parse as a duration or "Nx" count, and no further
        /// '+' may remain in the head (the old first-'+' split silently dropped extra
        /// segments). Literal plus inside a URL: percent-encode as %2B.</summary>
        private static string ExtractSchedule(string str, out TimeSpan? delay, out uint? times)
        {
            delay = null;
            times = null;

            int split = str.LastIndexOf('+');

            if (split < 0)
                return str;

            string head = str[..split];
            string tail = str[(split + 1)..];

            if (head.Contains('+'))
                throw new FormatException(
                    $"Multiple '+' separators in action '{str}' — '+' is reserved for the schedule suffix; escape a literal plus in URLs as %2B");

            if (TimesPattern().IsMatch(tail))
            {
                times = uint.Parse(tail[..^1]);
            }
            else
            {
                try
                {
                    delay = TimeSpan.Parse(ValueVariations.NormalizeTimeSpan(tail));
                }
                catch (FormatException)
                {
                    throw new FormatException(
                        $"Invalid schedule suffix '+{tail}' in action '{str}' — '+' is reserved for the schedule "
                        + "(e.g. +10min, +2x); escape a literal plus in URLs as %2B");
                }

                if (delay < TimeSpan.Zero)
                    throw new FormatException($"Negative delay in action '{str}'");
            }

            return head;
        }

        [GeneratedRegex(@"^\d+x$")]
        private static partial Regex TimesPattern();
    }
}
