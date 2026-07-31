using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Desomnia.Configuration
{
    [TypeConverter(typeof(ThrottledActionInfoConverter))]
    public class ThrottledActionInfo : DelayedActionInfo
    {
        public ThrottledActionInfo(string name, Arguments? args, uint times) : base(name, args)
        {
            Times = times;
        }

        public ThrottledActionInfo(Uri url, uint times) : base(url)
        {
            Times = times;
        }

        public override bool HasDelay => Times > 0;

        public uint Times { get; }
    }

    public class ThrottledActionInfoConverter : DelayedActionInfoConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type type)
        {
            return type == typeof(string);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string s && !s.Contains('+'))
                value += $"+0x";

            if (base.ConvertFrom(context, culture, value) is ThrottledActionInfo action)
                return action;

            throw new FormatException($"Cannot convert {value} to ThrottledActionInfo");
        }
    }
}
