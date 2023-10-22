using System;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public static class YARGTextLoader
    {
        private static readonly UTF32Encoding UTF32BE = new(true, false);

        public static YARGTextReader<byte, ByteStringDecoder>? TryLoadByteText(byte[] data)
        {
            if ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF))
                return null;

            int position = data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            return new YARGTextReader<byte, ByteStringDecoder>(data, position);
        }

        public static YARGTextReader<char, CharStringDecoder> LoadCharText(byte[] data)
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
            return new YARGTextReader<char, CharStringDecoder>(charData, 0);
        }
    }

    public sealed class YARGTextReader<TChar, TDecoder>
        where TChar : unmanaged, IConvertible
        where TDecoder : IStringDecoder<TChar>, new()
    {
        private readonly TDecoder decoder = new();
        public readonly YARGTextContainer<TChar> Container;

        public YARGTextReader(TChar[] data, int position)
        {
            Container = new YARGTextContainer<TChar>(data, position);
            SkipWhitespace();
            SetNextPointer();
            if (Container.Data[Container.Position].ToChar(null) == '\n')
                GotoNextLine();
        }

        public char SkipWhitespace()
        {
            while (Container.Position < Container.Length)
            {
                char ch = Container.Data[Container.Position].ToChar(null);
                if (ch.IsAsciiWhitespace())
                {
                    if (ch == '\n')
                        return ch;
                }
                else if (ch != '=')
                    return ch;
                ++Container.Position;
            }

            return (char) 0;
        }

        public void GotoNextLine()
        {
            char curr;
            do
            {
                Container.Position = Container.Next;
                if (Container.Position >= Container.Length)
                    break;

                Container.Position++;
                curr = SkipWhitespace();

                if (Container.Position == Container.Length)
                    break;

                if (Container.Data[Container.Position].ToChar(null) == '{')
                {
                    Container.Position++;
                    curr = SkipWhitespace();
                }

                SetNextPointer();
            } while (curr == '\n' || curr == '/' && Container.Data[Container.Position + 1].ToChar(null) == '/');
        }

        public void SetNextPointer()
        {
            Container.Next = Container.Position;
            while (Container.Next < Container.Length && Container.Data[Container.Next].ToChar(null) != '\n')
                ++Container.Next;
        }

        public string ExtractModifierName()
        {
            int curr = Container.Position;
            while (curr < Container.Length)
            {
                char b = Container.Data[curr].ToChar(null);
                if (b.IsAsciiWhitespace() || b == '=')
                    break;
                ++curr;
            }

            string name = decoder.Decode(Container.Data, Container.Position, curr - Container.Position);
            Container.Position = curr;
            SkipWhitespace();
            return name;
        }

        public string ExtractLine()
        {
            return decoder.Decode(Container.Data, Container.Position, Container.Next - Container.Position).TrimEnd();
        }

        public string ExtractText(bool isChartFile)
        {
            (int stringBegin, int stringEnd) = (Container.Position, Container.Next);
            if (Container.Data[stringEnd - 1].ToChar(null) == '\r')
                --stringEnd;

            if (isChartFile && Container.Data[Container.Position].ToChar(null) == '\"')
            {
                int end = stringEnd - 1;
                while (Container.Position + 1 < end && Container.Data[end].ToChar(null).IsAsciiWhitespace())
                    --end;

                if (Container.Position < end && Container.Data[end].ToChar(null) == '\"' && Container.Data[end - 1].ToChar(null) != '\\')
                {
                    ++stringBegin;
                    stringEnd = end;
                }
            }

            if (stringEnd < stringBegin)
                return string.Empty;

            while (stringEnd > stringBegin && Container.Data[stringEnd - 1].ToChar(null).IsAsciiWhitespace())
                --stringEnd;

            Container.Position = Container.Next;
            return decoder.Decode(Container.Data, stringBegin, stringEnd - stringBegin);
        }

        public bool ExtractBoolean()
        {
            bool result = Container.ExtractBoolean();
            SkipWhitespace();
            return result;
        }

        public short ExtractInt16()
        {
            short result = Container.ExtractInt16();
            SkipWhitespace();
            return result;
        }

        public ushort ExtractUInt16()
        {
            ushort result = Container.ExtractUInt16();
            SkipWhitespace();
            return result;
        }

        public int ExtractInt32()
        {
            int result = Container.ExtractInt32();
            SkipWhitespace();
            return result;
        }

        public uint ExtractUInt32()
        {
            uint result = Container.ExtractUInt32();
            SkipWhitespace();
            return result;
        }

        public long ExtractInt64()
        {
            long result = Container.ExtractInt64();
            SkipWhitespace();
            return result;
        }

        public ulong ExtractUInt64()
        {
            ulong result = Container.ExtractUInt64();
            SkipWhitespace();
            return result;
        }

        public float ExtractFloat()
        {
            float result = Container.ExtractFloat();
            SkipWhitespace();
            return result;
        }

        public double ExtractDouble()
        {
            double result = Container.ExtractDouble();
            SkipWhitespace();
            return result;
        }

        public bool ExtractInt16(out short value)
        {
            if (!Container.ExtractInt16(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractUInt16(out ushort value)
        {
            if (!Container.ExtractUInt16(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractInt32(out int value)
        {
            if (!Container.ExtractInt32(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractUInt32(out uint value)
        {
            if (!Container.ExtractUInt32(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractInt64(out long value)
        {
            if (!Container.ExtractInt64(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractUInt64(out ulong value)
        {
            if (!Container.ExtractUInt64(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractFloat(out float value)
        {
            if (!Container.ExtractFloat(out value))
                return false;
            SkipWhitespace();
            return true;
        }

        public bool ExtractDouble(out double value)
        {
            if (!Container.ExtractDouble(out value))
                return false;
            SkipWhitespace();
            return true;
        }
    }
}
