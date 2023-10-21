using System;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public static class YARGTextContainer
    {
        public static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
    }

    public abstract class YARGTextContainer<TChar>
        where TChar : unmanaged, IConvertible
    {
        public readonly TChar[] Data;
        public readonly int Length;
        public int Position;
        public int Next;

        protected YARGTextContainer(TChar[] data, int position)
        {
            Data = data;
            Length = data.Length;
            Position = position;
            Next = position;
        }

        protected YARGTextContainer(YARGTextContainer<TChar> other)
        {
            Data = other.Data;
            Length = other.Length;
            Position = other.Position;
            Next = other.Next;
        }

        public abstract char SkipWhitespace();

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
        public bool ExtractInt16(out short value)
        {
            bool result = InternalExtractSigned(out long tmp, short.MaxValue, short.MinValue, SHORT_MAX);
            value = (short)tmp;
            return result;
        }

        private const int INT_MAX = int.MaxValue / 10;
        public bool ExtractInt32(out int value)
        {
            bool result = InternalExtractSigned(out long tmp, int.MaxValue, int.MinValue, INT_MAX);
            value = (int)tmp;
            return result;
        }

        private const long LONG_MAX = long.MaxValue / 10;
        public bool ExtractInt64(out long value)
        {
            return InternalExtractSigned(out value, long.MaxValue, long.MinValue, LONG_MAX);
        }

        private const ushort USHORT_MAX = ushort.MaxValue / 10;
        public bool ExtractUInt16(out ushort value)
        {
            bool result = InternalExtractUnsigned(out ulong tmp, ushort.MaxValue, USHORT_MAX);
            value = (ushort) tmp;
            return result;
        }

        private const uint UINT_MAX = uint.MaxValue / 10;
        public bool ExtractUInt32(out uint value)
        {
            bool result = InternalExtractUnsigned(out ulong tmp, uint.MaxValue, UINT_MAX);
            value = (uint) tmp;
            return result;
        }

        private const ulong ULONG_MAX = ulong.MaxValue / 10;
        public bool ExtractUInt64(out ulong value)
        {
            return InternalExtractUnsigned(out value, ulong.MaxValue, ULONG_MAX);
        }

        public bool ExtractFloat(out float value)
        {
            bool result = ExtractDouble(out double tmp);
            value = (float) tmp;
            return result;
        }

        public bool ExtractDouble(out double value)
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
            SkipWhitespace();
            return true;
        }

        public bool ExtractBoolean()
        {
            bool value = Data[Position].ToChar(null) switch
            {
                '0' => false,
                '1' => true,
                _ => Position + 4 <= Next &&
                    (Data[Position].ToChar(null).ToAsciiLower() == 't') &&
                    (Data[Position + 1].ToChar(null).ToAsciiLower() == 'r') &&
                    (Data[Position + 2].ToChar(null).ToAsciiLower() == 'u') &&
                    (Data[Position + 3].ToChar(null).ToAsciiLower() == 'e'),
            };
            SkipWhitespace();
            return value;
        }

        public short ExtractInt16()
        {
            if (ExtractInt16(out short value))
                return value;
            throw new Exception("Data for Int16 not present");
        }

        public ushort ExtractUInt16()
        {
            if (ExtractUInt16(out ushort value))
                return value;
            throw new Exception("Data for UInt16 not present");
        }

        public int ExtractInt32()
        {
            if (ExtractInt32(out int value))
                return value;
            throw new Exception("Data for Int32 not present");
        }

        public uint ExtractUInt32()
        {
            if (ExtractUInt32(out uint value))
                return value;
            throw new Exception("Data for UInt32 not present");
        }

        public long ExtractInt64()
        {
            if (ExtractInt64(out long value))
                return value;
            throw new Exception("Data for Int64 not present");
        }

        public ulong ExtractUInt64()
        {
            if (ExtractUInt64(out ulong value))
                return value;
            throw new Exception("Data for UInt64 not present");
        }

        public float ExtractFloat()
        {
            if (ExtractFloat(out float value))
                return value;
            throw new Exception("Data for Float not present");
        }

        public double ExtractDouble()
        {
            if (ExtractDouble(out double value))
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

        private bool InternalExtractSigned(out long value, long hardMax, long hardMin, long softMax)
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
                        SkipWhitespace();
                        return true;
                    }
                }

                value *= sign;
                SkipWhitespace();
                return true;
            }
        }

        private bool InternalExtractUnsigned(out ulong value, ulong hardMax, ulong softMax)
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
                    }
                }
                break;
            }
            SkipWhitespace();
            return true;
        }
    }
}
