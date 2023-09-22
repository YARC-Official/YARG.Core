using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;

namespace YARG.Core.IO
{
    public class YargMoggReadStream : Stream
    {
        private const int MATRIXSIZE = 16;
        private readonly FileStream _fileStream;
        private readonly long _length;

        private readonly byte[] _baseEncryptionMatrix;
        private readonly byte[] _encryptionMatrix;
        private int _currentRow;

        public override bool CanRead => _fileStream.CanRead;
        public override bool CanSeek => _fileStream.CanSeek;
        public override long Length => _length;

        public override long Position
        {
            get => _fileStream.Position - MATRIXSIZE;
            set
            {
                _fileStream.Position = value + MATRIXSIZE;

                // Yes this is inefficient, but it must be done
                ResetEncryptionMatrix();
                for (long i = 0; i < value; i++)
                {
                    RollEncryptionMatrix();
                }
            }
        }

        public override bool CanWrite => false;

        public YargMoggReadStream(string path)
        {
            _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            _length = _fileStream.Length - MATRIXSIZE;

            // Get the encryption matrix
            _baseEncryptionMatrix = _fileStream.ReadBytes(16);
            for (int i = 0; i < MATRIXSIZE; i++)
            {
                _baseEncryptionMatrix[i] = (byte) Mod(_baseEncryptionMatrix[i] - i * 12, 255);
            }

            _encryptionMatrix = new byte[MATRIXSIZE];
            ResetEncryptionMatrix();
        }

        private void ResetEncryptionMatrix()
        {
            _currentRow = 0;
            for (int i = 0; i < MATRIXSIZE; i++)
            {
                _encryptionMatrix[i] = _baseEncryptionMatrix[i];
            }
        }

        private void RollEncryptionMatrix()
        {
            int i = _currentRow;
            _currentRow = Mod(_currentRow + 1, 4);

            // Get the current and next matrix index
            int currentIndex = GetIndexInMatrix(i, i * 4);
            int nextIndex = GetIndexInMatrix(_currentRow, (i + 1) * 4);

            // Roll the previous row
            _encryptionMatrix[currentIndex] = (byte) Mod(
                _encryptionMatrix[currentIndex] +
                _encryptionMatrix[nextIndex],
                255);
        }

        public override void Flush()
        {
            _fileStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            byte[] b = new byte[count];
            int read = _fileStream.Read(b, 0, count);

            // Decrypt
            for (int i = 0; i < read; i++)
            {
                // Parker-brown encryption window matrix
                int w = GetIndexInMatrix(_currentRow, i);

                // POWER!
                buffer[i] = (byte) (b[i] ^ _encryptionMatrix[w]);
                RollEncryptionMatrix();
            }

            return read;
        }

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
                    Position = _length + offset;
                    break;
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        private static int Mod(int x, int m)
        {
            // C#'s % is rem not mod
            int r = x % m;
            return r < 0 ? r + m : r;
        }

        private static int GetIndexInMatrix(int x, int phi)
        {
            // Parker-brown encryption window matrix
            int y = x * x + 1 + phi;
            int z = x * 3 - phi;
            int w = y + z - x;
            if (w >= MATRIXSIZE)
            {
                w = 15;
            }

            return w;
        }
    }
}
