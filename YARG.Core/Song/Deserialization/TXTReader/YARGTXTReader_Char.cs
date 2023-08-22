using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Song.Deserialization
{
    public class YARGTXTReader_Char : YARGTXTReader_BaseChar, ITXTReader
    {
        static YARGTXTReader_Char() { }

        public YARGTXTReader_Char(char[] data) : base(data)
        {
            SkipWhiteSpace();
            SetNextPointer();
            if (data[_position] == '\n')
                GotoNextLine();
        }

        public YARGTXTReader_Char(string path) : this(File.ReadAllText(path).ToCharArray()) { }

        public override char SkipWhiteSpace()
        {
            while (_position < length)
            {
                char ch = data[_position];
                if (IsWhitespace(ch))
                {
                    if (ch == '\n')
                        return ch;
                }
                else if (ch != '=')
                    return ch;
                ++_position;
            }

            return (char) 0;
        }

        public void GotoNextLine()
        {
            char curr;
            do
            {
                _position = _next;
                if (_position >= length)
                    break;

                _position++;
                curr = SkipWhiteSpace();

                if (_position == length)
                    break;

                if (data[_position] == '{')
                {
                    _position++;
                    curr = SkipWhiteSpace();
                }

                SetNextPointer();
            } while (curr == '\n' || curr == '/' && data[_position + 1] == '/');
        }

        public void SetNextPointer()
        {
            _next = _position;
            while (_next < length && data[_next] != '\n')
                ++_next;
        }

        public string ExtractText(bool checkForQuotes = true)
        {
            (int, int) boundaries = new(_position, _next);
            if (boundaries.Item2 == length)
                --boundaries.Item2;

            if (checkForQuotes && data[_position] == '\"')
            {
                int end = boundaries.Item2 - 1;
                while (_position + 1 < end && IsWhitespace(data[end]))
                    --end;

                if (_position < end && data[end] == '\"' && data[end - 1] != '\\')
                {
                    ++boundaries.Item1;
                    boundaries.Item2 = end;
                }
            }

            if (boundaries.Item2 < boundaries.Item1)
                return string.Empty;

            while (boundaries.Item2 > boundaries.Item1 && IsWhitespace(data[boundaries.Item2 - 1]))
                --boundaries.Item2;

            _position = _next;
            return new string(data, boundaries.Item1, boundaries.Item2 - boundaries.Item1);
        }

        public string ExtractModifierName()
        {
            int curr = _position;
            while (curr < length)
            {
                char b = data[curr];
                if (IsWhitespace(b) || b == '=')
                    break;
                ++curr;
            }

            string name = new(data, _position, curr - _position);
            _position = curr;
            SkipWhiteSpace();
            return name;
        }
    }
}
