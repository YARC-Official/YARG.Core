using System;
using System.Text;

namespace YARG.Core.IO
{
    public static class YARGTextContainer
    {
        public static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
        private static readonly UTF32Encoding UTF32BE = new(true, false);

        public static YARGTextContainer<byte>? TryLoadByteText(byte[] data)
        {
            if ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF))
                return null;

            int position = data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            return new YARGTextContainer<byte>(data, position);
        }

        public static YARGTextContainer<char> LoadCharText(byte[] data)
        {
            char[] charData;
            if (data[0] == 0xFF && data[1] == 0xFE)
            {
                if (data[2] != 0)
                    charData = Encoding.Unicode.GetChars(data, 2, data.Length - 2);
                else
                    charData = Encoding.UTF32.GetChars(data, 3, data.Length - 3);
            }
            else
            {
                if (data[2] != 0)
                    charData = Encoding.BigEndianUnicode.GetChars(data, 2, data.Length - 2);
                else
                    charData = UTF32BE.GetChars(data, 3, data.Length - 3);
            }
            return new YARGTextContainer<char>(charData, 0);
        }
    }

    public sealed class YARGTextContainer<TChar>
        where TChar : IConvertible
    {
        public readonly TChar[] Data;
        public readonly int Length;
        public int Position;
        public int Next;

        public TChar Current => Data[Position];
        public TChar this[int index] => Data[index];

        public YARGTextContainer(TChar[] data, int position)
        {
            Data = data;
            Length = data.Length;
            Position = position;
            Next = position;
        }

        public YARGTextContainer(YARGTextContainer<TChar> other)
        {
            Data = other.Data;
            Length = other.Length;
            Position = other.Position;
            Next = other.Next;
        }

        public bool IsCurrentCharacter(char cmp)
        {
            return Data[Position].ToChar(null).Equals(cmp);
        }

        public bool IsEndOfFile()
        {
            return Position >= Length;
        }

        public ReadOnlySpan<TChar> GetSpan(int position, int length)
        {
            return new ReadOnlySpan<TChar>(Data, position, length);
        }
    }
}
