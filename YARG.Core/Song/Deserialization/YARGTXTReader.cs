using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Core.Song.Deserialization
{
    public unsafe class YARGTXTReader : YARGTXTReader_Base
    {
        internal static readonly byte[] BOM = { 0xEF, 0xBB, 0xBF };
        internal static readonly UTF8Encoding UTF8 = new(true, true);
        static YARGTXTReader() { }

#nullable enable
        private readonly YARGFile? file;
        public YARGTXTReader(byte* ptr, int length) : base(ptr, length)
        {
            if (new ReadOnlySpan<byte>(ptr, 3).SequenceEqual(BOM))
                _position += 3;

            SkipWhiteSpace();
            SetNextPointer();
            if (ptr[_position] == '\n')
                GotoNextLine();
        }

        public YARGTXTReader(YARGFile file) : this(file.Data, file.Length)
        {
            this.file = file;
        }

        public YARGTXTReader(byte[] data) : this(new YARGFile(data)) { }

        public YARGTXTReader(string path) : this(new YARGFile(path)) { }

        public override byte SkipWhiteSpace()
        {
            while (_position < length)
            {
                byte ch = ptr[_position];
                if (ch <= 32)
                {
                    if (ch == '\n')
                        break;
                }
                else if (ch != '=')
                    break;
                ++_position;
            }

            return _position < length ? ptr[_position] : (byte) 0;
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

                if (ptr[_position] == '{')
                {
                    _position++;
                    curr = SkipWhiteSpace();
                }

                SetNextPointer();
            } while (curr == '\n' || curr == '/' && ptr[_position + 1] == '/');
        }

        public void SetNextPointer()
        {
            _next = _position;
            while (_next < length && ptr[_next] != '\n')
                ++_next;
        }

        public ReadOnlySpan<byte> ExtractTextSpan(bool checkForQuotes = true)
        {
            (int, int) boundaries = new(_position, _next);
            if (boundaries.Item2 == length)
                --boundaries.Item2;

            if (checkForQuotes && ptr[_position] == '\"')
            {
                int end = boundaries.Item2 - 1;
                while (_position + 1 < end && ptr[end] <= 32)
                    --end;

                if (_position < end && ptr[end] == '\"' && ptr[end - 1] != '\\')
                {
                    ++boundaries.Item1;
                    boundaries.Item2 = end;
                }
            }

            if (boundaries.Item2 < boundaries.Item1)
                return new();

            while (boundaries.Item2 > boundaries.Item1 && ptr[boundaries.Item2 - 1] <= 32)
                --boundaries.Item2;

            _position = _next;
            return new(ptr + boundaries.Item1, boundaries.Item2 - boundaries.Item1);
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
                byte b = ptr[curr];
                if (b <= 32 || b == '=')
                    break;
                ++curr;
            }

            ReadOnlySpan<byte> name = new(ptr + _position, curr - _position);
            _position = curr;
            SkipWhiteSpace();
            return Encoding.UTF8.GetString(name);
        }
    }
}
