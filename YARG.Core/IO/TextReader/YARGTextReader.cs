using System;
using System.Text;

namespace YARG.Core.IO
{
    public static class YARGTextLoader
    {
        private static readonly UTF32Encoding UTF32BE = new(true, false);

        public class Result<TChar, TDecoder>
            where TChar : unmanaged, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            public YARGTextContainer<TChar> Container;
            public readonly TDecoder Decoder;

            public Result(TChar[] data, int position)
            {
                Container = new YARGTextContainer<TChar>(data, position);
                Decoder = new TDecoder();

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
        }

        public static Result<byte, ByteStringDecoder>? TryLoadByteText(byte[] data)
        {
            if ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF))
            {
                return null;
            }

            int position = data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            return new Result<byte, ByteStringDecoder>(data, position);
        }

        public static Result<char, CharStringDecoder> LoadCharText(byte[] data)
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
            return new Result<char, CharStringDecoder>(charData, 0);
        }
    }

    public static class YARGTextReader
    {
        public static char SkipWhitespace<TChar>(ref YARGTextContainer<TChar> container)
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

        public static void GotoNextLine<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            while (container.Position < container.Length)
            {
                if (container.Data[container.Position++].ToChar(null) == '\n')
                {
                    // skip to first non-whitespace
                    while (container.Position < container.Length && container.Data[container.Position].ToChar(null) <= 32)
                    {
                        ++container.Position;
                    }
                    break;
                }
            }
        }

        public static bool SkipLinesUntil<TChar>(ref YARGTextContainer<TChar> container, char stopCharacter)
            where TChar : unmanaged, IConvertible
        {
            GotoNextLine(ref container);
            while (container.Position < container.Length)
            {
                if (container.Data[container.Position].ToChar(null) == stopCharacter)
                {
                    // Runs a check to ensure that the character is the start of the line
                    int test = container.Position - 1;
                    char character = container.Data[test].ToChar(null);
                    while (test > 0 && character <= 32 && character != '\n')
                    {
                        --test;
                        character = container.Data[test].ToChar(null);
                    }

                    if (character == '\n')
                    {
                        return true;
                    }
                }
                ++container.Position;
            }
            return false;
        }

        public static string ExtractModifierName<TChar, TDecoder>(ref YARGTextContainer<TChar> container, TDecoder decoder)
            where TChar : unmanaged, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            int curr = container.Position;
            while (curr < container.Length)
            {
                char b = container.Data[curr].ToChar(null);
                if (b <= 32 || b == '=')
                    break;
                ++curr;
            }

            string name = decoder.Decode(container.Data, container.Position, curr - container.Position);
            container.Position = curr;
            SkipWhitespace(ref container);
            return name;
        }

        public static string PeekLine<TChar, TDecoder>(ref YARGTextContainer<TChar> container, TDecoder decoder)
            where TChar : unmanaged, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            var curr = container.Position;
            while (curr < container.Length && container.Data[curr].ToChar(null) != '\n')
            {
                ++curr;
            }
            return decoder.Decode(container.Data, container.Position, curr - container.Position).TrimEnd();
        }

        public static string ExtractText<TChar, TDecoder>(ref YARGTextContainer<TChar> container, TDecoder decoder, bool isChartFile)
            where TChar : unmanaged, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
        {
            var stringBegin = container.Position;
            var stringEnd = -1;
            if (isChartFile && container.Position < container.Length && container.Data[container.Position].ToChar(null) == '\"')
            {
                while (true)
                {
                    ++container.Position;
                    if (container.Position == container.Length)
                    {
                        break;
                    }

                    char ch = container.Data[container.Position].ToChar(null);
                    if (ch == '\n')
                    {
                        break;
                    }

                    if (stringEnd == -1)
                    {
                        if (ch == '\"' && container.Data[container.Position - 1].ToChar(null) != '\\')
                        {
                            ++stringBegin;
                            stringEnd = container.Position;
                        }
                        else if (ch == '\r')
                        {
                            stringEnd = container.Position;
                        }
                    }
                }
            }
            else
            {
                while (container.Position < container.Length)
                {
                    char ch = container.Data[container.Position].ToChar(null);
                    if (ch == '\n')
                    {
                        break;
                    }

                    if (ch == '\r' && stringEnd == -1)
                    {
                        stringEnd = container.Position;
                    }
                    ++container.Position;
                }
            }

            if (stringEnd == -1)
            {
                stringEnd = container.Position;
            }

            while (stringBegin < stringEnd && container.Data[stringEnd - 1].ToChar(null) <= 32)
                --stringEnd;

            return decoder.Decode(container.Data, stringBegin, stringEnd - stringBegin);
        }

        public static bool ExtractBoolean<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            bool result = container.ExtractBoolean();
            SkipWhitespace(ref container);
            return result;
        }

        public static short ExtractInt16<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!container.TryExtractInt16(out short value))
            {
                throw new Exception("Data for Int16 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        public static ushort ExtractUInt16<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!container.TryExtractUInt16(out ushort value))
            {
                throw new Exception("Data for UInt16 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        public static int ExtractInt32<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!container.TryExtractInt32(out int value))
            {
                throw new Exception("Data for Int32 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        public static uint ExtractUInt32<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!container.TryExtractUInt32(out uint value))
            {
                throw new Exception("Data for UInt32 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        public static long ExtractInt64<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!container.TryExtractInt64(out long value))
            {
                throw new Exception("Data for Int64 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        public static ulong ExtractUInt64<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!container.TryExtractUInt64(out ulong value))
            {
                throw new Exception("Data for UInt64 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        public static float ExtractFloat<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!container.TryExtractFloat(out float value))
            {
                throw new Exception("Data for Int16 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        public static double ExtractDouble<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (!container.TryExtractDouble(out double value))
            {
                throw new Exception("Data for Int16 not present");
            }
            SkipWhitespace(ref container);
            return value;
        }
    }
}
