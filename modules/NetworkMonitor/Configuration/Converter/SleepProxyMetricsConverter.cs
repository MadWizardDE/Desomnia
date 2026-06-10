using MadWizard.Desomnia.Network.SleepProxy;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MadWizard.Desomnia.Network.Configuration.Converter
{
    public partial class SleepProxyMetricsConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type type) => type == typeof(string);

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string str)
            {
                return ParseMetrics(str);
            }

            return null;
        }

        private static SleepProxyMetrics ParseMetrics(string str)
        {
            switch (string.Concat(str.Where(c => !char.IsWhiteSpace(c))).ToLower())
            {
                case "best":    return SleepProxyMetrics.Best;
                case "average": return SleepProxyMetrics.Average;
                case "worst":   return SleepProxyMetrics.Worst;

                default: if (SleepProxyMetricsPattern().Match(str) is Match match && match.Success)
                    return new SleepProxyMetrics
                    {
                        Intent          = ParseMetricPart(match.Groups["Intent"]),
                        Portability     = ParseMetricPart(match.Groups["Portability"]),
                        MarginalPower   = ParseMetricPart(match.Groups["MarginalPower"]),
                        TotalPower      = ParseMetricPart(match.Groups["TotalPower"])
                    };

                else throw new FormatException("Invalid sleep proxy metrics format");
            }
        }

        private static byte ParseMetricPart(Group group)
        {
            if (!byte.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out byte value))
                throw new FormatException("Invalid sleep proxy metrics value");
            if (value is < 10 or > 99)
                throw new FormatException("Invalid sleep proxy metrics value; must be in the range 10-99");

            return value;
        }

        [GeneratedRegex(@"^(?<Intent>\d{2})-(?<Portability>\d{2})-(?<MarginalPower>\d{2})-(?<TotalPower>\d{2})$", RegexOptions.CultureInvariant)]
        private static partial Regex SleepProxyMetricsPattern();
    }
}
