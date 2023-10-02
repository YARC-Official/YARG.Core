using System.Text;

namespace YARG.Core.IO
{
    public interface IYARGTextReader
    {
        public string ExtractText(bool checkForQuotes = true);
    }

    public static class YARGTextReader
    {
        public static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
        private static readonly UTF32Encoding UTF32BE = new(true, false);

        public static bool Load(byte[] data, out IYARGTextReader reader)
        {
            if (data[0] == 0xFF && data[1] == 0xFE)
            {
                if (data[2] != 0)
                    reader = new YARGTextReader<char, CharStringDecoder>(Encoding.Unicode.GetChars(data, 2, data.Length - 2), 0);
                else
                    reader = new YARGTextReader<char, CharStringDecoder>(Encoding.UTF32.GetChars(data, 3, data.Length - 3), 0);
                return false;
            }

            if (data[0] == 0xFE && data[1] == 0xFF)
            {
                if (data[2] != 0)
                    reader = new YARGTextReader<char, CharStringDecoder>(Encoding.BigEndianUnicode.GetChars(data, 2, data.Length - 2), 0);
                else
                    reader = new YARGTextReader<char, CharStringDecoder>(UTF32BE.GetChars(data, 3, data.Length - 3), 0);
                return false;
            }

            int position = data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            reader = new YARGTextReader<byte, ByteStringDecoder>(data, position);
            return true;
        }
    }
}
