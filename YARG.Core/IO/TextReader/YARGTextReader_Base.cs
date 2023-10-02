using System;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public abstract class YARGTextReader_Base<TChar>
        where TChar : IConvertible
    {
        public readonly TChar[] Data;
        public readonly int Length;
        public int Position;

        protected int _next;
        public int Next => _next;

        public bool IsCurrentCharacter(char cmp)
        {
            return Data[Position].ToChar(null).Equals(cmp);
        }

        protected YARGTextReader_Base(TChar[] data)
        {
            Data = data;
            Length = data.Length;
        }

        public abstract char SkipWhiteSpace();

        private void SkipDigits()
        {
            while (Position < _next)
            {
                char ch = Data[Position].ToChar(null);
                if (!ch.IsAsciiDigit())
                    break;
                ++Position;
            }
        }

        public bool IsEndOfFile()
        {
            return Position >= Length;
        }

        private const char LAST_DIGIT_SIGNED = '7';
        private const char LAST_DIGIT_UNSIGNED = '5';

        private const short SHORT_MAX = short.MaxValue / 10;
        public bool ReadInt16(out short value)
        {
            if (InternalReadSigned(out long tmp, short.MaxValue, short.MinValue, SHORT_MAX))
            {
                value = (short) tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const int INT_MAX = int.MaxValue / 10;
        public bool ReadInt32(out int value)
        {
            if (InternalReadSigned(out long tmp, int.MaxValue, int.MinValue, INT_MAX))
            {
                value = (int) tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const long LONG_MAX = long.MaxValue / 10;
        public bool ReadInt64(out long value)
        {
            return InternalReadSigned(out value, long.MaxValue, long.MinValue, LONG_MAX);
        }

        private const ushort USHORT_MAX = ushort.MaxValue / 10;
        public bool ReadUInt16(out ushort value)
        {
            if (InternalReadUnsigned(out ulong tmp, ushort.MaxValue, USHORT_MAX))
            {
                value = (ushort) tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const uint UINT_MAX = uint.MaxValue / 10;
        public bool ReadUInt32(out uint value)
        {
            if (InternalReadUnsigned(out ulong tmp, uint.MaxValue, UINT_MAX))
            {
                value = (uint) tmp;
                return true;
            }
            value = default;
            return false;
        }

        private const ulong ULONG_MAX = ulong.MaxValue / 10;
        public bool ReadUInt64(out ulong value)
        {
            return InternalReadUnsigned(out value, ulong.MaxValue, ULONG_MAX);
        }

        public bool ReadFloat(out float value)
        {
            if (ReadDouble(out double tmp))
            {
                value = (float) tmp;
                return true;
            }
            value = default;
            return false;
        }

        public bool ReadDouble(out double value)
        {
            value = 0;
            if (Position >= _next)
                return false;

            char ch = Data[Position].ToChar(null);
            double sign = ch == '-' ? -1 : 1;

            if (ch == '-' || ch == '+')
            {
                ++Position;
                if (Position == _next)
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
                if (Position < _next)
                    ch = Data[Position].ToChar(null);
                else
                    break;
            }

            if (ch == '.')
            {
                ++Position;
                if (Position < _next)
                {
                    double divisor = 1;
                    ch = Data[Position].ToChar(null);
                    while (ch.IsAsciiDigit())
                    {
                        divisor *= 10;
                        value += (ch - '0') / divisor;

                        ++Position;
                        if (Position < _next)
                            ch = Data[Position].ToChar(null);
                        else
                            break;
                    }
                }
            }

            value *= sign;

            SkipWhiteSpace();
            return true;
        }

        public bool ReadBoolean()
        {
            return Data[Position].ToChar(null) switch
            {
                '0' => false,
                '1' => true,
                _ => Position + 4 <= _next &&
                    (Data[Position].ToChar(null).ToAsciiLower() == 't') &&
                    (Data[Position + 1].ToChar(null).ToAsciiLower() == 'r') &&
                    (Data[Position + 2].ToChar(null).ToAsciiLower() == 'u') &&
                    (Data[Position + 3].ToChar(null).ToAsciiLower() == 'e'),
            };
        }

        public short ReadInt16()
        {
            if (ReadInt16(out short value))
                return value;
            throw new Exception("Data for Int16 not present");
        }

        public ushort ReadUInt16()
        {
            if (ReadUInt16(out ushort value))
                return value;
            throw new Exception("Data for UInt16 not present");
        }

        public int ReadInt32()
        {
            if (ReadInt32(out int value))
                return value;
            throw new Exception("Data for Int32 not present");
        }

        public uint ReadUInt32()
        {
            if (ReadUInt32(out uint value))
                return value;
            throw new Exception("Data for UInt32 not present");
        }

        public long ReadInt64()
        {
            if (ReadInt64(out long value))
                return value;
            throw new Exception("Data for Int64 not present");
        }

        public ulong ReadUInt64()
        {
            if (ReadUInt64(out ulong value))
                return value;
            throw new Exception("Data for UInt64 not present");
        }

        public float ReadFloat()
        {
            if (ReadFloat(out float value))
                return value;
            throw new Exception("Data for Float not present");
        }

        public double ReadDouble()
        {
            if (ReadDouble(out double value))
                return value;
            throw new Exception("Data for Double not present");
        }

        private bool InternalReadSigned(out long value, long hardMax, long hardMin, long softMax)
        {
            value = 0;
            if (Position >= _next)
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
                    if (Position == _next)
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
                if (Position < _next)
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
                        SkipWhiteSpace();
                        return true;
                    }
                }

                value *= sign;
                SkipWhiteSpace();
                return true;
            }
        }

        private bool InternalReadUnsigned(out ulong value, ulong hardMax, ulong softMax)
        {
            value = 0;
            if (Position >= _next)
                return false;

            char ch = Data[Position].ToChar(null);
            if (ch == '+')
            {
                ++Position;
                if (Position == _next)
                    return false;
                ch = Data[Position].ToChar(null);
            }

            if (!ch.IsAsciiDigit())
                return false;

            while (true)
            {
                value += (ulong) (ch - '0');

                ++Position;
                if (Position < _next)
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
                        SkipWhiteSpace();
                        return true;
                    }
                }

                SkipWhiteSpace();
                return true;
            }
        }
    }
}
