using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public sealed class YARGDTAReader : YARGBaseTextReader<byte>
    {
        private static readonly byte[] BOM_UTF8 = { 0xEF, 0xBB, 0xBF };
        private static readonly byte[] BOM_OTHER = { 0xFF, 0xFE };

        private readonly List<int> nodeEnds = new();
        public Encoding encoding;

        public YARGDTAReader(byte[] data) : base(data)
        {
            if (data[0] == BOM_UTF8[0] && data[1] == BOM_UTF8[1] && data[2] == BOM_UTF8[2])
            {
                encoding = Encoding.UTF8;
                Position += 3;
            }
            else if (data[0] == BOM_OTHER[0] && data[1] == BOM_OTHER[1])
            {
                if (data[2] == 0)
                {
                    encoding = Encoding.UTF32;
                    Position += 3;
                }
                else
                {
                    encoding = Encoding.Unicode;
                    Position += 2;
                }
            }
            else if (data[0] == BOM_OTHER[1] && data[1] == BOM_OTHER[0])
            {
                encoding = Encoding.BigEndianUnicode;
                Position += 2;
            }
            else
                encoding = YARGTextReader.Latin1;

            SkipWhiteSpace();
        }

        public YARGDTAReader(string path) : this(File.ReadAllBytes(path)) { }

        public YARGDTAReader(YARGDTAReader reader) : base(reader.Data)
        {
            Position = reader.Position;
            _next = reader._next;
            encoding = reader.encoding;
            nodeEnds.Add(reader.nodeEnds[0]);
        }

        public override char SkipWhiteSpace()
        {
            while (Position < Length)
            {
                char ch = (char)Data[Position];
                if (!ch.IsAsciiWhitespace() && ch != ';')
                    return ch;

                ++Position;
                if (!ch.IsAsciiWhitespace())
                {
                    while (Position < Length)
                    {
                        ++Position;
                        if (Data[Position - 1] == '\n')
                            break;
                    }
                }
            }
            return (char)0;
        }

        public string GetNameOfNode()
        {
            char ch = (char)Data[Position];
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
                ch = (char)Data[++Position];

            int start = Position;
            while (ch != '\'')
            {
                if (ch.IsAsciiWhitespace())
                {
                    if (hasApostrophe)
                        throw new Exception("Invalid name format");
                    break;
                }
                ch = (char)Data[++Position];
            }
            int end = Position++;
            SkipWhiteSpace();
            return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(Data, start, end - start));
        }

        public string ExtractText()
        {
            char ch = (char)Data[Position];
            bool inSquirley = ch == '{';
            bool inQuotes = !inSquirley && ch == '\"';
            bool inApostrophes = !inQuotes && ch == '\'';

            if (inSquirley || inQuotes || inApostrophes)
                ++Position;

            int start = Position++;
            while (Position < _next)
            {
                ch = (char)Data[Position];
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
            if (Position != _next)
            {
                ++Position;
                SkipWhiteSpace();
            }
            else if (inSquirley || inQuotes || inApostrophes)
                throw new Exception("Improper end to text");

            return encoding.GetString(new ReadOnlySpan<byte>(Data, start, end - start)).Replace("\\q", "\"");
        }

        public List<int> ExtractList_Int()
        {
            List<int> values = new();
            while (Data[Position] != ')')
                values.Add(YARGNumberExtractor.Int32(this));
            return values;
        }

        public List<float> ExtractList_Float()
        {
            List<float> values = new();
            while (Data[Position] != ')')
                values.Add(YARGNumberExtractor.Float(this));
            return values;
        }

        public List<string> ExtractList_String()
        {
            List<string> strings = new();
            while (Data[Position] != ')')
                strings.Add(ExtractText());
            return strings;
        }

        public bool StartNode()
        {
            if (Position >= Length)
                return false;

            byte ch = Data[Position];
            if (ch != '(')
                return false;

            ++Position;
            SkipWhiteSpace();

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
            _next = pos - 1;
            return true;
        }

        public void EndNode()
        {
            int index = nodeEnds.Count - 1;
            Position = nodeEnds[index] + 1;
            nodeEnds.RemoveAt(index);
            if (index > 0)
                _next = nodeEnds[--index];
            SkipWhiteSpace();
        }
    };
}
