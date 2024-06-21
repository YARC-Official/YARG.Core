using System.Text;

namespace YARG.Core.IO
{
    public static unsafe class StringDecoder
    {
        private static readonly UTF8Encoding UTF8 = new(true, true);
        public static string Decode(byte* data, long count)
        {
            try
            {
                return UTF8.GetString(data, (int) count);
            }
            catch
            {
                return YARGTextContainer.Latin1.GetString(data, (int) count);
            }
        }

        public static string Decode(char* data, long count)
        {
            return new string(data, 0, (int) count);
        }
    }
}