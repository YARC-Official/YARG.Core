using System;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public sealed class ByteStringDecoder : StringDecoder<byte>
    {
        private static readonly UTF8Encoding UTF8 = new(true, true);
        private Encoding encoding = UTF8;

        public override string ExtractText(YARGTextContainer<byte> reader, bool isChartFile)
        {
            var span = InternalExtractTextSpan(reader, isChartFile);
            try
            {
                return Decode(span);
            }
            catch
            {
                encoding = YARGTextContainer.Latin1;
                return Decode(span);
            }
        }

        public override string Decode(ReadOnlySpan<byte> span)
        {
            return encoding.GetString(span);
        }
    }

    public sealed class CharStringDecoder : StringDecoder<char>
    {
        public override string ExtractText(YARGTextContainer<char> reader, bool isChartFile)
        {
            var span = InternalExtractTextSpan(reader, isChartFile);
            return Decode(span);
        }

        public override string Decode(ReadOnlySpan<char> span)
        {
            return span.ToString();
        }
    }

    public abstract class StringDecoder<TChar>
        where TChar : unmanaged, IConvertible
    {
        public abstract string ExtractText(YARGTextContainer<TChar> reader, bool isChartFile);

        public abstract string Decode(ReadOnlySpan<TChar> span);

        protected static ReadOnlySpan<TChar> InternalExtractTextSpan(YARGTextContainer<TChar> reader, bool isChartFile = true)
        {
            (int stringBegin, int stringEnd) = (reader.Position, reader.Next);
            if (reader.Data[stringEnd - 1].ToChar(null) == '\r')
                --stringEnd;

            if (isChartFile && reader.Data[reader.Position].ToChar(null) == '\"')
            {
                int end = stringEnd - 1;
                while (reader.Position + 1 < end && reader.Data[end].ToChar(null).IsAsciiWhitespace())
                    --end;

                if (reader.Position < end && reader.Data[end].ToChar(null) == '\"' && reader.Data[end - 1].ToChar(null) != '\\')
                {
                    ++stringBegin;
                    stringEnd = end;
                }
            }

            if (stringEnd < stringBegin)
                return new();

            while (stringEnd > stringBegin && reader.Data[stringEnd - 1].ToChar(null).IsAsciiWhitespace())
                --stringEnd;

            reader.Position = reader.Next;
            return new(reader.Data, stringBegin, stringEnd - stringBegin);
        }
    } 
}
