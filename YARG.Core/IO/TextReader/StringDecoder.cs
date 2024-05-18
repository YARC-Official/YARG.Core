using System;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public static class StringDecoder
    {
        private static readonly UTF8Encoding UTF8 = new(true, true);
        public static string Decode(byte[] data, int index, int count)
        {
            try
            {
                return UTF8.GetString(data, index, count);
            }
            catch
            {
                return YARGTextContainer.Latin1.GetString(data, index, count);
            }
        }

        public static string Decode(char[] data, int index, int count)
        {
            return new string(data, index, count);
        }
    }
}
