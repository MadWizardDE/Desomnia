using Makaretu.Dns;
using System.Net.NetworkInformation;

namespace MadWizard.Desomnia.Network.Naming.Options
{
    /// <summary>
    /// EDNS0 Owner option (code 4): identifies the sleeping host so the proxy can wake it — it carries the
    /// MAC address(es) for the magic packet and an optional SecureOn Wake-on-LAN password.
    /// </summary>
    public sealed class EdnsOwnerOption : EdnsOption
    {
        public EdnsOwnerOption() => Type = (EdnsOptionType)4;  // draft-cheshire-edns0-owner-option

        public byte Version { get; set; }
        public byte Sequence { get; set; }

        /// <summary>The host's primary (interface) MAC.</summary>
        public PhysicalAddress PrimaryMac { get; set; } = PhysicalAddress.None;

        /// <summary>The MAC to actually target with the magic packet, when it differs from <see cref="PrimaryMac"/>.</summary>
        public PhysicalAddress? WakeupMac { get; set; }

        /// <summary>Optional SecureOn Wake-on-LAN password (0, 4 or 6 bytes).</summary>
        public byte[]? Password { get; set; }

        /// <summary>The MAC a magic packet should be sent to.</summary>
        public PhysicalAddress WakeTarget => WakeupMac ?? PrimaryMac;

        public override void ReadData(WireReader reader, int length)
        {
            Version = reader.ReadByte();
            Sequence = reader.ReadByte();
            PrimaryMac = new PhysicalAddress(reader.ReadBytes(6));

            int remaining = length - 8; // consumed so far: version + sequence + 6-byte MAC

            if (remaining >= 6)
            {
                WakeupMac = new PhysicalAddress(reader.ReadBytes(6));
                remaining -= 6;
            }

            if (remaining > 0)
                Password = reader.ReadBytes(remaining);
        }

        public override void WriteData(WireWriter writer)
        {
            writer.WriteByte(Version);
            writer.WriteByte(Sequence);
            writer.WriteBytes(PrimaryMac.GetAddressBytes());

            if (WakeupMac is PhysicalAddress wakeup)
                writer.WriteBytes(wakeup.GetAddressBytes());

            if (Password is byte[] password)
                writer.WriteBytes(password);
        }
    }
}
