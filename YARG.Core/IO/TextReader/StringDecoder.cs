using System;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.IO.Disposables;

namespace YARG.Core.IO
{
    public sealed class ByteStringDecoder : IStringDecoder<byte>
    {
        private static readonly UTF8Encoding UTF8 = new(true, true);
        private Encoding encoding = UTF8;

        public unsafe string Decode(FixedArray<byte> data, long index, long count)
        {
            try
            {
                return encoding.GetString(data.Ptr + index, (int)count);
            }
            catch
            {
                encoding = YARGTextContainer.Latin1;
                return encoding.GetString(data.Ptr + index, (int)count);
            }
        }
    }

    public struct CharStringDecoder : IStringDecoder<char>
    {
        public readonly unsafe string Decode(FixedArray<char> data, long index, long count)
        {
            return new string(data.Ptr, (int)index, (int) count);
        }
    }

    public interface IStringDecoder<TChar>
        where TChar : unmanaged, IConvertible
    {
        public string Decode(FixedArray<TChar> data, long index, long count);
    } 
}
