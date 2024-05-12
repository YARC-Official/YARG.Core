using System;
using System.IO;
using System.Security.Cryptography;
using YARG.Core.Extensions;

namespace YARG.Core.Song
{
    [Serializable]
    public unsafe struct HashWrapper : IComparable<HashWrapper>, IEquatable<HashWrapper>
    {
        public static HashAlgorithm Algorithm => SHA1.Create();

        public const int HASH_SIZE_IN_BYTES = 20;
        public const int HASH_SIZE_IN_INTS = HASH_SIZE_IN_BYTES / sizeof(int);

        private fixed byte _hash[HASH_SIZE_IN_BYTES];
        private int _hashcode;

        public byte[] HashBytes
        {
            get
            {
                var bytes = new byte[HASH_SIZE_IN_BYTES];
                for (var i = 0; i < HASH_SIZE_IN_BYTES; i++)
                {
                    bytes[i] = _hash[i];
                }
                return bytes;
            }
        }

        public static HashWrapper Deserialize(BinaryReader reader)
        {
            var wrapper = new HashWrapper();
            var span = new Span<byte>(wrapper._hash, HASH_SIZE_IN_BYTES);
            if (reader.Read(span) != HASH_SIZE_IN_BYTES)
            {
                throw new EndOfStreamException();
            }

            int* integers = (int*) wrapper._hash;
            for (int i = 0; i < HASH_SIZE_IN_INTS; i++)
            {
                wrapper._hashcode ^= integers[i];
            }
            return wrapper;
        }

        public static HashWrapper Hash(ReadOnlySpan<byte> span)
        {
            var wrapper = new HashWrapper();
            var hashSpan = new Span<byte>(wrapper._hash, HASH_SIZE_IN_BYTES);

            using var algo = Algorithm;
            if (!algo.TryComputeHash(span, hashSpan, out int written))
            {
                throw new Exception("fucking how??? Hash generation error");
            }

            int* integers = (int*) wrapper._hash;
            for (int i = 0; i < HASH_SIZE_IN_INTS; i++)
            {
                wrapper._hashcode ^= integers[i];
            }
            return wrapper;
        }

        public static HashWrapper Create(byte[] hash)
        {
            var wrapper = new HashWrapper();
            for (var i = 0; i < HASH_SIZE_IN_BYTES; i++)
            {
                wrapper._hash[i] = hash[i];
            }

            int* integers = (int*) wrapper._hash;
            for (int i = 0; i < HASH_SIZE_IN_INTS; i++)
            {
                wrapper._hashcode ^= integers[i];
            }
            return wrapper;
        }

        public void Serialize(BinaryWriter writer)
        {
            fixed (byte* ptr = _hash)
            {
                var span = new ReadOnlySpan<byte>(ptr, HASH_SIZE_IN_BYTES);
                writer.Write(span);
            }
        }

        public int CompareTo(HashWrapper other)
        {
            for (int i = 0; i < HASH_SIZE_IN_BYTES; ++i)
            {
                if (_hash[i] != other._hash[i])
                {
                    return _hash[i] - other._hash[i];
                }
            }
            return 0;
        }

        public bool Equals(HashWrapper other)
        {
            for (int i = 0; i < HASH_SIZE_IN_BYTES; ++i)
            {
                if (_hash[i] != other._hash[i])
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            return _hashcode;
        }

        public override string ToString()
        {
            fixed (byte* ptr = _hash)
            {
                var span = new ReadOnlySpan<byte>(ptr, HASH_SIZE_IN_BYTES);
                return span.ToHexString(dashes: false);
            }
        }
    }
}
