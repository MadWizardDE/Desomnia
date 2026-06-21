using System.Text;

namespace MadWizard.Desomnia.Configuration.Converter
{
    /// <summary>
    /// A pseudo <see cref="Encoding"/> that treats the textual representation as Base64.
    /// <see cref="Encoding.GetBytes(string)"/> decodes the Base64 string into the raw bytes
    /// it represents (the reverse of <see cref="Encoding.GetString(byte[])"/>), allowing a
    /// password to be supplied as raw bytes rather than printable text.
    /// </summary>
    public sealed class Base64Encoding : Encoding
    {
        public static readonly Base64Encoding Instance = new();

        public override string EncodingName => "base64";

        public override int GetByteCount(char[] chars, int index, int count)
            => System.Convert.FromBase64CharArray(chars, index, count).Length;

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            byte[] decoded = System.Convert.FromBase64CharArray(chars, charIndex, charCount);
            Buffer.BlockCopy(decoded, 0, bytes, byteIndex, decoded.Length);
            return decoded.Length;
        }

        public override int GetMaxByteCount(int charCount) => charCount / 4 * 3 + 3;

        public override int GetCharCount(byte[] bytes, int index, int count)
            => (count + 2) / 3 * 4;

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            string base64 = System.Convert.ToBase64String(bytes, byteIndex, byteCount);
            base64.CopyTo(0, chars, charIndex, base64.Length);
            return base64.Length;
        }

        public override int GetMaxCharCount(int byteCount) => (byteCount + 2) / 3 * 4;
    }
}
