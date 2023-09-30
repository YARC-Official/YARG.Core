﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.Song.Deserialization
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

    public class YARGTXTReader<TType, TDecoder> : YARGTXTReader_Base<TType>, ITXTReader
        where TType : unmanaged, IConvertible
        where TDecoder : IStringDecoder<TType>, new()
    {
        private TDecoder Decoder = new();

        public YARGTXTReader(TType[] data, int position) : base(data)
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

        private ReadOnlySpan<TType> InternalExtractTextSpan(bool checkForQuotes = true)
        {
            (int, int) boundaries = new(_position, _next);
            if (Data[boundaries.Item2 - 1].ToChar(null) == '\r')
                --boundaries.Item2;

            if (checkForQuotes && Data[_position].ToChar(null) == '\"')
            {
                int end = boundaries.Item2 - 1;
                while (_position + 1 < end && ITXTReader.IsWhitespace(Data[end].ToChar(null)))
                    --end;

                if (_position < end && Data[end].ToChar(null) == '\"' && Data[end - 1].ToChar(null) != '\\')
                {
                    ++boundaries.Item1;
                    boundaries.Item2 = end;
                }
            }

            if (boundaries.Item2 < boundaries.Item1)
                return new();

            while (boundaries.Item2 > boundaries.Item1 && ITXTReader.IsWhitespace(Data[boundaries.Item2 - 1].ToChar(null)))
                --boundaries.Item2;

            _position = _next;
            return new(Data, boundaries.Item1, boundaries.Item2 - boundaries.Item1);
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
                Decoder.SetEncoding(ITXTReader.Latin1);
                return Decode(span);
            }
        }

        public string ExtractModifierName()
        {
            int curr = _position;
            while (curr < Length)
            {
                char b = Data[curr].ToChar(null);
                if (ITXTReader.IsWhitespace(b) || b == '=')
                    break;
                ++curr;
            }

            ReadOnlySpan<TType> name = new(Data, _position, curr - _position);
            _position = curr;
            SkipWhiteSpace();
            return Decode(name);
        }

        public string Decode(ReadOnlySpan<TType> span)
        {
            return Decoder.Decode(span);
        }
    }
}
