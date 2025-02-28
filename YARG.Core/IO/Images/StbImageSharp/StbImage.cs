/*
 * Public Domain STBImageSharp by Roman Shapiro
 */
using System;

namespace StbImageSharp
{
    public static unsafe partial class StbImage
    {
        public static readonly char[] stbi__parse_png_file_invalid_chunk = new char[25];

        public struct stbi__context
        {
            private readonly byte* _data;
            private readonly long _length;
            private long _position;

            public readonly byte* Data => _data;
            public readonly long Length => _length;
            public long Position
            {
                readonly get => _position;
                set => _position = value;
            }

            public int img_n;
            public int img_out_n;
            public uint img_x;
            public uint img_y;

            public stbi__context(byte* data, long length)
            {
                _data = data;
                _length = length;
                _position = 0;
                img_n = 0;
                img_out_n = 0;
                img_x = 0;
                img_y = 0;
            }
        }

        private static byte stbi__get8(stbi__context* s)
        {
            if (s->Position == s->Length)
            {
                return 0;
            }
            return s->Data[s->Position++];
        }

        private static void stbi__skip(stbi__context* s, int skip)
        {
            s->Position += skip;
        }

        private static void stbi__rewind(stbi__context* s)
        {
            s->Position = 0;
        }

        private static bool stbi__at_eof(stbi__context* s)
        {
            return s->Position == s->Length;
        }

        private static long stbi__getn(stbi__context* s, byte* buf, int size)
        {
            long read = s->Length - s->Position;
            if (read > size)
            {
                read = size;
            }
            Buffer.MemoryCopy(s->Data + s->Position, buf, size, read);
            s->Position += read;
            return read;
        }
    }
}