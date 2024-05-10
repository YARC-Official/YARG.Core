using System;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public sealed class ByteStringDecoder : IStringDecoder<byte>
    {
        private static readonly UTF8Encoding UTF8 = new(true, true);
        private Encoding encoding = UTF8;

        public string Decode(byte[] data, int index, int count)
        {
            try
            {
                return encoding.GetString(data, index, count);
            }
            catch
            {
                encoding = YARGTextContainer.Latin1;
                return encoding.GetString(data, index, count);
            }
        }
    }

    public struct CharStringDecoder : IStringDecoder<char>
    {
        public readonly string Decode(char[] data, int index, int count)
        {
            return new string(data, index, count);
        }
    }

    public interface IStringDecoder<TChar>
        where TChar : unmanaged, IConvertible
    {
        public string Decode(TChar[] data, int index, int count);
    } 
}
