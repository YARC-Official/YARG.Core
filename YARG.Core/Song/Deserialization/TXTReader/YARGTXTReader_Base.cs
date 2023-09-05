using System;
using System.Text;

namespace YARG.Core.Song.Deserialization
{
    public abstract class YARGTXTReader_Base<T>
        where T : IConvertible
    {
        protected readonly T[] data;
        protected readonly int length;
        protected int _position;

        public T[] Data => data;
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

        public T Peek()
        {
            return data[_position];
        }

        protected YARGTXTReader_Base(T[] data)
        {
            this.data = data;
            this.length = data.Length;
        }

        public abstract char SkipWhiteSpace();

        private void SkipDigitsAndWhiteSpace()
        {
            while (_position < _next)
            {
                char ch = data[_position].ToChar(null);
                if (ch < '0' || '9' < ch)
                    break;
                ++_position;
            }
            SkipWhiteSpace();
        }

        public bool IsEndOfFile()
        {
            return _position >= length;
        }

        public bool ReadBoolean(ref bool value)
        {
            value = data[_position].ToChar(null) switch
            {
                '0' => false,
                '1' => true,
                _ => _position + 4 <= _next &&
                    (data[_position    ].ToChar(null) == 't' || data[_position    ].ToChar(null) == 'T') &&
                    (data[_position + 1].ToChar(null) == 'r' || data[_position + 1].ToChar(null) == 'R') &&
                    (data[_position + 2].ToChar(null) == 'u' || data[_position + 2].ToChar(null) == 'U') &&
                    (data[_position + 3].ToChar(null) == 'e' || data[_position + 3].ToChar(null) == 'E'),
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

            char ch = data[_position].ToChar(null);
            short sign = ch == '-' ? (short) -1 : (short)1;

            if (ch == '-' || ch == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                ch = data[_position].ToChar(null);
            }

            if (ch < '0' || '9' < ch)
                return false;

            value = 0;
            while (true)
            {
                if (value > SHORT_MAX || (value == SHORT_MAX && ch > LAST_DIGIT_SIGNED))
                {
                    value = sign == -1 ? short.MinValue : short.MaxValue;
                    SkipDigitsAndWhiteSpace();
                    return true;
                }

                value += (short) (ch - '0');

                ++_position;
                if (_position == _next)
                    break;

                ch = data[_position].ToChar(null);
                if (ch < '0' || '9' < ch)
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

            char ch = data[_position].ToChar(null);

            if (ch == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                ch = data[_position].ToChar(null);
            }

            if (ch < '0' || '9' < ch)
                return false;

            value = 0;
            while (true)
            {
                if (value > USHORT_MAX || (value == USHORT_MAX && ch > LAST_DIGIT_UNSIGNED))
                {
                    value = ushort.MaxValue;
                    SkipDigitsAndWhiteSpace();
                    return true;
                }

                value += (ushort) (ch - '0');

                ++_position;
                if (_position == _next)
                    break;

                ch = data[_position].ToChar(null);
                if (ch < '0' || '9' < ch)
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

            char ch = data[_position].ToChar(null);
            int sign = ch == '-' ? -1 : 1;

            if (ch == '-' || ch == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                ch = data[_position].ToChar(null);
            }

            if (ch < '0' || '9' < ch)
                return false;

            value = 0;
            while (true)
            {
                if (value > INT_MAX || (value == INT_MAX && ch > LAST_DIGIT_SIGNED))
                {
                    value = sign == -1 ? int.MinValue : int.MaxValue;
                    SkipDigitsAndWhiteSpace();
                    return true;
                }

                value +=  ch - '0';

                ++_position;
                if (_position == _next)
                    break;

                ch = data[_position].ToChar(null);
                if (ch < '0' || '9' < ch)
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

            char ch = data[_position].ToChar(null);

            if (ch == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                ch = data[_position].ToChar(null);
            }

            if (ch < '0' || '9' < ch)
                return false;

            value = 0;
            while (true)
            {
                if (value > UINT_MAX || (value == UINT_MAX && ch > LAST_DIGIT_UNSIGNED))
                {
                    value = uint.MaxValue;
                    SkipDigitsAndWhiteSpace();
                    return true;
                }

                value += (uint) (ch - '0');

                ++_position;
                if (_position == _next)
                    break;

                ch = data[_position].ToChar(null);
                if (ch < '0' || '9' < ch)
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

            char ch = data[_position].ToChar(null);
            long sign = ch == '-' ? -1 : 1;

            if (ch == '-' || ch == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                ch = data[_position].ToChar(null);
            }

            if (ch < '0' || '9' < ch)
                return false;

            value = 0;
            while (true)
            {
                if (value > LONG_MAX || (value == LONG_MAX && ch > LAST_DIGIT_SIGNED))
                {
                    value = sign == -1 ? long.MinValue : long.MaxValue;
                    SkipDigitsAndWhiteSpace();
                    return true;
                }

                value += ch - '0';

                ++_position;
                if (_position == _next)
                    break;

                ch = data[_position].ToChar(null);
                if (ch < '0' || '9' < ch)
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

            char ch = data[_position].ToChar(null);
            if (ch == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                ch = data[_position].ToChar(null);
            }

            if (ch < '0' || '9' < ch)
                return false;

            value = 0;
            while (true)
            {
                if (value > ULONG_MAX || (value == ULONG_MAX && ch > LAST_DIGIT_UNSIGNED))
                {
                    value = ulong.MaxValue;
                    SkipDigitsAndWhiteSpace();
                    return true;
                }

                value += (ulong) (ch - '0');

                ++_position;
                if (_position == _next)
                    break;

                ch = data[_position].ToChar(null);
                if (ch < '0' || '9' < ch)
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

            char ch = data[_position].ToChar(null);
            float sign = ch == '-' ? -1 : 1;

            if (ch == '-' || ch == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                ch = data[_position].ToChar(null);
            }

            if (ch > '9' || (ch < '0' && ch != '.'))
                return false;

            value = 0;
            if (ch != '.')
            {
                while (true)
                {
                    value += ch - '0';
                    ++_position;
                    if (_position == _next)
                        break;

                    ch = data[_position].ToChar(null);
                    if (ch < '0' || ch > '9')
                        break;

                    value *= 10;
                }
            }

            if (ch == '.')
            {
                ++_position;

                float dec = 0;
                int count = 0;
                if (_position < _next)
                {
                    ch = data[_position].ToChar(null);
                    if ('0' <= ch && ch <= '9')
                    {
                        ++count;
                        while (true)
                        {
                            dec += ch - '0';
                            ++_position;
                            if (_position == _next)
                                break;

                            ch = data[_position].ToChar(null);
                            if (ch < '0' || ch > '9')
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

            value *= sign;

            SkipWhiteSpace();
            return true;
        }

        public bool ReadDouble(ref double value)
        {
            if (_position >= _next)
                return false;

            char ch = data[_position].ToChar(null);
            double sign = ch == '-' ? -1 : 1;

            if (ch == '-' || ch == '+')
            {
                ++_position;
                if (_position == _next)
                    return false;
                ch = data[_position].ToChar(null);
            }

            if (ch > '9' || (ch < '0' && ch != '.'))
                return false;

            value = 0;
            if (ch != '.')
            {
                while (true)
                {
                    ++_position;
                    value += ch - '0';
                    if (_position == _next)
                        break;

                    ch = data[_position].ToChar(null);
                    if (ch < '0' || ch > '9')
                        break;

                    value *= 10;
                }
            }

            if (ch == '.')
            {
                ++_position;

                double dec = 0;
                int count = 0;
                if (_position < _next)
                {
                    ch = data[_position].ToChar(null);
                    if ('0' <= ch && ch <= '9')
                    {
                        ++count;
                        while (true)
                        {
                            dec += ch - '0';
                            ++_position;
                            if (_position == _next)
                                break;

                            ch = data[_position].ToChar(null);
                            if (ch < '0' || ch > '9')
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

            value *= sign;

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

        public ReadOnlySpan<T> ExtractBasicSpan(int length)
        {
            return new ReadOnlySpan<T>(data, _position, length);
        }
    }
}
