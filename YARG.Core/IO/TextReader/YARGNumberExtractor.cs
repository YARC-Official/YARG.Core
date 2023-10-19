using System;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public static class YARGNumberExtractor
    {
        private const char LAST_DIGIT_SIGNED = '7';
        private const char LAST_DIGIT_UNSIGNED = '5';

        private const short SHORT_MAX = short.MaxValue / 10;
        public static bool Int16<TChar>(YARGTextContainer<TChar> container, out short value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            if (InternalReadSigned(container, out long tmp, short.MaxValue, short.MinValue, SHORT_MAX, SkipWhitespace))
            {
                value = (short) tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const int INT_MAX = int.MaxValue / 10;
        public static bool Int32<TChar>(YARGTextContainer<TChar> container, out int value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            if (InternalReadSigned(container, out long tmp, int.MaxValue, int.MinValue, INT_MAX, SkipWhitespace))
            {
                value = (int) tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const long LONG_MAX = long.MaxValue / 10;
        public static bool Int64<TChar>(YARGTextContainer<TChar> container, out long value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            return InternalReadSigned(container, out value, long.MaxValue, long.MinValue, LONG_MAX, SkipWhitespace);
        }

        private const ushort USHORT_MAX = ushort.MaxValue / 10;
        public static bool UInt16<TChar>(YARGTextContainer<TChar> container, out ushort value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            if (InternalReadUnsigned(container, out ulong tmp, ushort.MaxValue, USHORT_MAX, SkipWhitespace))
            {
                value = (ushort) tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const uint UINT_MAX = uint.MaxValue / 10;
        public static bool UInt32<TChar>(YARGTextContainer<TChar> container, out uint value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            if (InternalReadUnsigned(container, out ulong tmp, uint.MaxValue, UINT_MAX, SkipWhitespace))
            {
                value = (uint) tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const ulong ULONG_MAX = ulong.MaxValue / 10;
        public static bool UInt64<TChar>(YARGTextContainer<TChar> container, out ulong value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            return InternalReadUnsigned(container, out value, ulong.MaxValue, ULONG_MAX, SkipWhitespace);
        }

        public static bool Float<TChar>(YARGTextContainer<TChar> container, out float value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            if (Double(container, out double tmp, SkipWhitespace))
            {
                value = (float) tmp;
                return true;
            }
            value = default;
            return false;
        }

        public static bool Double<TChar>(YARGTextContainer<TChar> container, out double value, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            value = 0;
            if (container.Position >= container.Next)
                return false;

            char ch = container.Data[container.Position].ToChar(null);
            double sign = ch == '-' ? -1 : 1;

            if (ch == '-' || ch == '+')
            {
                ++container.Position;
                if (container.Position == container.Next)
                    return false;
                ch = container.Data[container.Position].ToChar(null);
            }

            if (!ch.IsAsciiDigit() && ch != '.')
                return false;

            while (ch.IsAsciiDigit())
            {
                value *= 10;
                value += ch - '0';
                ++container.Position;
                if (container.Position < container.Next)
                    ch = container.Data[container.Position].ToChar(null);
                else
                    break;
            }

            if (ch == '.')
            {
                ++container.Position;
                if (container.Position < container.Next)
                {
                    double divisor = 1;
                    ch = container.Data[container.Position].ToChar(null);
                    while (ch.IsAsciiDigit())
                    {
                        divisor *= 10;
                        value += (ch - '0') / divisor;

                        ++container.Position;
                        if (container.Position < container.Next)
                            ch = container.Data[container.Position].ToChar(null);
                        else
                            break;
                    }
                }
            }

            value *= sign;
            SkipWhitespace(container);
            return true;
        }

        public static bool Boolean<TChar>(YARGTextContainer<TChar> container)
            where TChar : IConvertible
        {
            return container.Data[container.Position].ToChar(null) switch
            {
                '0' => false,
                '1' => true,
                _ => container.Position + 4 <= container.Next &&
                    (container.Data[container.Position].ToChar(null).ToAsciiLower() == 't') &&
                    (container.Data[container.Position + 1].ToChar(null).ToAsciiLower() == 'r') &&
                    (container.Data[container.Position + 2].ToChar(null).ToAsciiLower() == 'u') &&
                    (container.Data[container.Position + 3].ToChar(null).ToAsciiLower() == 'e'),
            };
        }

        public static short Int16<TChar>(YARGTextContainer<TChar> container, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            if (Int16(container, out short value, SkipWhitespace))
                return value;
            throw new Exception("Data for Int16 not present");
        }

        public static ushort UInt16<TChar>(YARGTextContainer<TChar> container, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            if (UInt16(container, out ushort value, SkipWhitespace))
                return value;
            throw new Exception("Data for UInt16 not present");
        }

        public static int Int32<TChar>(YARGTextContainer<TChar> container, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            if (Int32(container, out int value, SkipWhitespace))
                return value;
            throw new Exception("Data for Int32 not present");
        }

        public static uint UInt32<TChar>(YARGTextContainer<TChar> container, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            if (UInt32(container, out uint value, SkipWhitespace))
                return value;
            throw new Exception("Data for UInt32 not present");
        }

        public static long Int64<TChar>(YARGTextContainer<TChar> container, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            if (Int64(container, out long value, SkipWhitespace))
                return value;
            throw new Exception("Data for Int64 not present");
        }

        public static ulong UInt64<TChar>(YARGTextContainer<TChar> container, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            if (UInt64(container, out ulong value, SkipWhitespace))
                return value;
            throw new Exception("Data for UInt64 not present");
        }

        public static float Float<TChar>(YARGTextContainer<TChar> container, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            if (Float(container, out float value, SkipWhitespace))
                return value;
            throw new Exception("Data for Float not present");
        }

        public static double Double<TChar>(YARGTextContainer<TChar> container, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            if (Double(container, out double value, SkipWhitespace))
                return value;
            throw new Exception("Data for Double not present");
        }

        private static void SkipDigits<TChar>(YARGTextContainer<TChar> container)
            where TChar : IConvertible
        {
            while (container.Position < container.Next)
            {
                char ch = container.Data[container.Position].ToChar(null);
                if (!ch.IsAsciiDigit())
                    break;
                ++container.Position;
            }
        }

        private static bool InternalReadSigned<TChar>(YARGTextContainer<TChar> container, out long value, long hardMax, long hardMin, long softMax, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            value = 0;
            if (container.Position >= container.Next)
                return false;

            char ch = container.Data[container.Position].ToChar(null);
            long sign = 1;

            switch (ch)
            {
                case '-':
                    sign = -1;
                    goto case '+';
                case '+':
                    ++container.Position;
                    if (container.Position == container.Next)
                        return false;
                    ch = container.Data[container.Position].ToChar(null);
                    break;
            }

            if (!ch.IsAsciiDigit())
                return false;

            while (true)
            {
                value += ch - '0';

                ++container.Position;
                if (container.Position < container.Next)
                {
                    ch = container.Data[container.Position].ToChar(null);
                    if (ch.IsAsciiDigit())
                    {
                        if (value < softMax || value == softMax && ch <= LAST_DIGIT_SIGNED)
                        {
                            value *= 10;
                            continue;
                        }

                        value = sign == -1 ? hardMin : hardMax;
                        SkipDigits(container);
                        SkipWhitespace(container);
                        return true;
                    }
                }

                value *= sign;
                SkipWhitespace(container);
                return true;
            }
        }

        private static bool InternalReadUnsigned<TChar>(YARGTextContainer<TChar> container, out ulong value, ulong hardMax, ulong softMax, Func<YARGTextContainer<TChar>, char> SkipWhitespace)
            where TChar : IConvertible
        {
            value = 0;
            if (container.Position >= container.Next)
                return false;

            char ch = container.Data[container.Position].ToChar(null);
            if (ch == '+')
            {
                ++container.Position;
                if (container.Position == container.Next)
                    return false;
                ch = container.Data[container.Position].ToChar(null);
            }

            if (!ch.IsAsciiDigit())
                return false;

            while (true)
            {
                value += (ulong) (ch - '0');

                ++container.Position;
                if (container.Position < container.Next)
                {
                    ch = container.Data[container.Position].ToChar(null);
                    if (ch.IsAsciiDigit())
                    {
                        if (value < softMax || value == softMax && ch <= LAST_DIGIT_UNSIGNED)
                        {
                            value *= 10;
                            continue;
                        }

                        value = hardMax;
                        SkipDigits(container);
                        SkipWhitespace(container);
                        return true;
                    }
                }

                SkipWhitespace(container);
                return true;
            }
        }
    }
}
