using System;
using System.Text;

namespace YARG.Core.Song.Deserialization
{
    public abstract class YARGTXTReader_Base<T>
        where T : IConvertible
    {
        public readonly T[] Data;
        public readonly int Length;
        protected int _position;

        public int Position
        {
            get { return _position; }
            set
            {
                if (value < _position && _position <= Length)
                    throw new ArgumentOutOfRangeException("Position");
                _position = value;
            }
        }

        protected int _next;
        public int Next { get { return _next; } }

        public bool IsCurrentCharacter(char cmp)
        {
            return Data[_position].ToChar(null).Equals(cmp);
        }

        protected YARGTXTReader_Base(T[] data)
        {
            Data = data;
            Length = data.Length;
        }

        public abstract char SkipWhiteSpace();

        private void SkipDigits()
        {
            while (_position < _next)
            {
                char ch = Data[_position].ToChar(null);
                if (ch < '0' || '9' < ch)
                    break;
                ++_position;
            }
        }

        public bool IsEndOfFile()
        {
            return _position >= Length;
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
            if (_position >= _next)
                return false;

            char ch = Data[_position].ToChar(null);
            double sign = ch == '-' ? -1 : 1;

            if (ch == '-' || ch == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                ch = Data[_position].ToChar(null);
            }

            if (ch > '9' || (ch < '0' && ch != '.'))
                return false;

            while ('0' <= ch && ch <= '9')
            {
                value *= 10;
                value += ch - '0';
                ++_position;
                if (_position < _next)
                    ch = Data[_position].ToChar(null);
                else
                    break;
            }

            if (ch == '.')
            {
                ++_position;
                if (_position < _next)
                {
                    double divisor = 1;
                    ch = Data[_position].ToChar(null);
                    while ('0' <= ch && ch <= '9')
                    {
                        divisor *= 10;
                        value += (ch - '0') / divisor;

                        ++_position;
                        if (_position < _next)
                            ch = Data[_position].ToChar(null);
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
            return Data[_position].ToChar(null) switch
            {
                '0' => false,
                '1' => true,
                _ => _position + 4 <= _next &&
                    (Data[_position].ToChar(null) == 't' || Data[_position].ToChar(null) == 'T') &&
                    (Data[_position + 1].ToChar(null) == 'r' || Data[_position + 1].ToChar(null) == 'R') &&
                    (Data[_position + 2].ToChar(null) == 'u' || Data[_position + 2].ToChar(null) == 'U') &&
                    (Data[_position + 3].ToChar(null) == 'e' || Data[_position + 3].ToChar(null) == 'E'),
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

        public ReadOnlySpan<T> ExtractBasicSpan(int length)
        {
            return new ReadOnlySpan<T>(Data, _position, length);
        }

        private bool InternalReadSigned(out long value, long hardMax, long hardMin, long softMax)
        {
            value = 0;
            if (_position >= _next)
                return false;

            char ch = Data[_position].ToChar(null);
            long sign = 1;

            switch (ch)
            {
                case '-':
                    sign = -1;
                    goto case '+';
                case '+':
                    ++_position;
                    if (_position == _next)
                        return false;
                    ch = Data[_position].ToChar(null);
                    break;
            }

            if (ch < '0' || '9' < ch)
                return false;

            while (true)
            {
                value += ch - '0';

                ++_position;
                if (_position < _next)
                {
                    ch = Data[_position].ToChar(null);
                    if ('0' <= ch && ch <= '9')
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
            if (_position >= _next)
                return false;

            char ch = Data[_position].ToChar(null);
            if (ch == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                ch = Data[_position].ToChar(null);
            }

            if (ch < '0' || '9' < ch)
                return false;

            while (true)
            {
                value += (ulong) (ch - '0');

                ++_position;
                if (_position < _next)
                {
                    ch = Data[_position].ToChar(null);
                    if ('0' <= ch && ch <= '9')
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
