using System;

namespace YARG.Core.Extensions
{
    public static class MemoryExtensions
    {
        public static string ToHexString(this byte[] buffer, bool dashes = true)
            => ToHexString(buffer.AsSpan(), dashes);

        public static string ToHexString(this ReadOnlyMemory<byte> buffer, bool dashes = true)
            => ToHexString(buffer.Span, dashes);

        public static string ToHexString(this ReadOnlySpan<byte> buffer, bool dashes = true)
        {
            const string characters = "0123456789ABCDEF";

            if (buffer.IsEmpty)
                return "";

            if (dashes)
            {
                const int charsPerByte = 3;
                Span<char> stringBuffer = stackalloc char[buffer.Length * charsPerByte];
                for (int i = 0; i < buffer.Length; i++)
                {
                    byte value = buffer[i];
                    int stringIndex = i * charsPerByte;
                    stringBuffer[stringIndex] = characters[(value & 0xF0) >> 4];
                    stringBuffer[stringIndex + 1] = characters[value & 0x0F];
                    stringBuffer[stringIndex + 2] = '-';
                }

                // Exclude last '-'
                stringBuffer = stringBuffer[..^1];

                return new string(stringBuffer);
            }
            else
            {
                const int charsPerByte = 2;
                Span<char> stringBuffer = stackalloc char[buffer.Length * charsPerByte];
                for (int i = 0; i < buffer.Length; i++)
                {
                    byte value = buffer[i];
                    int stringIndex = i * charsPerByte;
                    stringBuffer[stringIndex] = characters[(value & 0xF0) >> 4];
                    stringBuffer[stringIndex + 1] = characters[value & 0x0F];
                }

                return new string(stringBuffer);
            }
        }
    }
}