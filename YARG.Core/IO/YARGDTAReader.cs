using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.Logging;

namespace YARG.Core.IO
{
    public sealed class YARGDTAReader
    {
        public static YARGDTAReader? TryCreate(CONFileListing listing, Stream stream)
        {
            try
            {
                var bytes = listing.LoadAllBytes(stream);
                return TryCreate(bytes);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {listing.ConFile.FullName}");
                return null;
            }
        }

        public static YARGDTAReader? TryCreate(string filename)
        {
            try
            {
                var bytes = File.ReadAllBytes(filename);
                return TryCreate(bytes);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {filename}");
                return null;
            }
        }

        private static YARGDTAReader? TryCreate(byte[] data)
        {
            if ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF))
            {
                YargLogger.LogError("UTF-16 & UTF-32 are not supported for .dta files");
                return null;
            }

            YARGTextContainer<byte> container;
            Encoding encoding;
            if (data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                container = new YARGTextContainer<byte>(data, 3);
                encoding = Encoding.UTF8;
            }
            else
            {
                container = new YARGTextContainer<byte>(data, 0);
                encoding = YARGTextContainer.Latin1;
            }
            return new YARGDTAReader(in container, encoding);
        }

        private YARGTextContainer<byte> _container;
        public Encoding Encoding;

        private YARGDTAReader(in YARGTextContainer<byte> container, Encoding encoding)
        {
            _container = container;
            Encoding = encoding;
            SkipWhitespace();
        }

        public YARGDTAReader Clone()
        {
            return new YARGDTAReader(_container, Encoding);
        }

        public char SkipWhitespace()
        {
            while (_container.Position < _container.Length)
            {
                char ch = (char) _container.Data[_container.Position];
                if (ch > 32 && ch != ';')
                {
                    return ch;
                }

                ++_container.Position;
                if (ch > 32)
                {
                    // In comment
                    while (_container.Position < _container.Length)
                    {
                        if (_container.Data[_container.Position++] == '\n')
                        {
                            break;
                        }
                    }
                }
            }
            return (char) 0;
        }

        public string GetNameOfNode(bool allowNonAlphetical)
        {
            char ch = (char)_container.Data[_container.Position];
            if (ch == '(')
            {
                return string.Empty;
            }

            bool hasApostrophe = ch == '\'';
            if (hasApostrophe)
            {
                ++_container.Position;
                if (_container.Position >= _container.Length)
                {
                    throw new EndOfStreamException();
                }
                ch = (char) _container.Data[_container.Position];
            }

            int start = _container.Position;
            while (ch != '\'')
            {
                if (ch <= 32)
                {
                    if (hasApostrophe)
                    {
                        throw new Exception("Invalid name format");
                    }
                    break;
                }

                if (!allowNonAlphetical && !ch.IsAsciiLetter() && ch != '_')
                {
                    break;
                }
                ++_container.Position;
                ch = (char) _container.Data[_container.Position];
            }

            int end = _container.Position++;
            SkipWhitespace();
            return Encoding.UTF8.GetString(_container.Data, start, end - start);
        }

        private enum TextScopeState
        {
            None,
            Squirlies,
            Quotes,
            Apostrophes
        }

        public string ExtractText()
        {
            if (_container.Position >= _container.Length)
            {
                throw new EndOfStreamException();
            }

            char ch = (char)_container.Data[_container.Position];
            var state = ch switch
            {
                '{'  => TextScopeState.Squirlies,
                '\"' => TextScopeState.Quotes,
                '\'' => TextScopeState.Apostrophes,
                _    => TextScopeState.None
            };

            if (state != TextScopeState.None)
            {
                ++_container.Position;
                if (_container.Position >= _container.Length)
                {
                    throw new EndOfStreamException();
                }
                ch = (char) _container.Data[_container.Position];
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
                if (_container.Position >= _container.Length)
                {
                    throw new EndOfStreamException();
                }
                ch = (char) _container.Data[_container.Position];
            }

            string txt = Encoding.GetString(_container.Data, start, _container.Position - start).Replace("\\q", "\"");
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
            while (_container.Data[_container.Position] != ')')
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
            while (_container.Data[_container.Position] != ')')
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
            while (_container.Data[_container.Position] != ')')
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
            if (_container.Position >= _container.Length || _container.Data[_container.Position] != '(')
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
            while (_container.Position < _container.Length && scopeLevel >= 0)
            {
                char curr = (char) _container.Data[_container.Position];
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

        public bool ExtractBoolean()
        {
            bool result = _container.ExtractBoolean();
            SkipWhitespace();
            return result;
        }

        public short ExtractInt16()
        {
            if (!_container.TryExtractInt16(out short value))
            {
                throw new Exception("Data for Int16 not present");
            }
            SkipWhitespace();
            return value;
        }

        public ushort ExtractUInt16()
        {
            if (!_container.TryExtractUInt16(out ushort value))
            {
                throw new Exception("Data for UInt16 not present");
            }
            SkipWhitespace();
            return value;
        }

        public int ExtractInt32()
        {
            if (!_container.TryExtractInt32(out int value))
            {
                throw new Exception("Data for Int32 not present");
            }
            SkipWhitespace();
            return value;
        }
        public uint ExtractUInt32()
        {
            if (!_container.TryExtractUInt32(out uint value))
            {
                throw new Exception("Data for UInt32 not present");
            }
            SkipWhitespace();
            return value;
        }

        public long ExtractInt64()
        {
            if (!_container.TryExtractInt64(out long value))
            {
                throw new Exception("Data for Int64 not present");
            }
            SkipWhitespace();
            return value;
        }

        public ulong ExtractUInt64()
        {
            if (!_container.TryExtractUInt64(out ulong value))
            {
                throw new Exception("Data for UInt64 not present");
            }
            SkipWhitespace();
            return value;
        }

        public float ExtractFloat()
        {
            if (!_container.TryExtractFloat(out float value))
            {
                throw new Exception("Data for float not present");
            }
            SkipWhitespace();
            return value;
        }

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
