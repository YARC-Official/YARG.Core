﻿using System;
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

            int position = data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            return new YARGDTAReader(data, position);
        }

        private readonly YARGTextContainer<byte> container;
        public Encoding encoding;

        private YARGDTAReader(byte[] data, int position)
        {
            container = new YARGTextContainer<byte>(data, position);
            encoding = position == 3 ? Encoding.UTF8 : YARGTextContainer.Latin1;
            SkipWhitespace();
        }

        public YARGDTAReader Clone()
        {
            return new YARGDTAReader(this);
        }

        private YARGDTAReader(YARGDTAReader reader)
        {
            container = new(reader.container);
            encoding = reader.encoding;
        }

        public char SkipWhitespace()
        {
            while (container.Position < container.Length)
            {
                char ch = (char) container.Data[container.Position];
                if (ch > 32 && ch != ';')
                {
                    return ch;
                }

                ++container.Position;
                if (ch > 32)
                {
                    // In comment
                    while (container.Position < container.Length)
                    {
                        if (container.Data[container.Position++] == '\n')
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
            char ch = (char)container.Data[container.Position];
            if (ch == '(')
            {
                return string.Empty;
            }

            bool hasApostrophe = ch == '\'';
            if (hasApostrophe)
            {
                ++container.Position;
                if (container.Position >= container.Length)
                {
                    throw new EndOfStreamException();
                }
                ch = (char) container.Data[container.Position];
            }

            int start = container.Position;
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
                ++container.Position;
                ch = (char) container.Data[container.Position];
            }

            int end = container.Position++;
            SkipWhitespace();
            return Encoding.UTF8.GetString(container.Data, start, end - start);
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
            if (container.Position >= container.Length)
            {
                throw new EndOfStreamException();
            }

            char ch = (char)container.Data[container.Position];
            var state = ch switch
            {
                '{'  => TextScopeState.Squirlies,
                '\"' => TextScopeState.Quotes,
                '\'' => TextScopeState.Apostrophes,
                _    => TextScopeState.None
            };

            if (state != TextScopeState.None)
            {
                ++container.Position;
                if (container.Position >= container.Length)
                {
                    throw new EndOfStreamException();
                }
                ch = (char) container.Data[container.Position];
            }

            var start = container.Position;
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
                ++container.Position;
                if (container.Position >= container.Length)
                {
                    throw new EndOfStreamException();
                }
                ch = (char) container.Data[container.Position];
            }

            string txt = encoding.GetString(container.Data, start, container.Position - start).Replace("\\q", "\"");
            if (ch != ')')
            {
                ++container.Position;

            }
            SkipWhitespace();
            return txt;
        }

        public int[] ExtractArray_Int()
        {
            bool doEnd = StartNode();
            List<int> values = new();
            while (container.Data[container.Position] != ')')
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
            while (container.Data[container.Position] != ')')
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
            while (container.Data[container.Position] != ')')
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
            if (container.Position >= container.Length || container.Data[container.Position] != '(')
            {
                return false;
            }

            ++container.Position;
            SkipWhitespace();
            return true;
        }

        public void EndNode()
        {
            int scopeLevel = 0;
            bool inApostropes = false;
            bool inQuotes = false;
            bool inComment = false;
            while (container.Position < container.Length && scopeLevel >= 0)
            {
                char curr = (char) container.Data[container.Position];
                ++container.Position;
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
            bool result = container.ExtractBoolean();
            SkipWhitespace();
            return result;
        }

        public short ExtractInt16()
        {
            if (!container.TryExtractInt16(out short value))
            {
                throw new Exception("Data for Int16 not present");
            }
            SkipWhitespace();
            return value;
        }

        public ushort ExtractUInt16()
        {
            if (!container.TryExtractUInt16(out ushort value))
            {
                throw new Exception("Data for UInt16 not present");
            }
            SkipWhitespace();
            return value;
        }

        public int ExtractInt32()
        {
            if (!container.TryExtractInt32(out int value))
            {
                throw new Exception("Data for Int32 not present");
            }
            SkipWhitespace();
            return value;
        }
        public uint ExtractUInt32()
        {
            if (!container.TryExtractUInt32(out uint value))
            {
                throw new Exception("Data for UInt32 not present");
            }
            SkipWhitespace();
            return value;
        }

        public long ExtractInt64()
        {
            if (!container.TryExtractInt64(out long value))
            {
                throw new Exception("Data for Int64 not present");
            }
            SkipWhitespace();
            return value;
        }

        public ulong ExtractUInt64()
        {
            if (!container.TryExtractUInt64(out ulong value))
            {
                throw new Exception("Data for UInt64 not present");
            }
            SkipWhitespace();
            return value;
        }

        public float ExtractFloat()
        {
            if (!container.TryExtractFloat(out float value))
            {
                throw new Exception("Data for float not present");
            }
            SkipWhitespace();
            return value;
        }

        public double ExtractDouble()
        {
            if (!container.TryExtractDouble(out double value))
            {
                throw new Exception("Data for double not present");
            }
            SkipWhitespace();
            return value;
        }
    };
}
