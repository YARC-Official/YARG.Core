using System;
using System.IO;
using System.Linq;
using System.Text;

namespace YARG.Core.Song.Deserialization
{
    public class BadEncodingException : Exception
    {
        public BadEncodingException() : base("Forbidden encoding") { }
    }

    public class YARGTXTReader : YARGTXTReader_Base, ITXTReader
    {
        private static readonly byte[] BOM_UTF8 = { 0xEF, 0xBB, 0xBF };
        private static readonly byte[] BOM_OTHER = { 0xFF, 0xFE };
        private static readonly UTF8Encoding UTF8 = new(true, true);
        static YARGTXTReader() { }

        public YARGTXTReader(byte[] data) : base(data)
        {
            /*
             * "A protocol SHOULD forbid use of U+FEFF as a signature for those
             * textual protocol elements that the protocol mandates to be always UTF-8,
             * the signature function being totally useless in those cases."
             * https://datatracker.ietf.org/doc/html/rfc3629
             * 
             * True reasoning: Other than some text events, the main usecase for .chart is the to hold note information.
             * That leads to a lot of basic ASCII characters, tabs/spaces, newlines, and especially digits.
             * Anything other than UTF-8 (or extended ASCII) just needlessly over bloats filesize.
             * Therefore, we should actively discourage/disallow their usage.
             */
            if ((data[0] == BOM_OTHER[0] && data[1] == BOM_OTHER[1]) ||
                (data[0] == BOM_OTHER[1] && data[1] == BOM_OTHER[0]))
                throw new BadEncodingException();

            if (data[0] == BOM_UTF8[0] && data[1] == BOM_UTF8[1] && data[2] == BOM_UTF8[2])
                _position += 3;

            SkipWhiteSpace();
            SetNextPointer();
            if (data[_position] == '\n')
                GotoNextLine();
        }

        public YARGTXTReader(string path) : this(File.ReadAllBytes(path)) { }

        public override byte SkipWhiteSpace()
        {
            while (_position < length)
            {
                byte ch = data[_position];
                if (IsWhitespace(ch))
                {
                    if (ch == '\n')
                        break;
                }
                else if (ch != '=')
                    break;
                ++_position;
            }

            return _position < length ? data[_position] : (byte) 0;
        }

        public void GotoNextLine()
        {
            byte curr;
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

        public ReadOnlySpan<byte> ExtractTextSpan(bool checkForQuotes = true)
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
                return new();

            while (boundaries.Item2 > boundaries.Item1 && IsWhitespace(data[boundaries.Item2 - 1]))
                --boundaries.Item2;

            _position = _next;
            return new(data, boundaries.Item1, boundaries.Item2 - boundaries.Item1);
        }

        public string ExtractEncodedString(bool checkForQuotes = true)
        {
            var span = ExtractTextSpan(checkForQuotes);
            try
            {
                return UTF8.GetString(span);
            }
            catch
            {
                char[] str = new char[span.Length];
                for (int i = 0; i < span.Length; ++i)
                    str[i] = (char) span[i];
                return new(str);
            }
        }

        public string ExtractModifierName()
        {
            int curr = _position;
            while (true)
            {
                byte b = data[curr];
                if (IsWhitespace(b) || b == '=')
                    break;
                ++curr;
            }

            ReadOnlySpan<byte> name = new(data, _position, curr - _position);
            _position = curr;
            SkipWhiteSpace();
            return Encoding.UTF8.GetString(name);
        }
    }
}
