using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Song.Deserialization
{
    public interface ITXTReader
    {
        public static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
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

        public const char SPACE_ASCII = (char) 32;
        public static bool IsWhitespace(char character)
        {
            return character <= SPACE_ASCII;
        }
    }
}
