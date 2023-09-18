using System;
using System.IO;
using System.Linq;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.Song.Deserialization
{
    public class YARGTXTReader : YARGTXTReader_Base<byte>, ITXTReader
    {
        private static readonly byte[] BOM_UTF8 = { 0xEF, 0xBB, 0xBF };
        private static readonly byte[] BOM_OTHER = { 0xFF, 0xFE };
        private static readonly UTF8Encoding UTF8 = new(true, true);
        static YARGTXTReader() { }

        public static YARGTXTReader? Load(byte[] data)
        {
            if (!ValidateBOM(data))
                return null;

            int position = data[0] == BOM_UTF8[0] && data[1] == BOM_UTF8[1] && data[2] == BOM_UTF8[2] ? 3 : 0;
            return new YARGTXTReader(data, position);
        }

        public static YARGTXTReader? Load(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            byte[] bom = fs.ReadBytes(3);

            if (!ValidateBOM(bom))
                return null;

            int position = bom[0] == BOM_UTF8[0] && bom[1] == BOM_UTF8[1] && bom[2] == BOM_UTF8[2] ? 3 : 0;
            fs.Position = 0;
            byte[] data = fs.ReadBytes((int)fs.Length);
            return new YARGTXTReader(data, position);
        }

        private static bool ValidateBOM(byte[] bom)
        {
            return (bom[0] != BOM_OTHER[0] || bom[1] != BOM_OTHER[1]) && (bom[0] != BOM_OTHER[1] || bom[1] != BOM_OTHER[0]);
        }

        private Encoding encoding = Encoding.UTF8;

        private YARGTXTReader(byte[] data, int position) : base(data)
        {
            _position = position;

            SkipWhiteSpace();
            SetNextPointer();
            if (data[_position] == '\n')
                GotoNextLine();
        }

        public override char SkipWhiteSpace()
        {
            while (_position < length)
            {
                char ch = (char)data[_position];
                if (ITXTReader.IsWhitespace(ch))
                {
                    if (ch == '\n')
                        return ch;
                }
                else if (ch != '=')
                    return ch;
                ++_position;
            }

            return (char)0;
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

        private ReadOnlySpan<byte> InternalExtractTextSpan(bool checkForQuotes = true)
        {
            (int, int) boundaries = new(_position, _next);
            if (boundaries.Item2 == length)
                --boundaries.Item2;

            if (checkForQuotes && data[_position] == '\"')
            {
                int end = boundaries.Item2 - 1;
                while (_position + 1 < end && ITXTReader.IsWhitespace((char)data[end]))
                    --end;

                if (_position < end && data[end] == '\"' && data[end - 1] != '\\')
                {
                    ++boundaries.Item1;
                    boundaries.Item2 = end;
                }
            }

            if (boundaries.Item2 < boundaries.Item1)
                return new();

            while (boundaries.Item2 > boundaries.Item1 && ITXTReader.IsWhitespace((char)data[boundaries.Item2 - 1]))
                --boundaries.Item2;

            _position = _next;
            return new(data, boundaries.Item1, boundaries.Item2 - boundaries.Item1);
        }

        public string ExtractText(bool checkForQuotes = true)
        {
            var span = InternalExtractTextSpan(checkForQuotes);
            try
            {
                return encoding.GetString(span);
            }
            catch
            {
                encoding = ITXTReader.Latin1;
                return encoding.GetString(span);
            }
        }

        public string ExtractModifierName()
        {
            int curr = _position;
            while (curr < length)
            {
                char b = (char)data[curr];
                if (ITXTReader.IsWhitespace(b) || b == '=')
                    break;
                ++curr;
            }

            ReadOnlySpan<byte> name = new(data, _position, curr - _position);
            _position = curr;
            SkipWhiteSpace();
            return encoding.GetString(name);
        }
    }
}
