﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Song.Deserialization
{
    public class YARGDTAReader : YARGTXTReader_Base
    {
        private readonly List<int> nodeEnds = new();
        //private static readonly Encoding Western = Encoding.GetEncoding(1252);
        

        public YARGDTAReader(byte[] data) : base(data)
        {
            SkipWhiteSpace();
        }

        public YARGDTAReader(string path) : this(File.ReadAllBytes(path)) { }

        public YARGDTAReader(YARGDTAReader reader) : base(reader.data)
        {
            _position = reader._position;
            _next = reader._next;
            nodeEnds.Add(reader.nodeEnds[0]);
        }

        public override byte SkipWhiteSpace()
        {
            while (_position < length)
            {
                byte ch = data[_position];
                if (ch > 32 && ch != ';')
                    return ch;

                ++_position;
                if (ch > 32)
                {
                    while (_position < length)
                    {
                        ++_position;
                        if (data[_position - 1] == '\n')
                            break;
                    }
                }
            }
            return 0;
        }

        public string GetNameOfNode()
        {
            byte ch = data[_position];
            if (ch == '(')
                return string.Empty;

            bool hasApostrophe = true;
            if (ch != '\'')
            {
                if (data[_position - 1] != '(')
                    throw new Exception("Invalid name call");
                hasApostrophe = false;
            }
            else
                ch = data[++_position];

            int start = _position;
            while (ch != '\'')
            {
                if (ch <= 32)
                {
                    if (hasApostrophe)
                        throw new Exception("Invalid name format");
                    break;
                }
                ch = data[++_position];
            }
            int end = _position++;
            SkipWhiteSpace();
            return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(data, start, end - start));
        }

        public string ExtractText()
        {
            byte ch = data[_position];
            bool inSquirley = ch == '{';
            bool inQuotes = !inSquirley && ch == '\"';
            bool inApostrophes = !inQuotes && ch == '\'';

            if (inSquirley || inQuotes || inApostrophes)
                ++_position;

            int start = _position++;
            while (_position < _next)
            {
                ch = data[_position];
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
                else if (ch <= 32)
                {
                    if (inApostrophes)
                        throw new Exception("Text error - no whitespace allowed");
                    if (!inSquirley && !inQuotes)
                        break;
                }
                ++_position;
            }

            int end = _position;
            if (_position != _next)
            {
                ++_position;
                SkipWhiteSpace();
            }
            else if (inSquirley || inQuotes || inApostrophes)
                throw new Exception("Improper end to text");

            return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(data, start, end - start)).Replace("\\q", "\"");
        }

        public List<int> ExtractList_Int()
        {
            List<int> values = new();
            while (data[_position] != ')')
                values.Add(ReadInt32());
            return values;
        }

        public List<float> ExtractList_Float()
        {
            List<float> values = new();
            while (data[_position] != ')')
                values.Add(ReadFloat());
            return values;
        }

        public List<string> ExtractList_String()
        {
            List<string> strings = new();
            while (data[_position] != ')')
                strings.Add(ExtractText());
            return strings;
        }

        public bool StartNode()
        {
            byte ch = data[_position];
            if (ch != '(')
                return false;

            ++_position;
            SkipWhiteSpace();

            int scopeLevel = 1;
            bool inApostropes = false;
            bool inQuotes = false;
            bool inComment = false;
            int pos = _position;
            while (scopeLevel >= 1 && pos < length)
            {
                ch = data[pos];
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
            _position = nodeEnds[index] + 1;
            nodeEnds.RemoveAt(index);
            if (index > 0)
                _next = nodeEnds[--index];
            SkipWhiteSpace();
        }
    };
}
