using System;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public static class YARGNumberExtractor
    {
        private const char LAST_DIGIT_SIGNED = '7';
        private const char LAST_DIGIT_UNSIGNED = '5';

        private const short SHORT_MAX = short.MaxValue / 10;
        public static bool Int16<TChar>(YARGBaseTextReader<TChar> reader, out short value)
            where TChar : IConvertible
        {
            if (InternalReadSigned(reader, out long tmp, short.MaxValue, short.MinValue, SHORT_MAX))
            {
                value = (short) tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const int INT_MAX = int.MaxValue / 10;
        public static bool Int32<TChar>(YARGBaseTextReader<TChar> reader, out int value)
            where TChar : IConvertible
        {
            if (InternalReadSigned(reader, out long tmp, int.MaxValue, int.MinValue, INT_MAX))
            {
                value = (int) tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const long LONG_MAX = long.MaxValue / 10;
        public static bool Int64<TChar>(YARGBaseTextReader<TChar> reader, out long value)
            where TChar : IConvertible
        {
            return InternalReadSigned(reader, out value, long.MaxValue, long.MinValue, LONG_MAX);
        }

        private const ushort USHORT_MAX = ushort.MaxValue / 10;
        public static bool UInt16<TChar>(YARGBaseTextReader<TChar> reader, out ushort value)
            where TChar : IConvertible
        {
            if (InternalReadUnsigned(reader, out ulong tmp, ushort.MaxValue, USHORT_MAX))
            {
                value = (ushort) tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const uint UINT_MAX = uint.MaxValue / 10;
        public static bool UInt32<TChar>(YARGBaseTextReader<TChar> reader, out uint value)
            where TChar : IConvertible
        {
            if (InternalReadUnsigned(reader, out ulong tmp, uint.MaxValue, UINT_MAX))
            {
                value = (uint) tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const ulong ULONG_MAX = ulong.MaxValue / 10;
        public static bool UInt64<TChar>(YARGBaseTextReader<TChar> reader, out ulong value)
            where TChar : IConvertible
        {
            return InternalReadUnsigned(reader, out value, ulong.MaxValue, ULONG_MAX);
        }

        public static bool Float<TChar>(YARGBaseTextReader<TChar> reader, out float value)
            where TChar : IConvertible
        {
            if (Double(reader, out double tmp))
            {
                value = (float) tmp;
                return true;
            }
            value = default;
            return false;
        }

        public static bool Double<TChar>(YARGBaseTextReader<TChar> reader, out double value)
            where TChar : IConvertible
        {
            value = 0;
            if (reader.Position >= reader.Next)
                return false;

            char ch = reader.Data[reader.Position].ToChar(null);
            double sign = ch == '-' ? -1 : 1;

            if (ch == '-' || ch == '+')
            {
                ++reader.Position;
                if (reader.Position == reader.Next)
                    return false;
                ch = reader.Data[reader.Position].ToChar(null);
            }

            if (!ch.IsAsciiDigit() && ch != '.')
                return false;

            while (ch.IsAsciiDigit())
            {
                value *= 10;
                value += ch - '0';
                ++reader.Position;
                if (reader.Position < reader.Next)
                    ch = reader.Data[reader.Position].ToChar(null);
                else
                    break;
            }

            if (ch == '.')
            {
                ++reader.Position;
                if (reader.Position < reader.Next)
                {
                    double divisor = 1;
                    ch = reader.Data[reader.Position].ToChar(null);
                    while (ch.IsAsciiDigit())
                    {
                        divisor *= 10;
                        value += (ch - '0') / divisor;

                        ++reader.Position;
                        if (reader.Position < reader.Next)
                            ch = reader.Data[reader.Position].ToChar(null);
                        else
                            break;
                    }
                }
            }

            value *= sign;

            reader.SkipWhiteSpace();
            return true;
        }

        public static bool Boolean<TChar>(YARGBaseTextReader<TChar> reader)
            where TChar : IConvertible
        {
            return reader.Data[reader.Position].ToChar(null) switch
            {
                '0' => false,
                '1' => true,
                _ => reader.Position + 4 <= reader.Next &&
                    (reader.Data[reader.Position].ToChar(null).ToAsciiLower() == 't') &&
                    (reader.Data[reader.Position + 1].ToChar(null).ToAsciiLower() == 'r') &&
                    (reader.Data[reader.Position + 2].ToChar(null).ToAsciiLower() == 'u') &&
                    (reader.Data[reader.Position + 3].ToChar(null).ToAsciiLower() == 'e'),
            };
        }

        public static short Int16<TChar>(YARGBaseTextReader<TChar> reader)
            where TChar : IConvertible
        {
            if (Int16(reader, out short value))
                return value;
            throw new Exception("Data for Int16 not present");
        }

        public static ushort UInt16<TChar>(YARGBaseTextReader<TChar> reader)
            where TChar : IConvertible
        {
            if (UInt16(reader, out ushort value))
                return value;
            throw new Exception("Data for UInt16 not present");
        }

        public static int Int32<TChar>(YARGBaseTextReader<TChar> reader)
            where TChar : IConvertible
        {
            if (Int32(reader, out int value))
                return value;
            throw new Exception("Data for Int32 not present");
        }

        public static uint UInt32<TChar>(YARGBaseTextReader<TChar> reader)
            where TChar : IConvertible
        {
            if (UInt32(reader, out uint value))
                return value;
            throw new Exception("Data for UInt32 not present");
        }

        public static long Int64<TChar>(YARGBaseTextReader<TChar> reader)
            where TChar : IConvertible
        {
            if (Int64(reader, out long value))
                return value;
            throw new Exception("Data for Int64 not present");
        }

        public static ulong UInt64<TChar>(YARGBaseTextReader<TChar> reader)
            where TChar : IConvertible
        {
            if (UInt64(reader, out ulong value))
                return value;
            throw new Exception("Data for UInt64 not present");
        }

        public static float Float<TChar>(YARGBaseTextReader<TChar> reader)
            where TChar : IConvertible
        {
            if (Float(reader, out float value))
                return value;
            throw new Exception("Data for Float not present");
        }

        public static double Double<TChar>(YARGBaseTextReader<TChar> reader)
            where TChar : IConvertible
        {
            if (Double(reader, out double value))
                return value;
            throw new Exception("Data for Double not present");
        }

        private static void SkipDigits<TChar>(YARGBaseTextReader<TChar> reader)
            where TChar : IConvertible
        {
            while (reader.Position < reader.Next)
            {
                char ch = reader.Data[reader.Position].ToChar(null);
                if (!ch.IsAsciiDigit())
                    break;
                ++reader.Position;
            }
        }

        private static bool InternalReadSigned<TChar>(YARGBaseTextReader<TChar> reader, out long value, long hardMax, long hardMin, long softMax)
            where TChar : IConvertible
        {
            value = 0;
            if (reader.Position >= reader.Next)
                return false;

            char ch = reader.Data[reader.Position].ToChar(null);
            long sign = 1;

            switch (ch)
            {
                case '-':
                    sign = -1;
                    goto case '+';
                case '+':
                    ++reader.Position;
                    if (reader.Position == reader.Next)
                        return false;
                    ch = reader.Data[reader.Position].ToChar(null);
                    break;
            }

            if (!ch.IsAsciiDigit())
                return false;

            while (true)
            {
                value += ch - '0';

                ++reader.Position;
                if (reader.Position < reader.Next)
                {
                    ch = reader.Data[reader.Position].ToChar(null);
                    if (ch.IsAsciiDigit())
                    {
                        if (value < softMax || value == softMax && ch <= LAST_DIGIT_SIGNED)
                        {
                            value *= 10;
                            continue;
                        }

                        value = sign == -1 ? hardMin : hardMax;
                        SkipDigits(reader);
                        reader.SkipWhiteSpace();
                        return true;
                    }
                }

                value *= sign;
                reader.SkipWhiteSpace();
                return true;
            }
        }

        private static bool InternalReadUnsigned<TChar>(YARGBaseTextReader<TChar> reader, out ulong value, ulong hardMax, ulong softMax)
            where TChar : IConvertible
        {
            value = 0;
            if (reader.Position >= reader.Next)
                return false;

            char ch = reader.Data[reader.Position].ToChar(null);
            if (ch == '+')
            {
                ++reader.Position;
                if (reader.Position == reader.Next)
                    return false;
                ch = reader.Data[reader.Position].ToChar(null);
            }

            if (!ch.IsAsciiDigit())
                return false;

            while (true)
            {
                value += (ulong) (ch - '0');

                ++reader.Position;
                if (reader.Position < reader.Next)
                {
                    ch = reader.Data[reader.Position].ToChar(null);
                    if (ch.IsAsciiDigit())
                    {
                        if (value < softMax || value == softMax && ch <= LAST_DIGIT_UNSIGNED)
                        {
                            value *= 10;
                            continue;
                        }

                        value = hardMax;
                        SkipDigits(reader);
                        reader.SkipWhiteSpace();
                        return true;
                    }
                }

                reader.SkipWhiteSpace();
                return true;
            }
        }
    }
}
