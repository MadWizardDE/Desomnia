using MadWizard.Desomnia.Display.Manager;
using System.ComponentModel;
using System.Globalization;

namespace MadWizard.Desomnia.Display.Configuration.Converter
{
    public class ResolutionConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type type)
        {
            return type == typeof(string);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            return value is string str ? Parse(str) : null;
        }

        private static Resolution Parse(string text)
        {
            string[] parts = text.Split(['x', 'X', '×'], 2);

            return new Resolution(int.Parse(parts[0].Trim()), int.Parse(parts[1].Trim()));
        }
    }
}
