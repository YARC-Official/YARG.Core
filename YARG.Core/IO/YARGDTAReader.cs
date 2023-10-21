using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public sealed class YARGDTAReader : YARGTextContainer<byte>
    {
        public static YARGDTAReader? TryCreate(CONFileListing listing)
        {
            try
            {
                var bytes = listing.LoadAllBytes();
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

        private readonly List<int> nodeEnds = new();
        public Encoding encoding;

        private YARGDTAReader(byte[] data, int position) : base(data, position)
        {
            encoding = Position == 3 ? Encoding.UTF8 : YARGTextContainer.Latin1;
            SkipWhitespace();
        }

        public YARGDTAReader(YARGDTAReader reader) : base(reader)
        {
            encoding = reader.encoding;
            nodeEnds.Add(reader.nodeEnds[0]);
        }

        public override char SkipWhitespace()
        {
            while (Position < Length)
            {
                char ch = (char) Current;
                if (!ch.IsAsciiWhitespace() && ch != ';')
                    return ch;

                ++Position;
                if (!ch.IsAsciiWhitespace())
                {
                    while (Position < Length)
                    {
                        if (Data[Position++] == '\n')
                            break;
                    }
                }
            }
            return (char) 0;
        }

        public string GetNameOfNode()
        {
            char ch = (char)Current;
            if (ch == '(')
                return string.Empty;

            bool hasApostrophe = true;
            if (ch != '\'')
            {
                if (Data[Position - 1] != '(')
                    throw new Exception("Invalid name call");
                hasApostrophe = false;
            }
            else
                ch = (char) Data[++Position];

            int start = Position;
            while (ch != '\'')
            {
                if (ch.IsAsciiWhitespace())
                {
                    if (hasApostrophe)
                        throw new Exception("Invalid name format");
                    break;
                }
                ch = (char) Data[++Position];
            }
            int end = Position++;
            SkipWhitespace();
            return Encoding.UTF8.GetString(Slice(start, end - start));
        }

        public string ExtractText()
        {
            char ch = (char)Current;
            bool inSquirley = ch == '{';
            bool inQuotes = !inSquirley && ch == '\"';
            bool inApostrophes = !inQuotes && ch == '\'';

            if (inSquirley || inQuotes || inApostrophes)
                ++Position;

            int start = Position++;
            while (Position < Next)
            {
                ch = (char)Current;
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
                ++Position;
            }

            int end = Position;
            if (Position != Next)
            {
                ++Position;
                SkipWhitespace();
            }
            else if (inSquirley || inQuotes || inApostrophes)
                throw new Exception("Improper end to text");

            return encoding.GetString(Slice(start, end - start)).Replace("\\q", "\"");
        }

        public List<int> ExtractList_Int()
        {
            List<int> values = new();
            while (Current != ')')
                values.Add(ExtractInt32());
            return values;
        }

        public List<float> ExtractList_Float()
        {
            List<float> values = new();
            while (Current != ')')
                values.Add(ExtractFloat());
            return values;
        }

        public List<string> ExtractList_String()
        {
            List<string> strings = new();
            while (Current != ')')
                strings.Add(ExtractText());
            return strings;
        }

        public bool StartNode()
        {
            if (Position >= Length)
                return false;

            byte ch = Current;
            if (ch != '(')
                return false;

            ++Position;
            SkipWhitespace();

            int scopeLevel = 1;
            bool inApostropes = false;
            bool inQuotes = false;
            bool inComment = false;
            int pos = Position;
            while (scopeLevel >= 1 && pos < Length)
            {
                ch = Data[pos];
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
            Next = pos - 1;
            return true;
        }

        public void EndNode()
        {
            int index = nodeEnds.Count - 1;
            Position = nodeEnds[index] + 1;
            nodeEnds.RemoveAt(index);
            if (index > 0)
                Next = nodeEnds[--index];
            SkipWhitespace();
        }
    };
}
