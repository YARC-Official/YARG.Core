using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Song.Deserialization
{
    public class YARGTXTReader_Char : YARGTXTReader_Base<char>, ITXTReader
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
            while (_position < Length)
            {
                char ch = Data[_position];
                if (ITXTReader.IsWhitespace(ch))
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
                if (_position >= Length)
                    break;

                _position++;
                curr = SkipWhiteSpace();

                if (_position == Length)
                    break;

                if (Data[_position] == '{')
                {
                    _position++;
                    curr = SkipWhiteSpace();
                }

                SetNextPointer();
            } while (curr == '\n' || curr == '/' && Data[_position + 1] == '/');
        }

        public void SetNextPointer()
        {
            _next = _position;
            while (_next < Length && Data[_next] != '\n')
                ++_next;
        }

        public string ExtractText(bool checkForQuotes = true)
        {
            (int, int) boundaries = new(_position, _next);
            if (boundaries.Item2 == Length)
                --boundaries.Item2;

            if (checkForQuotes && Data[_position] == '\"')
            {
                int end = boundaries.Item2 - 1;
                while (_position + 1 < end && ITXTReader.IsWhitespace(Data[end]))
                    --end;

                if (_position < end && Data[end] == '\"' && Data[end - 1] != '\\')
                {
                    ++boundaries.Item1;
                    boundaries.Item2 = end;
                }
            }

            if (boundaries.Item2 < boundaries.Item1)
                return string.Empty;

            while (boundaries.Item2 > boundaries.Item1 && ITXTReader.IsWhitespace(Data[boundaries.Item2 - 1]))
                --boundaries.Item2;

            _position = _next;
            return new string(Data, boundaries.Item1, boundaries.Item2 - boundaries.Item1);
        }

        public string ExtractModifierName()
        {
            int curr = _position;
            while (curr < Length)
            {
                char ch = Data[curr];
                if (ITXTReader.IsWhitespace(ch) || ch == '=')
                    break;
                ++curr;
            }

            string name = new(Data, _position, curr - _position);
            _position = curr;
            SkipWhiteSpace();
            return name;
        }
    }
}
