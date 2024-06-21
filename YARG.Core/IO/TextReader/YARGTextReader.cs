using System;
using System.Text;
using YARG.Core.IO.Disposables;

namespace YARG.Core.IO
{
    public static unsafe class YARGTextReader
    {
        private static readonly UTF32Encoding UTF32BE = new(true, false);

        public static bool TryLoadByteText(FixedArray<byte> data, out YARGTextContainer<byte> container)
        {
            if ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF))
            {
                container = default;
                return false;
            }

            int position = data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            container = new YARGTextContainer<byte>(data, position);
            return true;
        }

        public static FixedArray<char> ConvertToChar(FixedArray<byte> data)
        {
            long offset;
            long length;
            Encoding encoding;
            if (data[2] != 0)
            {
                offset = 2;
                length = (data.Length - 2) / 2;

                // UTF-16 encoding, endian-correct, so we can use a basic cast
                if ((data[0] == 0xFF) == BitConverter.IsLittleEndian) unsafe
                {
                    return FixedArray<char>.Alias((char*) (data.Ptr + offset), length);
                }
                encoding = data[0] == 0xFF ? Encoding.Unicode : Encoding.BigEndianUnicode;
            }
            else
            {
                offset = 3;
                length = (data.Length - 3) / 4;
                encoding = data[0] == 0xFF ? Encoding.UTF32 : UTF32BE;
            }

            var charData = AllocatedArray<char>.Alloc(length);
            unsafe
            {
                encoding.GetChars(data.Ptr + offset, (int) (data.Length - offset), charData.Ptr, (int) charData.Length);
            }
            return charData;
        }

        public static void SkipPureWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            while (container.Position < container.End && container.Position->ToChar(null) <= 32)
            {
                ++container.Position;
            }
        }

        /// <summary>
        /// Skips all whitespace starting at the current position of the provided container,
        /// until the end of the current line.
        /// </summary>
        /// <remarks>"\n" is not included as whitespace in this version</remarks>
        /// <typeparam name="TChar">Type of data contained</typeparam>
        /// <param name="container">Buffer of data</param>
        /// <returns>The current character that halted skipping, or 0 if at EoF</returns>
        public static char SkipWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            while (container.Position < container.End)
            {
                char ch = container.Position->ToChar(null);
                if (ch > 32 || ch == '\n')
                {
                    return ch;
                }
                ++container.Position;
            }
            return (char) 0;
        }

        public static void SkipWhitespaceAndEquals<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            if (SkipWhitespace(ref container) == '=')
            {
                ++container.Position;
                SkipWhitespace(ref container);
            }
        }

        public static void GotoNextLine<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            while (container.Position < container.End)
            {
                char curr = container.Position->ToChar(null);
                ++container.Position;
                if (curr == '\n')
                {
                    SkipPureWhitespace(ref container);
                    break;
                }
            }
        }

        public static bool SkipLinesUntil<TChar>(ref YARGTextContainer<TChar> container, char stopCharacter)
            where TChar : unmanaged, IConvertible
        {
            GotoNextLine(ref container);
            var pivot = container.Position;
            while (container.Position < container.End)
            {
                if (container.Position->ToChar(null) == stopCharacter)
                {
                    // Runs a check to ensure that the character is the start of the line
                    var test = container.Position - 1;
                    char character = test->ToChar(null);
                    while (test > pivot && character <= 32 && character != '\n')
                    {
                        --test;
                        character = test->ToChar(null);
                    }

                    if (character == '\n')
                    {
                        return true;
                    }
                    pivot = container.Position;
                }
                ++container.Position;
            }
            return false;
        }

        public static unsafe string ExtractModifierName<TChar>(ref YARGTextContainer<TChar> container, in delegate*<TChar*, long, string> decoder)
            where TChar : unmanaged, IConvertible
        {
            var curr = container.Position;
            while (curr < container.End)
            {
                char b = curr->ToChar(null);
                if (b <= 32 || b == '=')
                {
                    break;
                }
                ++curr;
            }

            string name = decoder(container.Position, curr - container.Position);
            container.Position = curr;
            SkipWhitespaceAndEquals(ref container);
            return name;
        }

        public static unsafe string PeekLine<TChar>(ref YARGTextContainer<TChar> container, in delegate*<TChar*, long, string> decoder)
            where TChar : unmanaged, IConvertible
        {
            var curr = container.Position;
            while (curr < container.End && curr->ToChar(null) != '\n')
            {
                ++curr;
            }
            return decoder(container.Position, curr - container.Position).TrimEnd();
        }

        public static unsafe string ExtractText<TChar>(ref YARGTextContainer<TChar> container, in delegate*<TChar*, long, string> decoder, bool isChartFile)
            where TChar : unmanaged, IConvertible
        {
            var stringBegin = container.Position;
            TChar* stringEnd = null;
            if (isChartFile && container.Position < container.End && container.Position->ToChar(null) == '\"')
            {
                while (true)
                {
                    ++container.Position;
                    if (container.Position == container.End)
                    {
                        break;
                    }

                    char ch = container.Position->ToChar(null);
                    if (ch == '\n')
                    {
                        break;
                    }

                    if (stringEnd == null)
                    {
                        if (ch == '\"' && container.Position[-1].ToChar(null) != '\\')
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
                while (container.Position < container.End)
                {
                    char ch = container.Position->ToChar(null);
                    if (ch == '\n')
                    {
                        break;
                    }

                    if (ch == '\r' && stringEnd == null)
                    {
                        stringEnd = container.Position;
                    }
                    ++container.Position;
                }
            }

            if (stringEnd == null)
            {
                stringEnd = container.Position;
            }

            while (stringBegin < stringEnd && stringEnd[-1].ToChar(null) <= 32)
                --stringEnd;

            return decoder(stringBegin, stringEnd - stringBegin);
        }

        /// <summary>
        /// Extracts a short and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The short</returns>
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

        /// <summary>
        /// Extracts a ushort and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The ushort</returns>
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

        /// <summary>
        /// Extracts a int and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The int</returns>
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

        /// <summary>
        /// Extracts a uint and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The uint</returns>
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

        /// <summary>
        /// Extracts a long and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The long</returns>
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

        /// <summary>
        /// Extracts a ulong and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The ulong</returns>
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

        /// <summary>
        /// Extracts a float and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The float</returns>
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

        /// <summary>
        /// Extracts a double and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The double</returns>
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
