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

        public static char[] ConvertToChar(byte[] data)
        {
            int offset;
            Encoding encoding;
            if (data[2] != 0)
            {
                offset = 2;
                encoding = data[0] == 0xFF ? Encoding.Unicode : Encoding.BigEndianUnicode;
            }
            else
            {
                offset = 3;
                encoding = data[0] == 0xFF ? Encoding.UTF32 : UTF32BE;
            }
            return encoding.GetChars(data, offset, data.Length - offset);
        }
    }

    public static class YARGTextReader
    {
        public static char SkipWhitespace<TChar>(YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            while (container.Position < container.Length)
            {
                char ch = container.Data[container.Position].ToChar(null);
                if (ch <= 32)
                {
                    if (ch == '\n')
                        return ch;
                }
                else if (ch != '=')
                    return ch;
                ++container.Position;
            }
            return (char) 0;
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
            while (Container.Position < Container.Length)
            {
                char curr = Container.Data[Container.Position].ToChar(null);
                if (curr > 32 && curr != '{' && curr != '=')
                {
                    break;
                }
                ++Container.Position;
            }
        }

        public char SkipWhitespace()
        {
            return YARGTextReader.SkipWhitespace(Container);
        }

        public void GotoNextLine()
        {
            char curr = default;
            while (Container.Position < Container.Length)
            {
                curr = Container.Data[Container.Position].ToChar(null);
                ++Container.Position;
                if (curr == '\n')
                {
                    break;
                }
            }

            while (Container.Position < Container.Length)
            {
                curr = Container.Data[Container.Position].ToChar(null);
                if (curr > 32 && curr != '{' && curr != '=')
                {
                    break;

                }
                ++Container.Position;
            }
        }

        public void SkipLinesUntil(char stopCharacter)
        {
            GotoNextLine();
            while (Container.Position < Container.Length)
            {
                if (Container.Data[Container.Position].ToChar(null) == stopCharacter)
                {
                    // Runs a check to ensure that the character is the start of the line
                    int test = Container.Position - 1;
                    char character = Container.Data[test].ToChar(null);
                    while (test > 0 && character <= 32 && character != '\n')
                    {
                        --test;
                        character = Container.Data[test].ToChar(null);
                    }

                    if (character == '\n')
                        break;
                }
                ++Container.Position;
            }
        }

        public string ExtractModifierName()
        {
            int curr = Container.Position;
            while (curr < Container.Length)
            {
                char b = Container.Data[curr].ToChar(null);
                if (b <= 32 || b == '=')
                    break;
                ++curr;
            }

            string name = decoder.Decode(Container.Data, Container.Position, curr - Container.Position);
            Container.Position = curr;
            SkipWhitespace();
            return name;
        }

        public string PeekLine()
        {
            var curr = Container.Position;
            while (curr < Container.Length && Container.Data[curr].ToChar(null) != '\n')
            {
                ++curr;
            }
            return decoder.Decode(Container.Data, Container.Position, curr - Container.Position).TrimEnd();
        }

        public string ExtractText(bool isChartFile)
        {
            var stringBegin = Container.Position;
            var stringEnd = -1;
            if (isChartFile && Container.Position < Container.Length && Container.Data[Container.Position].ToChar(null) == '\"')
            {
                while (true)
                {
                    ++Container.Position;
                    if (Container.Position == Container.Length)
                    {
                        break;
                    }

                    char ch = Container.Data[Container.Position].ToChar(null);
                    if (ch == '\n')
                    {
                        break;
                    }

                    if (stringEnd == -1)
                    {
                        if (ch == '\"' && Container.Data[Container.Position - 1].ToChar(null) != '\\')
                        {
                            ++stringBegin;
                            stringEnd = Container.Position;
                        }
                        else if (ch == '\r')
                        {
                            stringEnd = Container.Position;
                        }
                    }
                }
            }
            else
            {
                while (Container.Position < Container.Length)
                {
                    char ch = Container.Data[Container.Position].ToChar(null);
                    if (ch == '\n')
                    {
                        break;
                    }

                    if (ch == '\r' && stringEnd == -1)
                    {
                        stringEnd = Container.Position;
                    }
                    ++Container.Position;
                }
            }

            if (stringEnd == -1)
            {
                stringEnd = Container.Position;
            }

            while (stringBegin < stringEnd && Container.Data[stringEnd - 1].ToChar(null) <= 32)
                --stringEnd;

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
