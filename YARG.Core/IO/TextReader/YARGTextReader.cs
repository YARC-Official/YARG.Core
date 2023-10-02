using System;
using System.IO;
using System.Linq;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public interface IStringDecoder<TType>
        where TType : unmanaged
    {
        public void SetEncoding(Encoding encoding);

        public string Decode(ReadOnlySpan<TType> span);
    }

    public class ByteStringDecoder : IStringDecoder<byte>
    {
        protected static readonly UTF8Encoding UTF8 = new(true, true);
        private Encoding encoding = UTF8;
        public void SetEncoding(Encoding encoding)
        {
            this.encoding = encoding;
        }

        public string Decode(ReadOnlySpan<byte> span)
        {
            return encoding.GetString(span);
        }
    }

    public struct CharStringDecoder : IStringDecoder<char>
    {
        public void SetEncoding(Encoding encoding)
        {
            throw new NotImplementedException();
        }

        public string Decode(ReadOnlySpan<char> span)
        {
            return span.ToString();
        }
    }

    public class YARGTextReader<TChar, TDecoder> : YARGTextReader_Base<TChar>, IYARGTextReader
        where TChar : unmanaged, IConvertible
        where TDecoder : IStringDecoder<TChar>, new()
    {
        private TDecoder Decoder = new();

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

        private ReadOnlySpan<TChar> InternalExtractTextSpan(bool isChartFile = true)
        {
            (int stringBegin, int stringEnd) = (Position, _next);
            if (Data[stringEnd - 1].ToChar(null) == '\r')
                --stringEnd;

            if (isChartFile && Data[Position].ToChar(null) == '\"')
            {
                int end = stringEnd - 1;
                while (Position + 1 < end && Data[end].ToChar(null).IsAsciiWhitespace())
                    --end;

                if (Position < end && Data[end].ToChar(null) == '\"' && Data[end - 1].ToChar(null) != '\\')
                {
                    ++stringBegin;
                    stringEnd = end;
                }
            }

            if (stringEnd < stringBegin)
                return new();

            while (stringEnd > stringBegin && Data[stringEnd - 1].ToChar(null).IsAsciiWhitespace())
                --stringEnd;

            Position = _next;
            return new(Data, stringBegin, stringEnd - stringBegin);
        }

        public string ExtractText(bool isChartFile = true)
        {
            var span = InternalExtractTextSpan(isChartFile);
            try
            {
                return Decode(span);
            }
            catch
            {
                Decoder.SetEncoding(YARGTextReader.Latin1);
                return Decode(span);
            }
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

            ReadOnlySpan<TChar> name = new(Data, Position, curr - Position);
            Position = curr;
            SkipWhiteSpace();
            return Decode(name);
        }

        public string Decode(ReadOnlySpan<TChar> span)
        {
            return Decoder.Decode(span);
        }
    }
}
