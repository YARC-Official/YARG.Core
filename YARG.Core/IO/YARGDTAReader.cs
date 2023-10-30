using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public sealed class YARGDTAReader
    {
        public static YARGDTAReader? TryCreate(CONFileListing listing, SharedCONStream CONFile)
        {
            try
            {
                var bytes = listing.LoadAllBytes(CONFile);
                return TryCreate(bytes);
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, $"Error while loading {listing.ConFile.FullName}");
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
                YargTrace.LogException(ex, $"Error while loading {filename}");
                return null;
            }
        }

        private static YARGDTAReader? TryCreate(byte[] data)
        {
            if ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF))
            {
                YargTrace.LogError("UTF-16 & UTF-32 are not supported for .dta files");
                return null;
            }

            int position = data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            return new YARGDTAReader(data, position);
        }

        private readonly YARGTextContainer<byte> container;
        private readonly List<int> nodeEnds = new();
        public Encoding encoding;

        private YARGDTAReader(byte[] data, int position)
        {
            container = new YARGTextContainer<byte>(data, position);
            encoding = container.Position == 3 ? Encoding.UTF8 : YARGTextContainer.Latin1;
            SkipWhitespace();
        }

        public YARGDTAReader(YARGDTAReader reader)
        {
            container = new(reader.container);
            encoding = reader.encoding;
            nodeEnds.Add(reader.nodeEnds[0]);
        }

        public char SkipWhitespace()
        {
            while (container.Position < container.Length)
            {
                char ch = (char) container.Data[container.Position];
                if (!ch.IsAsciiWhitespace() && ch != ';')
                    return ch;

                ++container.Position;
                if (!ch.IsAsciiWhitespace())
                {
                    while (container.Position < container.Length)
                    {
                        if (container.Data[container.Position++] == '\n')
                            break;
                    }
                }
            }
            return (char) 0;
        }

        public string GetNameOfNode()
        {
            char ch = (char)container.Data[container.Position];
            if (ch == '(')
                return string.Empty;

            bool hasApostrophe = true;
            if (ch != '\'')
            {
                if (container.Data[container.Position - 1] != '(')
                    throw new Exception("Invalid name call");
                hasApostrophe = false;
            }
            else
                ch = (char) container.Data[++container.Position];

            int start = container.Position;
            while (ch != '\'')
            {
                if (ch.IsAsciiWhitespace())
                {
                    if (hasApostrophe)
                        throw new Exception("Invalid name format");
                    break;
                }
                ch = (char) container.Data[++container.Position];
            }
            int end = container.Position++;
            SkipWhitespace();
            return Encoding.UTF8.GetString(container.Data, start, end - start);
        }

        public string ExtractText()
        {
            char ch = (char)container.Data[container.Position];
            bool inSquirley = ch == '{';
            bool inQuotes = !inSquirley && ch == '\"';
            bool inApostrophes = !inQuotes && ch == '\'';

            if (inSquirley || inQuotes || inApostrophes)
                ++container.Position;

            int start = container.Position++;
            while (container.Position < container.Next)
            {
                ch = (char)container.Data[container.Position];
                if (ch == '{')
                    throw new Exception("Text error - no { braces allowed");

                if (ch == '}')
                {
                    if (inSquirley)
                        break;
                    throw new Exception("Text error - no \'}\' allowed");
                }
                else if (ch == '\"')
                {
                    if (inQuotes)
                        break;
                    if (!inSquirley)
                        throw new Exception("Text error - no quotes allowed");
                }
                else if (ch == '\'')
                {
                    if (inApostrophes)
                        break;
                    if (!inSquirley && !inQuotes)
                        throw new Exception("Text error - no apostrophes allowed");
                }
                else if (ch.IsAsciiWhitespace())
                {
                    if (inApostrophes)
                        throw new Exception("Text error - no whitespace allowed");
                    if (!inSquirley && !inQuotes)
                        break;
                }
                ++container.Position;
            }

            int end = container.Position;
            if (container.Position != container.Next)
            {
                ++container.Position;
                SkipWhitespace();
            }
            else if (inSquirley || inQuotes || inApostrophes)
                throw new Exception("Improper end to text");

            return encoding.GetString(container.Data, start, end - start).Replace("\\q", "\"");
        }

        public List<int> ExtractList_Int()
        {
            List<int> values = new();
            while (container.Data[container.Position] != ')')
                values.Add(ExtractInt32());
            return values;
        }

        public List<float> ExtractList_Float()
        {
            List<float> values = new();
            while (container.Data[container.Position] != ')')
                values.Add(ExtractFloat());
            return values;
        }

        public List<string> ExtractList_String()
        {
            List<string> strings = new();
            while (container.Data[container.Position] != ')')
                strings.Add(ExtractText());
            return strings;
        }

        public bool StartNode()
        {
            if (container.Position >= container.Length)
                return false;

            byte ch = container.Data[container.Position];
            if (ch != '(')
                return false;

            ++container.Position;
            SkipWhitespace();

            int scopeLevel = 1;
            bool inApostropes = false;
            bool inQuotes = false;
            bool inComment = false;
            int pos = container.Position;
            while (scopeLevel >= 1 && pos < container.Length)
            {
                ch = container.Data[pos];
                if (inComment)
                {
                    if (ch == '\n')
                        inComment = false;
                }
                else if (ch == '\"')
                {
                    if (inApostropes)
                        throw new Exception("Ah hell nah wtf");
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes)
                {
                    if (!inApostropes)
                    {
                        if (ch == '(')
                            ++scopeLevel;
                        else if (ch == ')')
                            --scopeLevel;
                        else if (ch == '\'')
                            inApostropes = true;
                        else if (ch == ';')
                            inComment = true;
                    }
                    else if (ch == '\'')
                        inApostropes = false;
                }
                ++pos;
            }
            nodeEnds.Add(pos - 1);
            container.Next = pos - 1;
            return true;
        }

        public void EndNode()
        {
            int index = nodeEnds.Count - 1;
            container.Position = nodeEnds[index] + 1;
            nodeEnds.RemoveAt(index);
            if (index > 0)
                container.Next = nodeEnds[--index];
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
            short result = container.ExtractInt16();
            SkipWhitespace();
            return result;
        }

        public ushort ExtractUInt16()
        {
            ushort result = container.ExtractUInt16();
            SkipWhitespace();
            return result;
        }

        public int ExtractInt32()
        {
            int result = container.ExtractInt32();
            SkipWhitespace();
            return result;
        }
        public uint ExtractUInt32()
        {
            uint result = container.ExtractUInt32();
            SkipWhitespace();
            return result;
        }

        public long ExtractInt64()
        {
            long result = container.ExtractInt64();
            SkipWhitespace();
            return result;
        }

        public ulong ExtractUInt64()
        {
            ulong result = container.ExtractUInt64();
            SkipWhitespace();
            return result;
        }

        public float ExtractFloat()
        {
            float result = container.ExtractFloat();
            SkipWhitespace();
            return result;
        }

        public double ExtractDouble()
        {
            double result = container.ExtractDouble();
            SkipWhitespace();
            return result;
        }
    };
}
