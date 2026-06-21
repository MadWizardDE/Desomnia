using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace MadWizard.Desomnia.Configuration.Converter
{
    public sealed class EncodingConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
            => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string s)
                return Parse(s);

            return base.ConvertFrom(context, culture, value);
        }

        private static Encoding Parse(string name)
        {
            if (name.Equals("base64", StringComparison.InvariantCultureIgnoreCase))
                return Base64Encoding.Instance;

            // resolves the static instances (ASCII, UTF-8, UTF-16, ...) and any registered code page
            return Encoding.GetEncoding(name);
        }
    }
}
