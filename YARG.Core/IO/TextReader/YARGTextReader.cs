using System;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public static class YARGTextReader
    {
        public static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
        private static readonly UTF32Encoding UTF32BE = new(true, false);

        public static YARGTextReader<byte>? TryLoadByteReader(byte[] data)
        {
            if ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF))
                return null;

            int position = data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            return new YARGTextReader<byte>(data, position);
        }

        public static YARGTextReader<char> LoadCharReader(byte[] data)
        {
            char[] charData;
            if (data[0] == 0xFF && data[1] == 0xFE)
            {
                if (data[2] != 0)
                    charData = Encoding.Unicode.GetChars(data, 2, data.Length - 2);
                else
                    charData = Encoding.UTF32.GetChars(data, 3, data.Length - 3);
            }
            else
            {
                if (data[2] != 0)
                    charData = Encoding.BigEndianUnicode.GetChars(data, 2, data.Length - 2);
                else
                    charData = UTF32BE.GetChars(data, 3, data.Length - 3);
            }
            return new YARGTextReader<char>(charData, 0);
        }
    }

    public class YARGTextReader<TChar> : YARGBaseTextReader<TChar>
        where TChar : unmanaged, IConvertible
    {
        public YARGTextReader(TChar[] data, int position) : base(data)
        {
            Position = position;

            SkipWhiteSpace();
            SetNextPointer();
            if (data[Position].ToChar(null) == '\n')
                GotoNextLine();
        }

        public override char SkipWhiteSpace()
        {
            while (Position < Length)
            {
                char ch = Data[Position].ToChar(null);
                if (ch.IsAsciiWhitespace())
                {
                    if (ch == '\n')
                        return ch;
                }
                else if (ch != '=')
                    return ch;
                ++Position;
            }

            return (char) 0;
        }

        public void GotoNextLine()
        {
            char curr;
            do
            {
                Position = _next;
                if (Position >= Length)
                    break;

                Position++;
                curr = SkipWhiteSpace();

                if (Position == Length)
                    break;

                if (Data[Position].ToChar(null) == '{')
                {
                    Position++;
                    curr = SkipWhiteSpace();
                }

                SetNextPointer();
            } while (curr == '\n' || curr == '/' && Data[Position + 1].ToChar(null) == '/');
        }

        public void SetNextPointer()
        {
            _next = Position;
            while (_next < Length && Data[_next].ToChar(null) != '\n')
                ++_next;
        }

        public ReadOnlySpan<TChar> PeekBasicSpan(int length)
        {
            return new ReadOnlySpan<TChar>(Data, Position, length);
        }
    }
}
