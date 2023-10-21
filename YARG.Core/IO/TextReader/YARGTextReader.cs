using System;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public static class YARGTextLoader
    {
        private static readonly UTF32Encoding UTF32BE = new(true, false);

        public static YARGTextReader<byte, ByteStringDecoder>? TryLoadByteText(byte[] data)
        {
            if ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF))
                return null;

            int position = data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            return new YARGTextReader<byte, ByteStringDecoder>(data, position);
        }

        public static YARGTextReader<char, CharStringDecoder> LoadCharText(byte[] data)
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
            return new YARGTextReader<char, CharStringDecoder>(charData, 0);
        }
    }

    public sealed class YARGTextReader<TChar, TDecoder> : YARGTextContainer<TChar>
        where TChar : unmanaged, IConvertible
        where TDecoder : StringDecoder<TChar>, new()
    {
        private TDecoder decoder = new();
        public YARGTextReader(TChar[] data, int position) : base(data, position)
        {
            SkipWhitespace();
            SetNextPointer();
            if (Data[Position].ToChar(null) == '\n')
                GotoNextLine();
        }

        public override char SkipWhitespace()
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
                Position = Next;
                if (Position >= Length)
                    break;

                Position++;
                curr = SkipWhitespace();

                if (Position == Length)
                    break;

                if (Data[Position].ToChar(null) == '{')
                {
                    Position++;
                    curr = SkipWhitespace();
                }

                SetNextPointer();
            } while (curr == '\n' || curr == '/' && Data[Position + 1].ToChar(null) == '/');
        }

        public void SetNextPointer()
        {
            Next = Position;
            while (Next < Length && Data[Next].ToChar(null) != '\n')
                ++Next;
        }

        public string ExtractModifierName()
        {
            int curr = Position;
            while (curr < Length)
            {
                char b = Data[curr].ToChar(null);
                if (b.IsAsciiWhitespace() || b == '=')
                    break;
                ++curr;
            }

            var name = Slice(Position, curr - Position);
            Position = curr;
            SkipWhitespace();
            return decoder.Decode(name);
        }

        public string ExtractLine()
        {
            return decoder.Decode(Slice(Position, Next - Position)).TrimEnd();
        }

        public string ExtractText(bool isChartFile)
        {
            return decoder.ExtractText(this, isChartFile);
        }
    }
}
