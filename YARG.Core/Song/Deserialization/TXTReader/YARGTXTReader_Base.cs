using System;

namespace YARG.Core.Song.Deserialization
{
    public abstract class YARGTXTReader_Base
    {
        protected readonly byte[] data;
        protected readonly int length;
        protected int _position;

        public byte[] Data => data;
        public int Length => length;

        public int Position
        {
            get { return _position; }
            set
            {
                if (value < _position && _position <= length)
                    throw new ArgumentOutOfRangeException("Position");
                _position = value;
            }
        }

        protected int _next;
        public int Next { get { return _next; } }

        public byte Peek()
        {
            return data[_position];
        }

        protected YARGTXTReader_Base(byte[] data)
        {
            this.data = data;
            this.length = data.Length;
        }

        public abstract byte SkipWhiteSpace();

        private void SkipDigitsAndWhiteSpace()
        {
            while (_position < _next)
            {
                byte b = data[_position];
                if (b < '0' || '9' < b)
                    break;
                ++_position;
            }
            SkipWhiteSpace();
        }

        private const int SPACE_ASCII = 32;
        public static bool IsWhitespace(byte b)
        {
            return b <= SPACE_ASCII;
        }

        public bool IsEndOfFile()
        {
            return _position >= length;
        }

        public bool ReadBoolean(ref bool value)
        {
            value = data[_position] switch
            {
                (byte) '0' => false,
                (byte) '1' => true,
                _ => _position + 4 <= _next &&
                                    (data[_position] == 't' || data[_position] == 'T') &&
                                    (data[_position + 1] == 'r' || data[_position + 1] == 'R') &&
                                    (data[_position + 2] == 'u' || data[_position + 2] == 'U') &&
                                    (data[_position + 3] == 'e' || data[_position + 3] == 'E'),
            };
            return true;
        }


        private const char LAST_DIGIT_SIGNED = '7';
        private const char LAST_DIGIT_UNSIGNED = '5';
        private const short SHORT_MAX = short.MaxValue / 10;
        public bool ReadInt16(ref short value)
        {
            if (_position >= _next)
                return false;

            byte b = data[_position];
            short sign = b == '-' ? (short) -1 : (short)1;
            if (b == '-' || b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = data[_position];
            }

            if (b < '0' || '9' < b)
                return false;

            value = 0;
            while (true)
            {
                if (value > SHORT_MAX || (value == SHORT_MAX && b > LAST_DIGIT_SIGNED))
                {
                    value = sign == -1 ? short.MinValue : short.MaxValue;
                    SkipDigitsAndWhiteSpace();
                    return true;
                }

                value += (short) (b - '0');

                ++_position;
                if (_position == _next)
                    break;

                b = data[_position];
                if (b < '0' || '9' < b)
                    break;

                value *= 10;
            }

            value *= sign;
            SkipWhiteSpace();
            return true;
        }

        private const ushort USHORT_MAX = ushort.MaxValue / 10;
        public bool ReadUInt16(ref ushort value)
        {
            if (_position >= _next)
                return false;

            byte b = data[_position];
            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = data[_position];
            }

            if (b < '0' || '9' < b)
                return false;

            value = 0;
            while (true)
            {
                if (value > USHORT_MAX || (value == USHORT_MAX && b > LAST_DIGIT_UNSIGNED))
                {
                    value = ushort.MaxValue;
                    SkipDigitsAndWhiteSpace();
                    return true;
                }

                value += (ushort) (b - '0');

                ++_position;
                if (_position == _next)
                    break;

                b = data[_position];
                if (b < '0' || '9' < b)
                    break;
                value *= 10;
            }

            SkipWhiteSpace();
            return true;
        }

        private const int INT_MAX = int.MaxValue / 10;
        public bool ReadInt32(ref int value)
        {
            if (_position >= _next)
                return false;

            byte b = data[_position];
            int sign = b == '-' ? -1 : 1;
            if (b == '-' || b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = data[_position];
            }

            if (b < '0' || '9' < b)
                return false;

            value = 0;
            while (true)
            {
                if (value > INT_MAX || (value == INT_MAX && b > LAST_DIGIT_SIGNED))
                {
                    value = sign == -1 ? int.MinValue : int.MaxValue;
                    SkipDigitsAndWhiteSpace();
                    return true;
                }

                value +=  b - '0';

                ++_position;
                if (_position == _next)
                    break;

                b = data[_position];
                if (b < '0' || '9' < b)
                    break;
                value *= 10;
            }

            value *= sign;
            SkipWhiteSpace();
            return true;
        }

