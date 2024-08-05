using System;

namespace YARG.Core.Extensions
{
    public static class MemoryExtensions
    {
        public static bool TryWriteAndAdvance(ref this Span<char> dest, ReadOnlySpan<char> source, ref int written)
        {
            if (!source.TryCopyTo(dest))
                return false;

            dest = dest[source.Length..];
            written += source.Length;
            return true;
        }

        public static bool TryWriteAndAdvance(ref this Span<char> dest, char value, ref int written)
        {
            if (dest.Length < 1)
                return false;

            dest[0] = value;
            dest = dest[1..];
            written++;
            return true;
        }

        public static bool TryWriteAndAdvance(ref this Span<char> dest, int value, ref int written,
            ReadOnlySpan<char> format = default)
        {
            bool success = value.TryFormat(dest, out int valueWritten, format);
            written += valueWritten;

            if (success)
                dest = dest[valueWritten..];

            return success;
        }

        public static bool TryWriteAndAdvance(ref this Span<char> dest, double value, ref int written,
            ReadOnlySpan<char> format = default)
        {
            bool success = value.TryFormat(dest, out int valueWritten, format);
            written += valueWritten;

            if (success)
                dest = dest[valueWritten..];

            return success;
        }

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