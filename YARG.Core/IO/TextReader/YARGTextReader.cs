using System;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public static class TextConstants<TChar>
            where TChar : unmanaged
    {
        public static readonly TChar NEWLINE;
        public static readonly TChar OPEN_BRACKET;
        public static readonly TChar CLOSE_BRACE;

        static unsafe TextConstants()
        {
            int newline = '\n';
            int openBracket = '[';
            int closeBrace = '}';
            NEWLINE = *(TChar*) &newline;
            OPEN_BRACKET = *(TChar*) &openBracket;
            CLOSE_BRACE = *(TChar*) &closeBrace;
        }
    }

    public static class YARGTextReader
    {
        public static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
        public static readonly Encoding UTF8Strict = new UTF8Encoding(false, true);
        public const int WHITESPACE_LIMIT = 32;

        public static bool TryUTF8(in FixedArray<byte> data, out YARGTextContainer<byte> container)
        {
            // If it doesn't throw with `At(1)`, then 0 and 1 are valid indices.
            // We can therefore skip bounds checking
            if ((data.At(1) == 0xFE && data[0] == 0xFF) || (data[0] == 0xFE && data[1] == 0xFF))
            {
                container = default;
                return false;
            }

            container = new YARGTextContainer<byte>(data, UTF8Strict);
            // Same idea as above, but with index `2` instead
            if (data.At(2) == 0xBF && data[0] == 0xEF && data[1] == 0xBB)
            {
                container.Position += 3;
            }
            SkipPureWhitespace(ref container);
            return true;
        }

        public static FixedArray<char> TryUTF16Cast(in FixedArray<byte> data)
        {
            var buffer = FixedArray<char>.Null;
            if (data.At(2) != 0)
            {
                const int UTF16BOM_OFFSET = 2;
                long length = (data.Length - UTF16BOM_OFFSET) / sizeof(char);
                if ((data[0] == 0xFF) != BitConverter.IsLittleEndian)
                {
                    // We have to swap the endian of the data so string conversion works properly
                    // but we can't just use the original buffer as we create a hash off it.
                    buffer = FixedArray<char>.Alloc(length);
                    for (int i = 0, j = UTF16BOM_OFFSET; i < buffer.Length; ++i, j += sizeof(char))
                    {
                        buffer[i] = (char) (data[j] << 8 | data[j + 1]);
                    }
                }
                else
                {
                    buffer = FixedArray<char>.Cast(in data, UTF16BOM_OFFSET, length);
                }
            }
            return buffer;
        }

        public static YARGTextContainer<char> CreateUTF16Container(in FixedArray<char> data)
        {
            var container = new YARGTextContainer<char>(in data, Encoding.Unicode);
            SkipPureWhitespace(ref container);
            return container;
        }

        public static FixedArray<int> CastUTF32(in FixedArray<byte> data)
        {
            const int UTF32BOM_OFFSET = 3;

            FixedArray<int> buffer;
            long length = (data.Length - UTF32BOM_OFFSET) / sizeof(int);
            // We already know by this point that index `0` is valid
            if ((data[0] == 0xFF) != BitConverter.IsLittleEndian)
            {
                // We have to swap the endian of the data so string conversion works properly
                // but we can't just use the original buffer as we create a hash off it.
                buffer = FixedArray<int>.Alloc(length);
                for (int i = 0, j = UTF32BOM_OFFSET; i < buffer.Length; ++i, j += sizeof(int))
                {
                    buffer[i] = data[j] << 24 |
                                data[j + 1] << 16 |
                                data[j + 2] << 16 |
                                data[j + 3];
                }
            }
            else
            {
                buffer = FixedArray<int>.Cast(in data, UTF32BOM_OFFSET, length);
            }
            return buffer;
        }

        public static YARGTextContainer<int> CreateUTF32Container(in FixedArray<int> data)
        {
            var container = new YARGTextContainer<int>(in data, Encoding.UTF32);
            SkipPureWhitespace(ref container);
            return container;
        }

        public static void SkipPureWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            // Unity/Mono has a bug on the commented-out code here, where the JIT generates a useless
            // `cmp dword ptr [rax], 0` before actually performing ToInt32(null).
            // This causes an access violation (which translates to a NullReferenceException here) on
            // memory-mapped files whose size on disk is 2 or less bytes greater than the actual file contents,
            // due to the `cmp` above over-reading data from `rax` (which contains Position in that moment).
            //
            // Explicitly dereferencing the pointer into a value first avoids this issue. The useless `cmp`
            // is still generated, but now `rax` points to the stack, and so the over-read is always done in
            // a valid memory space.
            //
            // 9/28 Edit: However, now that fixedArray removed memorymappedfile functionality, the overread is a non-issue
            // in terms of causing any actual access violation errors
            while (!container.IsAtEnd() && container.Get() <= WHITESPACE_LIMIT)
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
        public static int SkipWhitespace<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            while (!container.IsAtEnd())
            {
                int ch = container.Get();
                if (ch > WHITESPACE_LIMIT || ch == '\n')
                {
                    return ch;
                }
                ++container.Position;
            }
            return 0;
        }

        public static void SkipWhitespaceAndEquals<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            while (!container.IsAtEnd())
            {
                int ch = container.Get();
                if (ch <= WHITESPACE_LIMIT)
                {
                    if (ch == '\n')
                    {
                        break;
                    }
                }
                else if (ch != '=')
                {
                    break;
                }
                ++container.Position;
            }
        }

        public static void GotoNextLine<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            int index = container.GetSpanOfRemainder().IndexOf(TextConstants<TChar>.NEWLINE);
            if (index >= 0)
            {
                container.Position += index;
                SkipPureWhitespace(ref container);
            }
            else
            {
                container.Position = container.Length;
            }
        }

        public static bool SkipLinesUntil<TChar>(ref YARGTextContainer<TChar> container, TChar stopCharacter)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            GotoNextLine(ref container);
            while (true)
            {
                int i = container.GetSpanOfRemainder().IndexOf(stopCharacter);
                if (i == -1)
                {
                    container.Position = container.Length;
                    return false;
                }

                container.Position += i;

                int limit = -i;
                for (int test = -1; test >= limit; --test)
                {
                    int val = container[test];
                    if (val == '\n')
                    {
                        return true;
                    }

                    if (val > WHITESPACE_LIMIT)
                    {
                        break;
                    }
                }
                ++container.Position;
            }
        }

        public static unsafe string ExtractModifierName<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible
        {
            int length = 0;
            while (container.Position + length < container.Length)
            {
                int val = container[length];
                if (val <= WHITESPACE_LIMIT || val == '=')
                {
                    break;
                }
                ++length;
            }

            string name = Decode(container.PositionPointer, length, ref container);
            container.Position += length;
            SkipWhitespaceAndEquals(ref container);
            return name;
        }

        public static unsafe string PeekLine<TChar>(ref YARGTextContainer<TChar> container)
            where TChar : unmanaged, IConvertible, IEquatable<TChar>
        {
            var span = container.GetSpanOfRemainder();
            long length = span.IndexOf(TextConstants<TChar>.NEWLINE);
            if (length == -1)
            {
                length = span.Length;
            }

            while (length > 0 && span[(int)(length - 1)].ToInt32(null) <= WHITESPACE_LIMIT)
            {
                --length;
            }
            return Decode(container.PositionPointer, length, ref container).TrimEnd();
        }

        public static unsafe string ExtractText<TChar>(ref YARGTextContainer<TChar> container, bool isChartFile)
            where TChar : unmanaged, IConvertible
        {
            long stringBegin = container.Position;
            long stringEnd = -1;
            if (isChartFile && !container.IsAtEnd() && container.Get() == '\"')
            {
                while (true)
                {
                    ++container.Position;
                    if (container.IsAtEnd())
                    {
                        break;
                    }

                    int ch = container.Get();
                    if (ch == '\n')
                    {
                        break;
                    }

                    if (stringEnd == -1)
                    {
                        if (ch == '\"' && container.PositionPointer[-1].ToInt32(null) != '\\')
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
                while (!container.IsAtEnd())
                {
                    int ch = container.Get();
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

            while (stringEnd > stringBegin && container.GetBuffer()[stringEnd - 1].ToInt32(null) <= WHITESPACE_LIMIT)
            {
                --stringEnd;
            }

            return Decode(container.GetBuffer() + stringBegin, stringEnd - stringBegin, ref container);
        }

        public static bool ExtractBoolean<TChar>(in YARGTextContainer<TChar> text)
            where TChar : unmanaged, IConvertible
        {
            return !text.IsAtEnd() && text.Get() switch
            {
                '1' => true,
                _ => text.Position + 4 <= text.Length &&
                    (text[0] | CharacterExtensions.ASCII_LOWERCASE_FLAG) is 't' &&
                    (text[1] | CharacterExtensions.ASCII_LOWERCASE_FLAG) is 'r' &&
                    (text[2] | CharacterExtensions.ASCII_LOWERCASE_FLAG) is 'u' &&
                    (text[3] | CharacterExtensions.ASCII_LOWERCASE_FLAG) is 'e',
            };
        }

        public static bool TryExtract<TChar, TNumber>(ref YARGTextContainer<TChar> text, out TNumber value)
            where TChar : unmanaged, IConvertible
            where TNumber : unmanaged, IComparable, IComparable<TNumber>, IConvertible, IEquatable<TNumber>, IFormattable
        {
            value = default;
            if (text.IsAtEnd())
            {
                return false;
            }

            int ch = text.Get();
            long sign = 1;

            switch (ch)
            {
                case '-':
                    if (!NumericalLimits<TNumber>.IS_SIGNED)
                    {
                        return false;
                    }
                    sign = -1;
                    goto case '+';
                case '+':
                    ++text.Position;
                    if (text.IsAtEnd())
                    {
                        return false;
                    }
                    ch = text.Get();
                    break;
            }

            if (ch < '0' || '9' < ch)
            {
                return false;
            }

            ulong tmp = 0;
            while (true)
            {
                tmp += (ulong) ch - '0';

                ++text.Position;
                if (text.IsAtEnd())
                {
                    break;
                }

                ch = text.Get();
                if (ch < '0' || '9' < ch)
                {
                    break;
                }

                if (!NumericalLimits<TNumber>.IS_SIGNED)
                {
                    const char LAST_DIGIT_UNSIGNED = '5';
                    if (tmp < NumericalLimits<TNumber>.UNSIGNED_SOFT_MAX || tmp == NumericalLimits<TNumber>.UNSIGNED_SOFT_MAX && ch <= LAST_DIGIT_UNSIGNED)
                    {
                        tmp *= 10;
                        continue;
                    }

                    while (!text.IsAtEnd())
                    {
                        ch = text.Get();
                        if (ch < '0' || '9' < ch)
                        {
                            break;
                        }
                        ++text.Position;
                    }
                    value = NumericalLimits<TNumber>.UNSIGNED_MAX;
                    return true;
                }
                else
                {
                    const char LAST_DIGIT_SIGNED = '7';
                    if (tmp < NumericalLimits<TNumber>.SIGNED_SOFT_MAX || tmp == NumericalLimits<TNumber>.SIGNED_SOFT_MAX && ch <= LAST_DIGIT_SIGNED)
                    {
                        tmp *= 10;
                        continue;
                    }

                    while (!text.IsAtEnd())
                    {
                        ch = text.Get();
                        if (ch < '0' || '9' < ch)
                        {
                            break;
                        }
                        ++text.Position;
                    }
                    value = sign == -1 ? NumericalLimits<TNumber>.SIGNED_MIN : NumericalLimits<TNumber>.SIGNED_MAX;
                    return true;
                }
            }

            unsafe
            {
                if (NumericalLimits<TNumber>.IS_SIGNED)
                {
                    long signed = (long) tmp * sign;
                    value = *(TNumber*) &signed;
                }
                else
                {
                    value = *(TNumber*) &tmp;
                }
            }
            return true;
        }

        public static bool TryExtract<TChar>(ref YARGTextContainer<TChar> text, out float value)
            where TChar : unmanaged, IConvertible
        {
            bool result = TryExtract(ref text, out double tmp);
            value = (float) tmp;
            return result;
        }

        public static bool TryExtract<TChar>(ref YARGTextContainer<TChar> text, out double value)
            where TChar : unmanaged, IConvertible
        {
            value = 0;
            if (text.IsAtEnd())
            {
                return false;
            }

            int ch = text.Get();
            double sign = ch == '-' ? -1 : 1;

            if (ch == '-' || ch == '+')
            {
                ++text.Position;
                if (text.IsAtEnd())
                {
                    return false;
                }
                ch = text.Get();
            }

            if (ch < '0' || '9' < ch && ch != '.')
            {
                return false;
            }

            while ('0' <= ch && ch <= '9')
            {
                value *= 10;
                value += ch - '0';
                ++text.Position;
                if (text.IsAtEnd())
                {
                    break;
                }
                ch = text.Get();
            }

            if (ch == '.')
            {
                ++text.Position;
                if (!text.IsAtEnd())
                {
                    double divisor = 1;
                    ch = text.Get();
                    while ('0' <= ch && ch <= '9')
                    {
                        divisor *= 10;
                        value += (ch - '0') / divisor;

                        ++text.Position;
                        if (text.IsAtEnd())
                        {
                            break;
                        }
                        ch = text.Get();
                    }
                }
            }

            value *= sign;
            return true;
        }

        public static bool TryExtractWithWhitespace<TChar, TNumber>(ref YARGTextContainer<TChar> text, out TNumber value)
            where TChar : unmanaged, IConvertible
            where TNumber : unmanaged, IComparable, IComparable<TNumber>, IConvertible, IEquatable<TNumber>, IFormattable
        {
            if (!TryExtract(ref text, out value))
            {
                return false;
            }
            SkipWhitespace(ref text);
            return true;
        }

        public static bool TryExtractWithWhitespace<TChar>(ref YARGTextContainer<TChar> text, out float value)
            where TChar : unmanaged, IConvertible
        {
            if (!TryExtract(ref text, out value))
            {
                return false;
            }
            SkipWhitespace(ref text);
            return true;
        }

        public static bool TryExtractWithWhitespace<TChar>(ref YARGTextContainer<TChar> text, out double value)
            where TChar : unmanaged, IConvertible
        {
            if (!TryExtract(ref text, out value))
            {
                return false;
            }
            SkipWhitespace(ref text);
            return true;
        }

        private static unsafe string Decode<TChar>(TChar* data, long count, ref YARGTextContainer<TChar> text)
            where TChar : unmanaged, IConvertible
        {
            while (true)
            {
                try
                {
                    return text.Encoding.GetString((byte*) data, (int) (count * sizeof(TChar)));
                }
                catch
                {
                    if (text.Encoding != UTF8Strict)
                    {
                        throw;
                    }
                    text.Encoding = Latin1;
                }
            }
        }

        private static class NumericalLimits<TNumber>
            where TNumber : unmanaged, IComparable, IComparable<TNumber>, IConvertible, IEquatable<TNumber>, IFormattable
        {
            public static readonly TNumber SIGNED_MAX;
            public static readonly ulong SIGNED_SOFT_MAX;
            public static readonly TNumber SIGNED_MIN;
            public static readonly TNumber UNSIGNED_MAX;
            public static readonly ulong UNSIGNED_SOFT_MAX;

            public static readonly bool IS_SIGNED;

            static unsafe NumericalLimits()
            {
                ulong ZERO = 0;
                ulong MAX = ulong.MaxValue >> ((8 - sizeof(TNumber)) * 8);
                UNSIGNED_MAX = *(TNumber*) &MAX;
                if (IS_SIGNED = UNSIGNED_MAX.CompareTo(*(TNumber*)&ZERO) < 0)
                {
                    ulong sMAX = (ulong)long.MaxValue >> ((8 - sizeof(TNumber)) * 8);
                    SIGNED_MAX = *(TNumber*) &sMAX;
                    sMAX /= 10;
                    SIGNED_SOFT_MAX = sMAX;

                    long sMin = long.MinValue >> ((8 - sizeof(TNumber)) * 8);
                    SIGNED_MIN = *(TNumber*) &sMin;
                }
                else
                {
                    MAX /= 10;
                    UNSIGNED_SOFT_MAX = MAX;
                }
            }
        }
    }
}
