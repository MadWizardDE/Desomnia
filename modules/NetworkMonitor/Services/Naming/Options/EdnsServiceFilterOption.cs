using MadWizard.Desomnia.Network.Filter.Rules;
using Makaretu.Dns;
using NetTools;
using System.Net;

namespace MadWizard.Desomnia.Network.Naming.Options
{
    /// <summary>
    /// Desomnia-private EDNS0 option (local/experimental-use code 65001) carrying the actionable host/range
    /// filter rules a Sleep Proxy should replicate for one service (identified by its DNS-SD type).
    /// </summary>
    public sealed class EdnsServiceFilterOption : EdnsServiceOption
    {
        public EdnsServiceFilterOption() => Type = (EdnsOptionType)0xFEED; // = 65261, IANA local/experimental-use range (65001–65534)

        public List<ServiceFilterEntry> Filters { get; init; } = [];

        public override void WriteData(WireWriter writer)
        {
            base.WriteData(writer);

            checked
            {
                writer.WriteByte((byte)Filters.Count);
            }

            foreach (var filter in Filters)
            {
                switch (filter)
                {
                    case StaticHostFilterEntry entry:
                        writer.WriteByte((byte)FilterType.StaticHost);
                        writer.WriteByte((byte)entry.Type);
                        writer.WriteAddress(entry.Address);
                        break;

                    case DynamicHostFilterEntry entry:
                        writer.WriteByte((byte)FilterType.DynamicHost);
                        writer.WriteByte((byte)entry.Type);
                        writer.WriteString(entry.Name);
                        break;

                    case StaticRangeFilterEntry entry:
                        writer.WriteByte((byte)FilterType.StaticRange);
                        writer.WriteByte((byte)entry.Type);
                        writer.WriteAddressRange(entry.Range);
                        break;

                    case LocalRangeFilterEntry entry:
                        writer.WriteByte((byte)FilterType.LocalRange);
                        writer.WriteByte((byte)entry.Type);
                        break;

                    default:
                        throw new NotSupportedException($"{filter.GetType().Name} cannot be serialised.");
                }
            }
        }

        public override void ReadData(WireReader reader, int length)
        {
            base.ReadData(reader, length);

            int count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                var kind = (FilterType)reader.ReadByte();
                var type = (FilterRuleType)reader.ReadByte();

                Filters.Add(kind switch
                {
                    FilterType.StaticHost     => new StaticHostFilterEntry    { Type = type, Address  = reader.ReadAddress() },
                    FilterType.DynamicHost    => new DynamicHostFilterEntry   { Type = type, Name     = reader.ReadString() },
                    FilterType.StaticRange    => new StaticRangeFilterEntry   { Type = type, Range    = reader.ReadAddressRange() },
                    FilterType.LocalRange     => new LocalRangeFilterEntry    { Type = type },

                    _ => throw new FormatException($"Unknown filter entry kind: {(byte)kind}"),
                });
            }
        }

        private enum FilterType : byte
        {
            StaticHost      = (byte) 'H',   // one IP address (binary)
            DynamicHost     = (byte) 'D',   // host name, resolved against the proxy's own network

            StaticRange     = (byte) 'R',   // a range: begin + end address (binary)
            LocalRange      = (byte) 'L',   // the local range, dynamically resolved by the proxy
        }
    }

    /// <summary>A single host/range filter rule on the wire. Subtypes carry the relevant fields; match with <c>is</c>.</summary>
    public abstract class ServiceFilterEntry
    {
        public required FilterRuleType Type { get; init; }
    }

    public sealed class StaticHostFilterEntry : ServiceFilterEntry
    {
        public required IPAddress Address { get; init; }
    }

    public sealed class DynamicHostFilterEntry : ServiceFilterEntry
    {
        public required string Name { get; init; }
    }

    public sealed class StaticRangeFilterEntry : ServiceFilterEntry
    {
        public required IPAddressRange Range { get; init; }
    }

    public sealed class LocalRangeFilterEntry : ServiceFilterEntry
    {

    }
}
