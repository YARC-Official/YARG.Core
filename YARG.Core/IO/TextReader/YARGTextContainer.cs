using System;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public static class YARGTextContainer
    {
        public static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
        public static readonly Encoding UTF8Strict = new UTF8Encoding(false, true);
    }

    public struct YARGTextContainer<TChar>
        where TChar : unmanaged, IConvertible
    {
        public readonly TChar[] Data;
        public int Position;

        public readonly int Length => Data.Length;

        public YARGTextContainer(TChar[] data, int position)
        {
            Data = data;
            Position = position;
        }

        public readonly bool IsCurrentCharacter(char cmp)
        {
            return Data[Position].ToChar(null).Equals(cmp);
        }

        public readonly bool IsEndOfFile()
        {
            return Position >= Length;
        }

        public readonly ReadOnlySpan<TChar> Slice(int position, int length)
        {
            return new ReadOnlySpan<TChar>(Data, position, length);
        }

        public ReadOnlySpan<TChar> ExtractSpan(int length)
        {
            var span = Slice(Position, length);
            Position += length;
            return span;
        }

        private const char LAST_DIGIT_SIGNED = '7';
        private const char LAST_DIGIT_UNSIGNED = '5';

        private const short SHORT_MAX = short.MaxValue / 10;
        public bool TryExtractInt16(out short value)
        {
            bool result = InternalExtractSigned(out long tmp, short.MaxValue, short.MinValue, SHORT_MAX);
            value = (short)tmp;
            return result;
        }

        private const int INT_MAX = int.MaxValue / 10;
        public bool TryExtractInt32(out int value)
        {
            bool result = InternalExtractSigned(out long tmp, int.MaxValue, int.MinValue, INT_MAX);
            value = (int)tmp;
            return result;
        }

        private const long LONG_MAX = long.MaxValue / 10;
        public bool TryExtractInt64(out long value)
        {
            return InternalExtractSigned(out value, long.MaxValue, long.MinValue, LONG_MAX);
        }

        private const ushort USHORT_MAX = ushort.MaxValue / 10;
        public bool TryExtractUInt16(out ushort value)
        {
            bool result = InternalExtractUnsigned(out ulong tmp, ushort.MaxValue, USHORT_MAX);
            value = (ushort) tmp;
            return result;
        }

        private const uint UINT_MAX = uint.MaxValue / 10;
        public bool TryExtractUInt32(out uint value)
        {
            bool result = InternalExtractUnsigned(out ulong tmp, uint.MaxValue, UINT_MAX);
            value = (uint) tmp;
            return result;
        }

        private const ulong ULONG_MAX = ulong.MaxValue / 10;
        public bool TryExtractUInt64(out ulong value)
        {
            return InternalExtractUnsigned(out value, ulong.MaxValue, ULONG_MAX);
        }

        public bool TryExtractFloat(out float value)
        {
            bool result = TryExtractDouble(out double tmp);
            value = (float) tmp;
            return result;
        }

        public bool TryExtractDouble(out double value)
        {
            value = 0;
            if (Position >= Length)
            {
                return false;
            }

            char ch = Data[Position].ToChar(null);
            double sign = ch == '-' ? -1 : 1;

            if (ch == '-' || ch == '+')
            {
                ++Position;
                if (Position >= Length)
                {
                    return false;
                }
                ch = Data[Position].ToChar(null);
            }

            if (ch < '0' || '9' < ch && ch != '.')
            {
                return false;
            }

            while ('0' <= ch && ch <= '9')
            {
                value *= 10;
                value += ch - '0';
                ++Position;
                if (Position == Length)
                {
                    break;
                }
                ch = Data[Position].ToChar(null);
            }

            if (ch == '.')
            {
                ++Position;
                if (Position < Length)
                {
                    double divisor = 1;
                    ch = Data[Position].ToChar(null);
                    while ('0' <= ch && ch <= '9')
                    {
                        divisor *= 10;
                        value += (ch - '0') / divisor;

                        ++Position;
                        if (Position == Length)
                        {
                            break;
                        }
                        ch = Data[Position].ToChar(null);
                    }
                }
            }

            value *= sign;
            return true;
        }

        public readonly bool ExtractBoolean()
        {
            return Position < Length && Data[Position].ToChar(null) switch
            {
                '0' => false,
                '1' => true,
                _ => Position + 4 <= Length &&
                    (Data[Position].ToChar(null).ToAsciiLower() == 't') &&
                    (Data[Position + 1].ToChar(null).ToAsciiLower() == 'r') &&
                    (Data[Position + 2].ToChar(null).ToAsciiLower() == 'u') &&
                    (Data[Position + 3].ToChar(null).ToAsciiLower() == 'e'),
            };
        }

        private void SkipDigits()
        {
            while (Position < Length)
            {
                char ch = Data[Position].ToChar(null);
                if (ch < '0' || '9' < ch)
                {
                    break;
                }
                ++Position;
            }
        }

        private bool InternalExtractSigned(out long value, long hardMax, long hardMin, long softMax)
        {
            value = 0;
            if (Position >= Length)
            {
                return false;
            }

            char ch = Data[Position].ToChar(null);
            long sign = 1;

            switch (ch)
            {
                case '-':
                    sign = -1;
                    goto case '+';
                case '+':
                    ++Position;
                    if (Position >= Length)
                    {
                        return false;
                    }
                    ch = Data[Position].ToChar(null);
                    break;
            }

            if (ch < '0' || '9' < ch)
            {
                return false;
            }

            while (true)
            {
                value += ch - '0';

                ++Position;
                if (Position < Length)
                {
                    ch = Data[Position].ToChar(null);
                    if ('0' <= ch && ch <= '9')
                    {
                        if (value < softMax || value == softMax && ch <= LAST_DIGIT_SIGNED)
                        {
                            value *= 10;
                            continue;
                        }

                        value = sign == -1 ? hardMin : hardMax;
                        SkipDigits();
                        return true;
                    }
                }

                value *= sign;
                return true;
            }
        }

        private bool InternalExtractUnsigned(out ulong value, ulong hardMax, ulong softMax)
        {
            value = 0;
            if (Position >= Length)
            {
                return false;
            }

            char ch = Data[Position].ToChar(null);
            if (ch == '+')
            {
                ++Position;
                if (Position >= Length)
                {
                    return false;
                }
                ch = Data[Position].ToChar(null);
            }

            if (ch < '0' || '9' < ch)
                return false;

            while (true)
            {
                value += (ulong)(ch - '0');

                ++Position;
                if (Position < Length)
                {
                    ch = Data[Position].ToChar(null);
                    if ('0' <= ch && ch <= '9')
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
            return true;
        }
    }
}
