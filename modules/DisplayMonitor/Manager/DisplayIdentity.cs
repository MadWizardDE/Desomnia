namespace MadWizard.Desomnia.Display.Manager
{
    /// <summary>
    /// EDID-derived identity — the common denominator across Windows (registry EDID),
    /// macOS (AppleCLCD2 ProductAttributes) and Linux (sysfs edid).
    ///
    /// Value equality over all fields is used to recognize a display that reconnects
    /// (HDMI hot-plug re-negotiation pulses, AVR link renegotiation). Serial numbers are
    /// NOT trustworthy on their own — cheap panels ship garbage like 0x01010101 — which is
    /// why the whole tuple, not the serial, is the identity.
    /// </summary>
    public record DisplayIdentity
    {
        /// <summary>PnP vendor code ("GSM", "DEL") — or an OUI string like "00-10-fa" for Apple built-ins.</summary>
        public required string VendorId { get; init; }

        /// <summary>EDID product code (16 bit); Apple built-in panels report wider opaque values.</summary>
        public required ulong ProductCode { get; init; }

        /// <summary>Model name from the EDID display descriptor, e.g. "LG HDR 4K".</summary>
        public string? Name { get; init; }

        public uint? SerialNumber { get; init; }
        public string? SerialString { get; init; }

        public byte? WeekOfManufacture { get; init; }
        public ushort? YearOfManufacture { get; init; }

        public override string ToString()
        {
            string str = $"{VendorId}:{ProductCode:X4}";

            if (Name != null)
                str += $" \"{Name}\"";

            if ((SerialString ?? SerialNumber?.ToString()) is string serial)
                str += $" #{serial}";

            return str;
        }
    }

    /// <summary>
    /// Minimal EDID 1.x parser — extracts the identity and selector properties DisplayMonitor
    /// builds on. Platform-independent: Windows reads the blob from the monitor devnode's
    /// registry key, Linux (someday) from sysfs. (Apple Silicon does not expose raw EDID;
    /// the macOS manager reads the pre-parsed ProductAttributes instead.)
    /// </summary>
    public class EDID(byte[] bytes)
    {
        public byte[] Raw => bytes;

        public bool HasValidHeader =>
            bytes.Length >= 128 &&
            bytes[0] == 0x00 && bytes[1] == 0xFF && bytes[2] == 0xFF && bytes[3] == 0xFF &&
            bytes[4] == 0xFF && bytes[5] == 0xFF && bytes[6] == 0xFF && bytes[7] == 0x00;

        /// <summary>Three-letter PnP vendor code, e.g. "DEL", "GSM".</summary>
        public string VendorId
        {
            get
            {
                int raw = bytes[8] << 8 | bytes[9]; // big-endian

                return new string([
                    (char)('A' - 1 + (raw >> 10 & 0x1F)),
                    (char)('A' - 1 + (raw >> 5 & 0x1F)),
                    (char)('A' - 1 + (raw & 0x1F)),
                ]);
            }
        }

        public ushort ProductCode => (ushort)(bytes[10] | bytes[11] << 8);
        public uint SerialNumber => (uint)(bytes[12] | bytes[13] << 8 | bytes[14] << 16 | bytes[15] << 24);

        public byte WeekOfManufacture => bytes[16];
        public ushort YearOfManufacture => (ushort)(1990 + bytes[17]);

        public bool IsDigitalInput => (bytes[20] & 0x80) != 0;

        /// <summary>Display name from the 0xFC descriptor, e.g. "DELL U2720Q".</summary>
        public string? DisplayName => ReadDescriptorText(0xFC);

        /// <summary>Serial string from the 0xFF descriptor (often more meaningful than the numeric serial).</summary>
        public string? SerialString => ReadDescriptorText(0xFF);

        /// <summary>Native resolution from the preferred detailed timing descriptor.</summary>
        public Resolution? NativeResolution
        {
            get
            {
                // The first 18-byte descriptor block (offset 54) holds the preferred timing,
                // recognizable by a non-zero pixel clock.
                if (bytes.Length < 72 || bytes[54] == 0 && bytes[55] == 0)
                    return null;

                int width = bytes[56] | (bytes[58] & 0xF0) << 4;
                int height = bytes[59] | (bytes[61] & 0xF0) << 4;

                return new Resolution(width, height);
            }
        }

        public DisplayIdentity ToIdentity() => new()
        {
            VendorId = VendorId,
            ProductCode = ProductCode,
            Name = DisplayName,
            SerialNumber = SerialNumber != 0 ? SerialNumber : null, // 0 = unset per spec
            SerialString = SerialString,
            WeekOfManufacture = WeekOfManufacture is > 0 and <= 54 ? WeekOfManufacture : null,
            YearOfManufacture = YearOfManufacture,
        };

        private string? ReadDescriptorText(byte tag)
        {
            for (int offset = 54; offset <= 108; offset += 18)
            {
                if (bytes.Length < offset + 18)
                    break;

                // display descriptors (as opposed to timings) start with 0x00 0x00 0x00 <tag>
                if (bytes[offset] == 0 && bytes[offset + 1] == 0 && bytes[offset + 2] == 0 && bytes[offset + 3] == tag)
                {
                    string text = System.Text.Encoding.ASCII.GetString(bytes, offset + 5, 13);

                    int end = text.IndexOf('\n');

                    return (end >= 0 ? text[..end] : text).Trim();
                }
            }

            return null;
        }
    }
}
