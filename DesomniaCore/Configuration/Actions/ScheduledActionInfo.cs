using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Desomnia.Configuration
{
    [TypeConverter(typeof(ScheduledActionInfoConverter))]
    public class ScheduledActionInfo : DelayedActionInfo
    {
        public ScheduledActionInfo(string name, Arguments? args, TimeSpan delay) : base(name, args)
        {
            Delay = delay;
        }

        public ScheduledActionInfo(Uri url, TimeSpan delay) : base(url)
        {
            Delay = delay;
        }

        public override bool HasDelay => Delay > TimeSpan.Zero;

        public TimeSpan Delay { get; }
    }

    public class ScheduledActionInfoConverter : DelayedActionInfoConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type type)
        {
            return type == typeof(string);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string s && !s.Contains('+'))
                value += $"+0s";

            if (base.ConvertFrom(context, culture, value) is ScheduledActionInfo action)
                return action;

            throw new FormatException($"Cannot convert {value} to ScheduledActionInfo");
        }

    }
}
