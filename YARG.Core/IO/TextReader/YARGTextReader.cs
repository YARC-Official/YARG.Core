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
            _position = position;

            SkipWhiteSpace();
            SetNextPointer();
            if (data[_position].ToChar(null) == '\n')
                GotoNextLine();
        }

        public override char SkipWhiteSpace()
        {
            while (_position < Length)
            {
                char ch = Data[_position].ToChar(null);
                if (ch.IsAsciiWhitespace())
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

                if (Data[_position].ToChar(null) == '{')
                {
                    _position++;
                    curr = SkipWhiteSpace();
                }

                SetNextPointer();
            } while (curr == '\n' || curr == '/' && Data[_position + 1].ToChar(null) == '/');
        }

        public void SetNextPointer()
        {
            _next = _position;
            while (_next < Length && Data[_next].ToChar(null) != '\n')
                ++_next;
        }

        public ReadOnlySpan<TChar> ExtractBasicSpan(int length)
        {
            return new ReadOnlySpan<TChar>(Data, _position, length);
        }

        private ReadOnlySpan<TChar> InternalExtractTextSpan(bool checkForQuotes = true)
        {
            (int stringBegin, int stringEnd) = (_position, _next);
            if (Data[stringEnd - 1].ToChar(null) == '\r')
                --stringEnd;

            if (checkForQuotes && Data[_position].ToChar(null) == '\"')
            {
                int end = stringEnd - 1;
                while (_position + 1 < end && Data[end].ToChar(null).IsAsciiWhitespace())
                    --end;

                if (_position < end && Data[end].ToChar(null) == '\"' && Data[end - 1].ToChar(null) != '\\')
                {
                    ++stringBegin;
                    stringEnd = end;
                }
            }

            if (stringEnd < stringBegin)
                return new();

            while (stringEnd > stringBegin && Data[stringEnd - 1].ToChar(null).IsAsciiWhitespace())
                --stringEnd;

            _position = _next;
            return new(Data, stringBegin, stringEnd - stringBegin);
        }

        public string ExtractText(bool checkForQuotes = true)
        {
            var span = InternalExtractTextSpan(checkForQuotes);
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
            int curr = _position;
            while (curr < Length)
            {
                char b = Data[curr].ToChar(null);
                if (b.IsAsciiWhitespace() || b == '=')
                    break;
                ++curr;
            }

            ReadOnlySpan<TChar> name = new(Data, _position, curr - _position);
            _position = curr;
            SkipWhiteSpace();
            return Decode(name);
        }

        public string Decode(ReadOnlySpan<TChar> span)
        {
            return Decoder.Decode(span);
        }
    }
}
