using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.IO.Disposables;
using YARG.Core.Logging;

namespace YARG.Core.IO
{
    public unsafe struct YARGDTAReader
    {
        public static bool TryCreate(FixedArray<byte> data, out YARGDTAReader reader)
        {
            if ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF))
            {
                YargLogger.LogError("UTF-16 & UTF-32 are not supported for .dta files");
                reader = default;
                return false;
            }

            reader = data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF
                ? new YARGDTAReader(data, 3, Encoding.UTF8)
                : new YARGDTAReader(data, 0, YARGTextContainer.Latin1);
            return true;
        }

        private YARGTextContainer<byte> _container;
        public Encoding Encoding;

        private YARGDTAReader(FixedArray<byte> data, int position, Encoding encoding)
        {
            _container = new YARGTextContainer<byte>(data, position);
            Encoding = encoding;
            SkipWhitespace();
        }

        public char SkipWhitespace()
        {
            while (_container.Position < _container.End)
            {
                char ch = (char) *_container.Position;
                if (ch > 32 && ch != ';')
                {
                    return ch;
                }

                ++_container.Position;
                if (ch > 32)
                {
                    // In comment
                    while (_container.Position < _container.End)
                    {
                        if (*_container.Position++ == '\n')
                        {
                            break;
                        }
                    }
                }
            }
            return (char) 0;
        }

        public unsafe string GetNameOfNode(bool allowNonAlphetical)
        {
            char ch = (char) _container.CurrentValue;
            if (ch == '(')
            {
                return string.Empty;
            }

            bool hasApostrophe = ch == '\'';
            if (hasApostrophe)
            {
                ++_container.Position;
                ch = (char) _container.CurrentValue;
            }

            var start = _container.Position;
            var end = _container.Position;
            while (true)
            {
                if (ch == '\'')
                {
                    if (!hasApostrophe)
                    {
                        throw new Exception("Invalid name format");
                    }
                    _container.Position = end + 1;
                    break;
                }

                if (ch <= 32)
                {
                    if (hasApostrophe)
                    {
                        throw new Exception("Invalid name format");
                    }
                    _container.Position = end + 1;
                    break;
                }

                if (!allowNonAlphetical && !ch.IsAsciiLetter() && ch != '_')
                {
                    _container.Position = end;
                    break;
                }
                
                ++end;
                if (end >= _container.End)
                {
                    _container.Position = end;
                    break;
                }
                ch = (char) *end;
            }

            SkipWhitespace();
            return Encoding.UTF8.GetString(start, (int) (end - start));
        }

        private enum TextScopeState
        {
            None,
            Squirlies,
            Quotes,
            Apostrophes
        }

        public unsafe string ExtractText()
        {
            char ch = (char) _container.CurrentValue;
            var state = ch switch
            {
                '{' => TextScopeState.Squirlies,
                '\"' => TextScopeState.Quotes,
                '\'' => TextScopeState.Apostrophes,
                _ => TextScopeState.None
            };

            if (state != TextScopeState.None)
            {
                ++_container.Position;
                ch = (char) _container.CurrentValue;
            }

            var start = _container.Position;
            // Loop til the end of the text is found
            while (true)
            {
                if (ch == '{')
                    throw new Exception("Text error - no { braces allowed");

                if (ch == '}')
                {
                    if (state == TextScopeState.Squirlies)
                        break;
                    throw new Exception("Text error - no \'}\' allowed");
                }
                else if (ch == '\"')
                {
                    if (state == TextScopeState.Quotes)
                        break;
                    if (state != TextScopeState.Squirlies)
                        throw new Exception("Text error - no quotes allowed");
                }
                else if (ch == '\'')
                {
                    if (state == TextScopeState.Apostrophes)
                        break;
                    if (state == TextScopeState.None)
                        throw new Exception("Text error - no apostrophes allowed");
                }
                else if (ch <= 32 || ch == ')')
                {
                    if (state == TextScopeState.None)
                        break;
                    if (state == TextScopeState.Apostrophes)
                        throw new Exception("Text error - no whitespace allowed");
                }
                ++_container.Position;
                ch = (char) _container.CurrentValue;
            }

            string txt = Encoding.GetString(start, (int) (_container.Position - start)).Replace("\\q", "\"");
            if (ch != ')')
            {
                ++_container.Position;

            }
            SkipWhitespace();
            return txt;
        }

        public int[] ExtractArray_Int()
        {
            bool doEnd = StartNode();
            List<int> values = new();
            while (_container.CurrentValue != ')')
            {
                values.Add(ExtractInt32());
            }

            if (doEnd)
            {
                EndNode();
            }
            return values.ToArray();
        }

        public float[] ExtractArray_Float()
        {
            bool doEnd = StartNode();
            List<float> values = new();
            while (_container.CurrentValue != ')')
            {
                values.Add(ExtractFloat());
            }

            if (doEnd)
            {
                EndNode();
            }
            return values.ToArray();
        }

        public string[] ExtractArray_String()
        {
            bool doEnd = StartNode();
            List<string> strings = new();
            while (_container.CurrentValue != ')')
            {
                strings.Add(ExtractText());
            }

            if (doEnd)
            {
                EndNode();
            }
            return strings.ToArray();
        }

        public bool StartNode()
        {
            if (_container.Position >= _container.End || !_container.IsCurrentCharacter('('))
            {
                return false;
            }

            ++_container.Position;
            SkipWhitespace();
            return true;
        }

        public void EndNode()
        {
            int scopeLevel = 0;
            bool inApostropes = false;
            bool inQuotes = false;
            bool inComment = false;
            while (_container.Position < _container.End && scopeLevel >= 0)
            {
                char curr = (char) *_container.Position;
                ++_container.Position;
                if (inComment)
                {
                    if (curr == '\n')
                    {
                        inComment = false;
                    }
                }
                else if (curr == '\"')
                {
                    if (inApostropes)
                    {
                        throw new Exception("Ah hell nah wtf");
                    }
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes)
                {
                    if (!inApostropes)
                    {
                        switch (curr)
                        {
                            case '(': ++scopeLevel; break;
                            case ')': --scopeLevel; break;
                            case '\'': inApostropes = true; break;
                            case ';': inComment = true; break;
                        }
                    }
                    else if (curr == '\'')
                    {
                        inApostropes = false;
                    }
                }
            }
            SkipWhitespace();
        }

        /// <summary>
        /// Extracts a boolean and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The boolean or `false` on failed extraction</returns>
        public bool ExtractBoolean()
        {
            bool result = _container.ExtractBoolean();
            SkipWhitespace();
            return result;
        }
        
        /// <summary>
        /// Extracts a boolean and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The boolean or `true` on failed extraction</returns>
        public bool ExtractBoolean_FlippedDefault()
        {
            bool result = _container.Position >= _container.End || (char)*_container.Position switch
            {
                '0' => false,
                '1' => true,
                _ => _container.Position + 5 > _container.End ||
                    char.ToLowerInvariant((char)_container.Position[0]) != 'f' ||
                    char.ToLowerInvariant((char)_container.Position[1]) != 'a' ||
                    char.ToLowerInvariant((char)_container.Position[2]) != 'l' ||
                    char.ToLowerInvariant((char)_container.Position[3]) != 's' ||
                    char.ToLowerInvariant((char)_container.Position[4]) != 'e',
            };
            SkipWhitespace();
            return result;
        }

        /// <summary>
        /// Extracts a short and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The short</returns>
        public short ExtractInt16()
        {
            if (!_container.TryExtractInt16(out short value))
            {
                throw new Exception("Data for Int16 not present");
            }
            SkipWhitespace();
            return value;
        }

        /// <summary>
        /// Extracts a ushort and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The ushort</returns>
        public ushort ExtractUInt16()
        {
            if (!_container.TryExtractUInt16(out ushort value))
            {
                throw new Exception("Data for UInt16 not present");
            }
            SkipWhitespace();
            return value;
        }

        /// <summary>
        /// Extracts a int and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The int</returns>
        public int ExtractInt32()
        {
            if (!_container.TryExtractInt32(out int value))
            {
                throw new Exception("Data for Int32 not present");
            }
            SkipWhitespace();
            return value;
        }

        /// <summary>
        /// Extracts a uint and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The uint</returns>
        public uint ExtractUInt32()
        {
            if (!_container.TryExtractUInt32(out uint value))
            {
                throw new Exception("Data for UInt32 not present");
            }
            SkipWhitespace();
            return value;
        }

        /// <summary>
        /// Extracts a long and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The long</returns>
        public long ExtractInt64()
        {
            if (!_container.TryExtractInt64(out long value))
            {
                throw new Exception("Data for Int64 not present");
            }
            SkipWhitespace();
            return value;
        }

        /// <summary>
        /// Extracts a ulong and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The ulong</returns>
        public ulong ExtractUInt64()
        {
            if (!_container.TryExtractUInt64(out ulong value))
            {
                throw new Exception("Data for UInt64 not present");
            }
            SkipWhitespace();
            return value;
        }

        /// <summary>
        /// Extracts a float and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The float</returns>
        public float ExtractFloat()
        {
            if (!_container.TryExtractFloat(out float value))
            {
                throw new Exception("Data for float not present");
            }
            SkipWhitespace();
            return value;
        }

        /// <summary>
        /// Extracts a double and skips the following whitespace
        /// </summary>
        /// <remarks>Throws if no value could be parsed</remarks>
        /// <returns>The double</returns>
        public double ExtractDouble()
        {
            if (!_container.TryExtractDouble(out double value))
            {
                throw new Exception("Data for double not present");
            }
            SkipWhitespace();
            return value;
        }
    };
}
