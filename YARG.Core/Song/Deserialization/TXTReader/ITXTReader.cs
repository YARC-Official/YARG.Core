using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Song.Deserialization
{
    public interface ITXTReader
    {
        public static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
        public bool ReadBoolean(ref bool value);

        public bool ReadInt16(ref short value);

        public bool ReadUInt16(ref ushort value);

        public bool ReadInt32(ref int value);

        public bool ReadUInt32(ref uint value);

        public bool ReadInt64(ref long value);

        public bool ReadUInt64(ref ulong value);

        public bool ReadFloat(ref float value);

        public bool ReadDouble(ref double value);

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
