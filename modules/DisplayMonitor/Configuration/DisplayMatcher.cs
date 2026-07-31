using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MadWizard.Desomnia.Configuration
{
    [TypeConverter(typeof(DisplayMatcherConverter))]
    public class DisplayMatcher
    {
        private List<string>? _patterns;

        private DisplayMatcher()
        {

        }

        public DisplayMatcher(string pattern)
        {
            _patterns = [pattern];
        }

        public bool IsMatchingAny => _patterns == null;
        public bool IsMatchingNone => _patterns != null && _patterns.Count == 0;

        public bool Match(string name)
        {
            if (IsMatchingAny)
                return true;

            foreach (string pattern in _patterns!)
                if (Regex.IsMatch(name, pattern))
                    return true;

            return false;
        }

        public static DisplayMatcher None => new() { _patterns = [] };
        public static DisplayMatcher Any => new() { _patterns = null };
    }

    public class DisplayMatcherConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type type)
        {
            return type == typeof(string);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string str)
            {
                if (str == "*" || str == "true")
                    return DisplayMatcher.Any;
                else if (str == "false")
                    return DisplayMatcher.None;
                else
                    return new DisplayMatcher(str);
            }

            return null;
        }
    }
}
