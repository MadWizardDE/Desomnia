using MadWizard.Desomnia.Configuration;
using System.ComponentModel;
using System.Globalization;

namespace MadWizard.Desomnia.Process.Configuration
{
    [TypeConverter(typeof(CPUThresholdConverter))]
    public readonly struct CPUThreshold
    {
        public double? RelativeUsage { get; private init; }
        public TimeSpan? AbsoluteTime { get; private init; }

        public CPUThreshold(double usage)
        {
            RelativeUsage = usage;
        }

        public CPUThreshold(TimeSpan time)
        {
            AbsoluteTime = time;
        }
    }

    public partial class CPUThresholdConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type type)
        {
            return type == typeof(string);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string str)
            {
                return TryParseFormat(str);
            }

            return null;
        }

        private static CPUThreshold TryParseFormat(string str)
        {
            str = string.Concat(str.Where(c => !char.IsWhiteSpace(c))); // remove all whitespace

            if (TimeSpan.TryParse(ValueVariations.NormalizeTimeSpan(str), CultureInfo.InvariantCulture, out var time))
            {
                return new CPUThreshold(time);
            }
            else if (str.EndsWith('%') && double.TryParse(str[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var usage))
            {
                return new CPUThreshold(usage / 100.0);
            }

            throw new FormatException("Invalid CPU threshold format");
        }
    }
}
