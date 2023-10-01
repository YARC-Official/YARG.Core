using System.Text;

namespace YARG.Core.IO
{
    public interface ITextReader
    {
        public static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
        public const char SPACE_ASCII = (char) 32;
        public static bool IsWhitespace(char character)
        {
            return character <= SPACE_ASCII;
        }

        private static readonly UTF32Encoding UTF32BE = new(true, false);

        public static bool Load(byte[] data, out ITextReader reader)
        {
            if (data[0] == 0xFF && data[1] == 0xFE)
            {
                if (data[2] != 0)
                    reader = new YARGTextReader<char, CharStringDecoder>(Encoding.Unicode.GetChars(data, 2, data.Length - 2), 0);
                else
                    reader = new YARGTextReader<char, CharStringDecoder>(Encoding.UTF32.GetChars(data, 3, data.Length - 3), 0);
                return false;
            }

            if (data[0] == 0xFE && data[1] == 0xFF)
            {
                if (data[2] != 0)
                    reader = new YARGTextReader<char, CharStringDecoder>(Encoding.BigEndianUnicode.GetChars(data, 2, data.Length - 2), 0);
                else
                    reader = new YARGTextReader<char, CharStringDecoder>(UTF32BE.GetChars(data, 3, data.Length - 3), 0);
                return false;
            }

            int position = data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            reader = new YARGTextReader<byte, ByteStringDecoder>(data, position);
            return true;
        }

        public bool ReadInt16(out short value);

        public bool ReadUInt16(out ushort value);

        public bool ReadInt32(out int value);

        public bool ReadUInt32(out uint value);

        public bool ReadInt64(out long value);

        public bool ReadUInt64(out ulong value);

        public bool ReadFloat(out float value);

        public bool ReadDouble(out double value);

        public bool ReadBoolean();

        public short ReadInt16();

        public ushort ReadUInt16();

        public int ReadInt32();

        public uint ReadUInt32();

        public long ReadInt64();

        public ulong ReadUInt64();

        public float ReadFloat();

        public double ReadDouble();

        public string ExtractText(bool checkForQuotes = true);
    }
}
