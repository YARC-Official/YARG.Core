using System;
using System.IO;

namespace YARG.Core.IO
{
    public class YARGSongFileStream : Stream
    {
        private const int HEADER_SIZE = 24;
        private const int SET_LENGTH  = 15;

        private static readonly byte[] FILE_SIGNATURE =
        {
            (byte) 'Y', (byte) 'A', (byte) 'R', (byte) 'G',
            (byte) 'S', (byte) 'O', (byte) 'N', (byte) 'G'
        };

        private readonly FileStream _fileStream;

        public override bool CanRead  => _fileStream.CanRead;
        public override bool CanSeek => _fileStream.CanSeek;
        public override bool CanWrite => false;

        public override long Position
        {
            get => _fileStream.Position - HEADER_SIZE;
            set => _fileStream.Position = value + HEADER_SIZE;
        }

        public override long Length  { get; }

        // These are very important values required to properly
        // decrypt the first layer of encryption (Crawford multi-
        // value cipher).
        private readonly int[] _values = new int[4];

        public YARGSongFileStream(string path)
        {
            _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            Length = _fileStream.Length - HEADER_SIZE;

            // Check file signature

            Span<byte> signature = stackalloc byte[FILE_SIGNATURE.Length];
            if (_fileStream.Read(signature) != FILE_SIGNATURE.Length)
            {
                throw new EndOfStreamException();
            }

            if (!signature.SequenceEqual(FILE_SIGNATURE))
            {
                throw new Exception("The specified file is not a YARG Song!");
            }

            // Get the Crawford special number

            // The main part
            int z = _fileStream.ReadByte();
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
            if (_fileStream.Read(set) != SET_LENGTH)
            {
                throw new EndOfStreamException();
            }

            // Get the values using X

            x = (x + 5) % 255; // Convert to byte again
            unchecked
            {
                for (int i = 0, j = 0; i < 24; i++, j += x)
                {
                    // Just use the standard numbers for this
                    _values[0] += (byte) (set[j % 15] + i * 3298 + 88903);
                    _values[1] -= set[(j + 7001) % 15];
                    _values[2] += set[j % 15];
                    _values[3] += j << 2;
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int pos = (int) Position;
            int read = _fileStream.Read(buffer, offset, count);
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

        public override void Flush() => _fileStream.Flush();

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
    }
}