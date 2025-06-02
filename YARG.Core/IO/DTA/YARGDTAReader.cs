using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public static class YARGDTAReader
    {
        public static YARGTextContainer<byte> Create(FixedArray<byte> data)
        {
            // If it doesn't throw with `At(1)`, then 0 and 1 are valid indices.
            // We can therefore skip bounds checking
            if ((data.At(1) == 0xFE && data[0] == 0xFF) || (data[0] == 0xFE && data[1] == 0xFF))
            {
                throw new Exception("UTF-16 & UTF-32 are not supported for .dta files");
            }

            var container = new YARGTextContainer<byte>(data, YARGTextReader.Latin1);
            // Same idea as above, but with index `2` instead
            if (data.At(2) == 0xBF && data[0] == 0xEF && data[1] == 0xBB)
            {
                container.Position += 3;
                container.Encoding = Encoding.UTF8;
            }
            return container;
        }

        public static int SkipWhitespace(ref YARGTextContainer<byte> container)
        {
            while (!container.IsAtEnd())
            {
                int ch = container.Get();
                if (ch > 32 && ch != ';')
                {
                    return ch;
                }

                ++container.Position;
                if (ch == ';')
                {
                    // In comment
                    while (!container.IsAtEnd() && ch != '\n')
                    {
                        ch = container.Get();
                        ++container.Position;
                    }
                }
            }
            return (char) 0;
        }

        /// <summary>
        /// Extracts a boolean and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The boolean or `false` on failed extraction</returns>
        public static bool ExtractBoolean(ref YARGTextContainer<byte> container)
        {
            bool result = YARGTextReader.ExtractBoolean(in container);
            SkipWhitespace(ref container);
            return result;
        }

        /// <summary>
        /// Extracts a boolean and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The boolean or `true` on failed extraction</returns>
        public static bool ExtractBoolean_FlippedDefault(ref YARGTextContainer<byte> container)
        {
            bool result = container.IsAtEnd() || container.Get() switch
            {
                '0' => false,
                _ => container.Position + 5 > container.Length ||
                    (container[0] | CharacterExtensions.ASCII_LOWERCASE_FLAG) != 'f' ||
                    (container[1] | CharacterExtensions.ASCII_LOWERCASE_FLAG) != 'a' ||
                    (container[2] | CharacterExtensions.ASCII_LOWERCASE_FLAG) != 'l' ||
                    (container[3] | CharacterExtensions.ASCII_LOWERCASE_FLAG) != 's' ||
                    (container[4] | CharacterExtensions.ASCII_LOWERCASE_FLAG) != 'e',
            };
            SkipWhitespace(ref container);
            return result;
        }

        /// <summary>
        /// Extracts a numerical value of the specified type and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The short</returns>
        public static TNumber ExtractInteger<TNumber>(ref YARGTextContainer<byte> container)
            where TNumber : unmanaged, IComparable, IComparable<TNumber>, IConvertible, IEquatable<TNumber>, IFormattable
        {
            if (!YARGTextReader.TryExtract(ref container, out TNumber value))
            {
                throw new Exception("Data for " + typeof(TNumber).Name + " not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        public static float ExtractFloat(ref YARGTextContainer<byte> container)
        {
            if (!YARGTextReader.TryExtract(ref container, out float value))
            {
                throw new Exception("Data for float not present");
            }
            SkipWhitespace(ref container);
            return value;
        }

        private enum TextScopeState
        {
            None,
            Squirlies,
            Quotes,
            Apostrophes,
            Comment
        }

        public static TextSpan ExtractTextBytes(ref YARGTextContainer<byte> container)
        {
            int ch = container.GetCurrentCharacter();
            var state = ch switch
            {
                '{' => TextScopeState.Squirlies,
                '\"' => TextScopeState.Quotes,
                '\'' => TextScopeState.Apostrophes,
                _ => TextScopeState.None
            };

            if (state != TextScopeState.None)
            {
                ++container.Position;
                ch = container.GetCurrentCharacter();
            }

            TextSpan span;
            unsafe
            {
                span = new TextSpan()
                {
                    ptr = container.GetBuffer() + container.Position,
                    length = 0
                };
            }

            while (true)
            {
                if (ch == '{')
                {
                    throw new Exception("Text error - no { braces allowed");
                }

                if (ch == '}')
                {
                    if (state == TextScopeState.Squirlies)
                    {
                        break;
                    }
                    throw new Exception("Text error - no \'}\' allowed");
                }
                else if (ch == '\"')
                {
                    if (state == TextScopeState.Quotes)
                    {
                        break;
                    }

                    if (state != TextScopeState.Squirlies)
                    {
                        throw new Exception("Text error - no quotes allowed");
                    }
                }
                else if (ch == '\'')
                {
                    if (state == TextScopeState.Apostrophes)
                    {
                        break;
                    }

                    if (state == TextScopeState.None)
                    {
                        throw new Exception("Text error - no apostrophes allowed");
                    }
                }
                else if (ch <= 32 || ch == ')')
                {
                    if (state == TextScopeState.None)
                    {
                        break;
                    }
                }
                ++span.length;
                ch = container.At(span.length);
            }

            container.Position += span.length;
            if (ch != ')')
            {
                ++container.Position;
            }
            SkipWhitespace(ref container);
            return span;
        }

        public static string ExtractText(ref YARGTextContainer<byte> container)
        {
            var span = ExtractTextBytes(ref container);
            return DecodeString(in span, Encoding.UTF8);
        }

        public static string DecodeString(in TextSpan span, Encoding encoding)
        {
            string str;
            try
            {
                str = span.GetString(encoding);
            }
            catch
            {
                if (encoding != YARGTextReader.UTF8Strict)
                {
                    throw;
                }
                str = span.GetString(YARGTextReader.Latin1);
            }
            return str.Replace("\\q", "\"");
        }

        public static TNumber[] ExtractIntegerArray<TNumber>(ref YARGTextContainer<byte> container)
            where TNumber : unmanaged, IComparable, IComparable<TNumber>, IConvertible, IEquatable<TNumber>, IFormattable
        {
            bool doEnd = StartNode(ref container);
            List<TNumber> values = new();
            while (container.GetCurrentCharacter() != ')')
            {
                values.Add(ExtractInteger<TNumber>(ref container));
            }

            if (doEnd)
            {
                EndNode(ref container);
            }
            return values.ToArray();
        }

        public static float[] ExtractFloatArray(ref YARGTextContainer<byte> container)
        {
            bool doEnd = StartNode(ref container);
            List<float> values = new();
            while (container.GetCurrentCharacter() != ')')
            {
                values.Add(ExtractFloat(ref container));
            }

            if (doEnd)
            {
                EndNode(ref container);
            }
            return values.ToArray();
        }

        public static string[] ExtractStringArray(ref YARGTextContainer<byte> container)
        {
            bool doEnd = StartNode(ref container);
            List<string> strings = new();
            while (container.GetCurrentCharacter() != ')')
            {
                strings.Add(ExtractText(ref container));
            }

            if (doEnd)
            {
                EndNode(ref container);
            }
            return strings.ToArray();
        }

        public static bool StartNode(ref YARGTextContainer<byte> container)
        {
            if (container.IsAtEnd() || container.Get() != '(')
            {
                return false;
            }

            ++container.Position;
            SkipWhitespace(ref container);
            return true;
        }

        public static string GetNameOfNode(ref YARGTextContainer<byte> container, bool allowNonAlphetical)
        {
            int ch = container.GetCurrentCharacter();
            if (ch == '(')
            {
                return string.Empty;
            }

            bool hasApostrophe = ch == '\'';
            if (hasApostrophe)
            {
                ++container.Position;
                ch = container.GetCurrentCharacter();
            }

            var start = container.Position;
            int length = 0;
            while (true)
            {
                if (ch == '\'')
                {
                    if (!hasApostrophe)
                    {
                        throw new Exception("Invalid name format");
                    }
                    container.Position += length + 1;
                    break;
                }

                if (ch <= 32)
                {
                    if (!hasApostrophe)
                    {
                        container.Position += length + 1;
                        break;
                    }
                }
                else if (!allowNonAlphetical && ch != '_')
                {
                    int cased = ch | CharacterExtensions.ASCII_LOWERCASE_FLAG;
                    if (cased < 'a' || 'z' < cased)
                    {
                        container.Position += length;
                        break;
                    }
                }

                ++length;
                if (container.Position + length == container.Length)
                {
                    container.Position = container.Length;
                    break;
                }
                ch = container[length];
            }

            SkipWhitespace(ref container);
            unsafe
            {
                return Encoding.UTF8.GetString(container.GetBuffer() + start, length);
            }
        }

        public static void EndNode(ref YARGTextContainer<byte> container)
        {
            int scopeLevel = 0;
            int squirlyCount = 0;
            var textState = TextScopeState.None;
            while (!container.IsAtEnd() && scopeLevel >= 0)
            {
                int curr = container.Get();
                ++container.Position;
                if (textState == TextScopeState.Comment)
                {
                    if (curr == '\n')
                    {
                        textState = TextScopeState.None;
                    }
                }
                else if (curr == '{')
                {
                    if (textState != TextScopeState.None && textState != TextScopeState.Squirlies)
                    {
                        throw new Exception("Invalid open-squirly found!");
                    }
                    textState = TextScopeState.Squirlies;
                    ++squirlyCount;
                }
                else if (curr == '}')
                {
                    if (textState != TextScopeState.Squirlies)
                    {
                        throw new Exception("Invalid close-squirly found!");
                    }
                    --squirlyCount;
                    if (squirlyCount == 0)
                    {
                        textState = TextScopeState.None;
                    }
                }
                else if (curr == '\"')
                {
                    switch (textState)
                    {
                        case TextScopeState.Apostrophes:
                            throw new Exception("Invalid quotation mark found!");
                        case TextScopeState.None:
                            textState = TextScopeState.Quotes;
                            break;
                        case TextScopeState.Quotes:
                            textState = TextScopeState.None;
                            break;
                    }
                }
                else if (textState == TextScopeState.None)
                {
                    switch (curr)
                    {
                        case '(': ++scopeLevel; break;
                        case ')': --scopeLevel; break;
                        case '\'': textState = TextScopeState.Apostrophes; break;
                        case ';': textState = TextScopeState.Comment; break;
                    }
                }
                else if (textState == TextScopeState.Apostrophes && curr == '\'')
                {
                    textState = TextScopeState.None;
                }
            }
            SkipWhitespace(ref container);
        }
    };
}