        private const uint UINT_MAX = uint.MaxValue / 10;
        public bool ReadUInt32(ref uint value)
        {
            if (_position >= _next)
                return false;

            byte b = data[_position];
            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = data[_position];
            }

            if (b < '0' || '9' < b)
                return false;

            value = 0;
            while (true)
            {
                if (value > UINT_MAX || (value == UINT_MAX && b > LAST_DIGIT_UNSIGNED))
                {
                    value = uint.MaxValue;
                    SkipDigitsAndWhiteSpace();
                    return true;
                }

                value += (uint) (b - '0');

                ++_position;
                if (_position == _next)
                    break;

                b = data[_position];
                if (b < '0' || '9' < b)
                    break;
                value *= 10;
            }

            SkipWhiteSpace();
            return true;
        }

        private const long LONG_MAX = long.MaxValue / 10;
        public bool ReadInt64(ref long value)
        {
            if (_position >= _next)
                return false;

            byte b = data[_position];
            long sign = b == '-' ? -1 : 1;
            if (b == '-' || b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = data[_position];
            }

            if (b < '0' || '9' < b)
                return false;

            value = 0;
            while (true)
            {
                if (value > LONG_MAX || (value == LONG_MAX && b > LAST_DIGIT_SIGNED))
                {
                    value = sign == -1 ? long.MinValue : long.MaxValue;
                    SkipDigitsAndWhiteSpace();
                    return true;
                }

                value += b - '0';

                ++_position;
                if (_position == _next)
                    break;

                b = data[_position];
                if (b < '0' || '9' < b)
                    break;
                value *= 10;
            }

            value *= sign;
            SkipWhiteSpace();
            return true;
        }

        private const ulong ULONG_MAX = ulong.MaxValue / 10;
        public bool ReadUInt64(ref ulong value)
        {
            if (_position >= _next)
                return false;

            byte b = data[_position];
            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = data[_position];
            }

            if (b < '0' || '9' < b)
                return false;

            value = 0;
            while (true)
            {
                if (value > ULONG_MAX || (value == ULONG_MAX && b > LAST_DIGIT_UNSIGNED))
                {
                    value = ulong.MaxValue;
                    SkipDigitsAndWhiteSpace();
                    return true;
                }

                value += (ulong) (b - '0');

                ++_position;
                if (_position == _next)
                    break;

                b = data[_position];
                if (b < '0' || '9' < b)
                    break;
                value *= 10;
            }

            SkipWhiteSpace();
            return true;
        }

        public bool ReadFloat(ref float value)
        {
            if (_position >= _next)
                return false;

            byte b = data[_position];
            bool isNegative = false;

            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = data[_position];
            }
            else if (b == '-')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = data[_position];
                isNegative = true;
            }

            if (b > '9' || (b < '0' && b != '.'))
                return false;

            value = 0;
            if (b != '.')
            {
                while (true)
                {
                    value += b - '0';
                    ++_position;
                    if (_position == _next)
                        break;

                    b = data[_position];
                    if (b < '0' || b > '9')
                        break;

                    value *= 10;
                }
            }

            if (b == '.')
            {
                ++_position;

                float dec = 0;
                int count = 0;
                if (_position < _next)
                {
                    b = data[_position];
                    if ('0' <= b && b <= '9')
                    {
                        ++count;
                        while (true)
                        {
                            dec += b - '0';
                            ++_position;
                            if (_position == _next)
                                break;

                            b = data[_position];
                            if (b < '0' || b > '9')
                                break;
                            dec *= 10;
                            ++count;
                        }
                    }
                }

                for (int i = 0; i < count; ++i)
                    dec /= 10;

                value += dec;
            }

            if (isNegative)
                value = -value;

            SkipWhiteSpace();
            return true;
        }

        public bool ReadDouble(ref double value)
        {
            if (_position >= _next)
                return false;

            byte b = data[_position];
            bool isNegative = false;

            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
            }
            else if (b == '-')
            {
                ++_position;
                if (_position == _next)
                    return false;
                isNegative = true;
            }

            if (b > '9')
                return false;

            if (b < '0' && b != '.')
                return false;

            value = 0;
            if (b != '.')
            {
                while (true)
                {
                    ++_position;
                    value += b - '0';
                    if (_position == _next)
                        break;

                    b = data[_position];
                    if (b < '0' || b > '9')
                        break;

                    value *= 10;
                }
            }

            if (b == '.')
            {
                ++_position;

                double dec = 0;
                int count = 0;
                if (_position < _next)
                {
                    b = data[_position];
                    if ('0' <= b && b <= '9')
                    {
                        ++count;
                        while (true)
                        {
                            dec += b - '0';
                            ++_position;
                            if (_position == _next)
                                break;

                            b = data[_position];
                            if (b < '0' || b > '9')
                                break;
                            dec *= 10;
                            ++count;
                        }
                    }
                }

                for (int i = 0; i < count; ++i)
                    dec /= 10;

                value += dec;
            }

            if (isNegative)
                value = -value;

            SkipWhiteSpace();
            return true;
        }

        public bool ReadBoolean()
        {
            bool value = default;
            if (!ReadBoolean(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public short ReadInt16()
        {
            short value = default;
            if (!ReadInt16(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public ushort ReadUInt16()
        {
            ushort value = default;
            if (!ReadUInt16(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public int ReadInt32()
        {
            int value = default;
            if (!ReadInt32(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public uint ReadUInt32()
        {
            uint value = default;
            if (!ReadUInt32(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public long ReadInt64()
        {
            long value = default;
            if (!ReadInt64(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public ulong ReadUInt64()
        {
            ulong value = default;
            if (!ReadUInt64(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public float ReadFloat()
        {
            float value = default;
            if (!ReadFloat(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public double ReadDouble()
        {
            double value = default;
            if (!ReadDouble(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public ReadOnlySpan<byte> ExtractBasicSpan(int length)
        {
            return new ReadOnlySpan<byte>(data, _position, length);
        }
    }

    public abstract class YARGTXTReader_BaseChar
    {
        protected readonly char[] data;
        protected readonly int length;
        protected int _position;

        public char[] Data => data;
        public int Length => length;

        public int Position
        {
            get { return _position; }
            set
            {
                if (value < _position && _position <= length)
                    throw new ArgumentOutOfRangeException("Position");
                _position = value;
            }
        }

        protected int _next;
        public int Next { get { return _next; } }

        public char Peek()
        {
            return data[_position];
        }

        protected YARGTXTReader_BaseChar(char[] data)
        {
            this.data = data;
            this.length = data.Length;
        }

        public abstract char SkipWhiteSpace();

        private const int SPACE_ASCII = 32;
        public static bool IsWhitespace(char b)
        {
            return b <= SPACE_ASCII;
        }

        public bool IsEndOfFile()
        {
            return _position >= length;
        }

        public bool ReadBoolean(ref bool value)
        {
            value = data[_position] switch
            {
                '0' => false,
                '1' => true,
                _ => _position + 4 <= _next &&
                                    (data[_position] == 't' || data[_position] == 'T') &&
                                    (data[_position + 1] == 'r' || data[_position + 1] == 'R') &&
                                    (data[_position + 2] == 'u' || data[_position + 2] == 'U') &&
                                    (data[_position + 3] == 'e' || data[_position + 3] == 'E'),
            };
            return true;
        }

        public bool ReadInt16(ref short value)
        {
            if (_position >= _next)
                return false;

            short b = (short) data[_position];
            if (b != '-')
            {
                if (b == '+')
                {
                    ++_position;
                    if (_position == _next)
                        return false;
                    b = (short) data[_position];
                }

                if (b < '0' || b > '9')
                    return false;

                value = 0;
                while (true)
                {
                    _position++;
                    short val = (short) (value + b - '0');
                    if (val >= value)
                    {
                        value = val;
                        if (_position == _next)
                            break;

                        b = (short) data[_position];
                        if (b < '0' || b > '9')
                            break;

                        val *= 10;
                        if (val >= value)
                            value = val;
                        else
                            value = short.MaxValue;
                    }
                    else
                        value = short.MaxValue;
                }
            }
            else
            {
                ++_position;
                if (_position == _next)
                    return false;

                b = (short) data[_position];
                if (b < '0' || b > '9')
                    return false;

                value = 0;
                while (true)
                {
                    _position++;
                    short val = (short) (value - (b - '0'));
                    if (val <= value)
                    {
                        value = val;
                        if (_position == _next)
                            break;

                        b = (short) data[_position];
                        if (b < '0' || b > '9')
                            break;

                        val *= 10;
                        if (val <= value)
                            value = val;
                        else
                            value = short.MinValue;
                    }
                    else
                        value = short.MinValue;
                }
            }

            SkipWhiteSpace();
            return true;
        }

        public bool ReadUInt16(ref ushort value)
        {
            if (_position >= _next)
                return false;

            ushort b = data[_position];
            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = data[_position];
            }

            if (b < '0' || b > '9')
                return false;

            value = 0;
            while (true)
            {
                _position++;
                ushort val = (ushort) (value + b - '0');
                if (val >= value)
                {
                    value = val;
                    if (_position == _next)
                        break;

                    b = data[_position];
                    if (b < '0' || b > '9')
                        break;

                    val *= 10;
                    if (val >= value)
                        value = val;
                    else
                        value = ushort.MaxValue;
                }
                else
                    value = ushort.MaxValue;
            }
            SkipWhiteSpace();
            return true;
        }

        public bool ReadInt32(ref int value)
        {
            if (_position >= _next)
                return false;

            int b = data[_position];
            if (b != '-')
            {
                if (b == '+')
                {
                    ++_position;
                    if (_position == _next)
                        return false;
                    b = data[_position];
                }

                if (b < '0' || b > '9')
                    return false;

                value = 0;
                while (true)
                {
                    _position++;
                    int val = value + b - '0';
                    if (val >= value)
                    {
                        value = val;
                        if (_position == _next)
                            break;

                        b = data[_position];
                        if (b < '0' || b > '9')
                            break;

                        val *= 10;
                        if (val >= value)
                            value = val;
                        else
                            value = int.MaxValue;
                    }
                    else
                        value = int.MaxValue;
                }
            }
            else
            {
                ++_position;
                if (_position == _next)
                    return false;

                b = data[_position];
                if (b < '0' || b > '9')
                    return false;

                value = 0;
                while (true)
                {
                    _position++;
                    int val = value - (b - '0');
                    if (val <= value)
                    {
                        value = val;
                        if (_position == _next)
                            break;

                        b = data[_position];
                        if (b < '0' || b > '9')
                            break;

                        val *= 10;
                        if (val <= value)
                            value = val;
                        else
                            value = int.MinValue;
                    }
                    else
                        value = int.MinValue;
                }
            }
            SkipWhiteSpace();
            return true;
        }

        public bool ReadUInt32(ref uint value)
        {
            if (_position >= _next)
                return false;

            uint b = data[_position];
            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = data[_position];
            }

            if (b < '0' || b > '9')
                return false;

            value = 0;
            while (true)
            {
                _position++;
                uint val = value + b - '0';
                if (val >= value)
                {
                    value = val;
                    if (_position == _next)
                        break;

                    b = data[_position];
                    if (b < '0' || b > '9')
                        break;

                    val *= 10;
                    if (val >= value)
                        value = val;
                    else
                        value = uint.MaxValue;
                }
                else
                    value = uint.MaxValue;
            }
            SkipWhiteSpace();
            return true;
        }

        public bool ReadInt64(ref long value)
        {
            if (_position >= _next)
                return false;

            long b = data[_position];
            if (b != '-')
            {
                if (b == '+')
                {
                    ++_position;
                    if (_position == _next)
                        return false;
                    b = data[_position];
                }

                if (b < '0' || b > '9')
                    return false;

                value = 0;
                while (true)
                {
                    _position++;
                    long val = value + b - '0';
                    if (val >= value)
                    {
                        value = val;
                        if (_position == _next)
                            break;

                        b = data[_position];
                        if (b < '0' || b > '9')
                            break;

                        val *= 10;
                        if (val >= value)
                            value = val;
                        else
                            value = long.MaxValue;
                    }
                    else
                        value = long.MaxValue;
                }
            }
            else
            {
                ++_position;
                if (_position == _next)
                    return false;

                b = data[_position];
                if (b < '0' || b > '9')
                    return false;

                value = 0;
                while (true)
                {
                    _position++;
                    long val = value - (b - '0');
                    if (val <= value)
                    {
                        value = val;
                        if (_position == _next)
                            break;

                        b = data[_position];
                        if (b < '0' || b > '9')
                            break;

                        val *= 10;
                        if (val <= value)
                            value = val;
                        else
                            value = long.MinValue;
                    }
                    else
                        value = long.MinValue;
                }
            }
            SkipWhiteSpace();
            return true;
        }

        public bool ReadUInt64(ref ulong value)
        {
            if (_position >= _next)
                return false;

            ulong b = data[_position];
            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = data[_position];
            }

            if (b < '0' || b > '9')
                return false;

            value = 0;
            while (true)
            {
                _position++;
                ulong val = value + b - '0';
                if (val >= value)
                {
                    value = val;
                    if (_position == _next)
                        break;

                    b = data[_position];
                    if (b < '0' || b > '9')
                        break;

                    val *= 10;
                    if (val >= value)
                        value = val;
                    else
                        value = ulong.MaxValue;
                }
                else
                    value = ulong.MaxValue;
            }
            SkipWhiteSpace();
            return true;
        }

        public bool ReadFloat(ref float value)
        {
            if (_position >= _next)
                return false;

            char b = data[_position];
            bool isNegative = false;

            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = data[_position];
            }
            else if (b == '-')
            {
                ++_position;
                if (_position == _next)
                    return false;
                b = data[_position];
                isNegative = true;
            }

            if (b > '9')
                return false;

            if (b < '0' && b != '.')
                return false;

            value = 0;
            if (b != '.')
            {
                while (true)
                {
                    value += b - '0';
                    ++_position;
                    if (_position == _next)
                        break;

                    b = data[_position];
                    if (b < '0' || b > '9')
                        break;

                    value *= 10;
                }
            }

            if (b == '.')
            {
                ++_position;

                float dec = 0;
                int count = 0;
                if (_position < _next)
                {
                    b = data[_position];
                    if ('0' <= b && b <= '9')
                    {
                        ++count;
                        while (true)
                        {
                            dec += b - '0';
                            ++_position;
                            if (_position == _next)
                                break;

                            b = data[_position];
                            if (b < '0' || b > '9')
                                break;
                            dec *= 10;
                            ++count;
                        }
                    }
                }

                for (int i = 0; i < count; ++i)
                    dec /= 10;

                value += dec;
            }

            if (isNegative)
                value = -value;

            SkipWhiteSpace();
            return true;
        }

        public bool ReadDouble(ref double value)
        {
            if (_position >= _next)
                return false;

            char b = data[_position];
            bool isNegative = false;

            if (b == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
            }
            else if (b == '-')
            {
                ++_position;
                if (_position == _next)
                    return false;
                isNegative = true;
            }

            if (b > '9')
                return false;

            if (b < '0' && b != '.')
                return false;

            value = 0;
            if (b != '.')
            {
                while (true)
                {
                    ++_position;
                    value += b - '0';
                    if (_position == _next)
                        break;

                    b = data[_position];
                    if (b < '0' || b > '9')
                        break;

                    value *= 10;
                }
            }

            if (b == '.')
            {
                ++_position;

                double dec = 0;
                int count = 0;
                if (_position < _next)
                {
                    b = data[_position];
                    if ('0' <= b && b <= '9')
                    {
                        ++count;
                        while (true)
                        {
                            dec += b - '0';
                            ++_position;
                            if (_position == _next)
                                break;

                            b = data[_position];
                            if (b < '0' || b > '9')
                                break;
                            dec *= 10;
                            ++count;
                        }
                    }
                }

                for (int i = 0; i < count; ++i)
                    dec /= 10;

                value += dec;
            }

            if (isNegative)
                value = -value;

            SkipWhiteSpace();
            return true;
        }

        public bool ReadBoolean()
        {
            bool value = default;
            if (!ReadBoolean(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public short ReadInt16()
        {
            short value = default;
            if (!ReadInt16(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public ushort ReadUInt16()
        {
            ushort value = default;
            if (!ReadUInt16(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public int ReadInt32()
        {
            int value = default;
            if (!ReadInt32(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public uint ReadUInt32()
        {
            uint value = default;
            if (!ReadUInt32(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public long ReadInt64()
        {
            long value = default;
            if (!ReadInt64(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public ulong ReadUInt64()
        {
            ulong value = default;
            if (!ReadUInt64(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public float ReadFloat()
        {
            float value = default;
            if (!ReadFloat(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public double ReadDouble()
        {
            double value = default;
            if (!ReadDouble(ref value))
                throw new Exception("Failed to parse data");
            return value;
        }

        public ReadOnlySpan<char> ExtractBasicSpan(int length)
        {
            return new ReadOnlySpan<char>(data, _position, length);
        }
    }
}
