using System;
using System.Text;
using YARG.Core.Extensions;

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

        public ReadOnlySpan<TChar> Slice(int position, int length)
        {
            return new ReadOnlySpan<TChar>(Data, position, length);
        }

        private const char LAST_DIGIT_SIGNED = '7';
        private const char LAST_DIGIT_UNSIGNED = '5';

        private const short SHORT_MAX = short.MaxValue / 10;
        public bool ExtractInt16(out short value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            if (InternalExtractSigned(out long tmp, short.MaxValue, short.MinValue, SHORT_MAX, SkipWhitespace))
            {
                value = (short)tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const int INT_MAX = int.MaxValue / 10;
        public bool ExtractInt32(out int value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            if (InternalExtractSigned(out long tmp, int.MaxValue, int.MinValue, INT_MAX, SkipWhitespace))
            {
                value = (int)tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const long LONG_MAX = long.MaxValue / 10;
        public bool ExtractInt64(out long value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            return InternalExtractSigned(out value, long.MaxValue, long.MinValue, LONG_MAX, SkipWhitespace);
        }

        private const ushort USHORT_MAX = ushort.MaxValue / 10;
        public bool ExtractUInt16(out ushort value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            if (InternalExtractUnsigned(out ulong tmp, ushort.MaxValue, USHORT_MAX, SkipWhitespace))
            {
                value = (ushort)tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const uint UINT_MAX = uint.MaxValue / 10;
        public bool ExtractUInt32(out uint value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            if (InternalExtractUnsigned(out ulong tmp, uint.MaxValue, UINT_MAX, SkipWhitespace))
            {
                value = (uint)tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const ulong ULONG_MAX = ulong.MaxValue / 10;
        public bool ExtractUInt64(out ulong value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            return InternalExtractUnsigned(out value, ulong.MaxValue, ULONG_MAX, SkipWhitespace);
        }

        public bool ExtractFloat(out float value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            if (ExtractDouble(out double tmp, SkipWhitespace))
            {
                value = (float)tmp;
                return true;
            }
            value = default;
            return false;
        }

        public bool ExtractDouble(out double value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            value = 0;
            if (Position >= Next)
                return false;

            char ch = Data[Position].ToChar(null);
            double sign = ch == '-' ? -1 : 1;

            if (ch == '-' || ch == '+')
            {
                ++Position;
                if (Position == Next)
                    return false;
                ch = Data[Position].ToChar(null);
            }

            if (!ch.IsAsciiDigit() && ch != '.')
                return false;

            while (ch.IsAsciiDigit())
            {
                value *= 10;
                value += ch - '0';
                ++Position;
                if (Position < Next)
                    ch = Data[Position].ToChar(null);
                else
                    break;
            }

            if (ch == '.')
            {
                ++Position;
                if (Position < Next)
                {
                    double divisor = 1;
                    ch = Data[Position].ToChar(null);
                    while (ch.IsAsciiDigit())
                    {
                        divisor *= 10;
                        value += (ch - '0') / divisor;

                        ++Position;
                        if (Position < Next)
                            ch = Data[Position].ToChar(null);
                        else
                            break;
                    }
                }
            }

            value *= sign;
            SkipWhitespace(this);
            return true;
        }

        public bool ExtractBoolean()
        {
            return Data[Position].ToChar(null) switch
            {
                '0' => false,
                '1' => true,
                _ => Position + 4 <= Next &&
                    (Data[Position].ToChar(null).ToAsciiLower() == 't') &&
                    (Data[Position + 1].ToChar(null).ToAsciiLower() == 'r') &&
                    (Data[Position + 2].ToChar(null).ToAsciiLower() == 'u') &&
                    (Data[Position + 3].ToChar(null).ToAsciiLower() == 'e'),
            };
        }

        public short ExtractInt16(Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            if (ExtractInt16(out short value, SkipWhitespace))
                return value;
            throw new Exception("Data for Int16 not present");
        }

        public ushort ExtractUInt16(Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            if (ExtractUInt16(out ushort value, SkipWhitespace))
                return value;
            throw new Exception("Data for UInt16 not present");
        }

        public int ExtractInt32(Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            if (ExtractInt32(out int value, SkipWhitespace))
                return value;
            throw new Exception("Data for Int32 not present");
        }

        public uint ExtractUInt32(Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            if (ExtractUInt32(out uint value, SkipWhitespace))
                return value;
            throw new Exception("Data for UInt32 not present");
        }

        public long ExtractInt64(Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            if (ExtractInt64(out long value, SkipWhitespace))
                return value;
            throw new Exception("Data for Int64 not present");
        }

        public ulong ExtractUInt64(Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            if (ExtractUInt64(out ulong value, SkipWhitespace))
                return value;
            throw new Exception("Data for UInt64 not present");
        }

        public float ExtractFloat(Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            if (ExtractFloat(out float value, SkipWhitespace))
                return value;
            throw new Exception("Data for Float not present");
        }

        public double ExtractDouble(Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            if (ExtractDouble(out double value, SkipWhitespace))
                return value;
            throw new Exception("Data for Double not present");
        }

        private void SkipDigits()
        {
            while (Position < Next)
            {
                char ch = Data[Position].ToChar(null);
                if (!ch.IsAsciiDigit())
                    break;
                ++Position;
            }
        }

        private bool InternalExtractSigned(out long value, long hardMax, long hardMin, long softMax, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            value = 0;
            if (Position >= Next)
                return false;

            char ch = Data[Position].ToChar(null);
            long sign = 1;

            switch (ch)
            {
                case '-':
                    sign = -1;
                    goto case '+';
                case '+':
                    ++Position;
                    if (Position == Next)
                        return false;
                    ch = Data[Position].ToChar(null);
                    break;
            }

            if (!ch.IsAsciiDigit())
                return false;

            while (true)
            {
                value += ch - '0';

                ++Position;
                if (Position < Next)
                {
                    ch = Data[Position].ToChar(null);
                    if (ch.IsAsciiDigit())
                    {
                        if (value < softMax || value == softMax && ch <= LAST_DIGIT_SIGNED)
                        {
                            value *= 10;
                            continue;
                        }

                        value = sign == -1 ? hardMin : hardMax;
                        SkipDigits();
                        SkipWhitespace(this);
                        return true;
                    }
                }

                value *= sign;
                SkipWhitespace(this);
                return true;
            }
        }

        private bool InternalExtractUnsigned(out ulong value, ulong hardMax, ulong softMax, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
        {
            value = 0;
            if (Position >= Next)
                return false;

            char ch = Data[Position].ToChar(null);
            if (ch == '+')
            {
                ++Position;
                if (Position == Next)
                    return false;
                ch = Data[Position].ToChar(null);
            }

            if (!ch.IsAsciiDigit())
                return false;

            while (true)
            {
                value += (ulong)(ch - '0');

                ++Position;
                if (Position < Next)
                {
                    ch = Data[Position].ToChar(null);
                    if (ch.IsAsciiDigit())
                    {
                        if (value < softMax || value == softMax && ch <= LAST_DIGIT_UNSIGNED)
                        {
                            value *= 10;
                            continue;
                        }

                        value = hardMax;
                        SkipDigits();
                        SkipWhitespace(this);
                        return true;
                    }
                }

                SkipWhitespace(this);
                return true;
            }
        }
    }
}
