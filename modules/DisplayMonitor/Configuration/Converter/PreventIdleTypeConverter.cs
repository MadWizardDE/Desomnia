using System.ComponentModel;
using System.Globalization;

namespace MadWizard.Desomnia.Display.Configuration.Converter
{
    /// <summary>
    /// Parses the three enum names plus the boolean aliases <c>true</c> (= <see cref="PreventIdleType.Always"/>)
    /// and <c>false</c> (= <see cref="PreventIdleType.Never"/>).
    /// </summary>
    public class PreventIdleTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type type)
        {
            return type == typeof(string);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string str)
            {
                return str.Trim().ToLowerInvariant() switch
                {
                    "never" or "false" => PreventIdleType.Never,
                    "always" or "true" => PreventIdleType.Always,
                    "enabled" => PreventIdleType.Enabled,
                    _ => throw new FormatException($"'{str}' is not a valid preventIdle value (never, always, enabled)."),
                };
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
