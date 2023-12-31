using System;
using System.IO;

namespace YARG.Core.IO
{
    public class YARGSongFileStream : Stream, ICloneable<YARGSongFileStream>
    {
        private const int HEADER_SIZE = 24;
        private const int SET_LENGTH  = 15;

        private static readonly byte[] FILE_SIGNATURE =
        {
            (byte) 'Y', (byte) 'A', (byte) 'R', (byte) 'G',
            (byte) 'S', (byte) 'O', (byte) 'N', (byte) 'G'
        };

        private readonly FileStream _filestream;

        public override bool CanRead  => _filestream.CanRead;
        public override bool CanSeek => _filestream.CanSeek;
        public override bool CanWrite => false;

        public override long Position
        {
            get => _filestream.Position - HEADER_SIZE;
            set => _filestream.Position = value + HEADER_SIZE;
        }

        public override long Length  { get; }

        // These are very important values required to properly
        // decrypt the first layer of encryption (Crawford multi-
        // value cipher).
        private readonly int[] _values;

        public static YARGSongFileStream? TryLoadYARGSong(string filename)
        {
            var filestream = InitStream_Internal(filename);

            Span<byte> signature = stackalloc byte[FILE_SIGNATURE.Length];
            if (filestream.Read(signature) != FILE_SIGNATURE.Length)
            {
                return null;
            }

            if (!signature.SequenceEqual(FILE_SIGNATURE))
            {
                return null;
            }

            // Get the Crawford special number

            // The main part
            int z = filestream.ReadByte();
            z += 1679;                  // A large-ish prime
            int w = (z ^ 4) - z * 2;    // Exponent W value
            int n = 25 * w - 5;         // Aleph value
            int x = (w + (z << 1)) ^ 4; // Use some bit shifting to our advantage

            // Approximate infinite summation (we're using bytes so it works)
            int l = (n + 73) * (n + 23);
            l -= n * n + 96 * n;
            x = -l + n + x - w * (5 * 5);

            // We are using a SET_LENGTH-long Euler cipher set (I think?) for this

            Span<byte> set = stackalloc byte[SET_LENGTH];
            if (filestream.Read(set) != SET_LENGTH)
            {
                throw new EndOfStreamException("YARGSong incomplete");
            }

            // Get the values using X

            x = (x + 5) % 255; // Convert to byte again

            int[] values = new int[4];
            unchecked
            {
                for (int i = 0, j = 0; i < 24; i++, j += x)
                {
                    // Just use the standard numbers for this
                    values[0] += (byte) (set[j % 15] + i * 3298 + 88903);
                    values[1] -= set[(j + 7001) % 15];
                    values[2] += set[j % 15];
                    values[3] += j << 2;
                }
            }

            long length = filestream.Length - HEADER_SIZE;
            return new YARGSongFileStream(filestream, values, length);
        }

        private YARGSongFileStream(FileStream filestream, int[] values, long length)
        {
            _filestream = filestream;
            _values = values;
            Length = length;
        }

        public YARGSongFileStream Clone()
        {
            var stream = InitStream_Internal(_filestream.Name);
            return new YARGSongFileStream(stream, _values, Length);
        }

        private static FileStream InitStream_Internal(string filename)
        {
            return new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int pos = (int) Position;
            int read = _filestream.Read(buffer, offset, count);
            var span = new Span<byte>(buffer, offset, read);

            unchecked
            {
                int a = _values[0];
                int b = _values[1];
                int c = _values[2];

                for (int i = 0; i < read; i++)
                {
                    // This is a super dumbed down version of the algorithm.
                    // If problems are encountered with this, use the full
                    // cipher roller.
                    span[i] = (byte) ((span[i] - (i + pos) * c - b) ^ a);
                }
            }

            return read;
        }

        public override void Flush() => _filestream.Flush();

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        protected override void Dispose(bool disposing)
        {
            _filestream.Dispose();
            base.Dispose(disposing);
        }
    }
}