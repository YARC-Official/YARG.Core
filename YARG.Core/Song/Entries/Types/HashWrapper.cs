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

        private fixed int _hash[HASH_SIZE_IN_INTS];
        private int _hashcode;

        public byte[] HashBytes
        {
            get
            {
                var bytes = new byte[HASH_SIZE_IN_BYTES];
                fixed (byte* ptr = bytes)
                {
                    int* integers = (int*)ptr;
                    for (var i = 0; i < HASH_SIZE_IN_INTS; i++)
                    {
                        integers[i] = _hash[i];
                    }
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

            for (int i = 0; i < HASH_SIZE_IN_INTS; i++)
            {
                wrapper._hashcode ^= wrapper._hash[i];
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

            for (int i = 0; i < HASH_SIZE_IN_INTS; i++)
            {
                wrapper._hashcode ^= wrapper._hash[i];
            }
            return wrapper;
        }

        public static HashWrapper Create(byte[] hash)
        {
            var wrapper = new HashWrapper();
            var bytes = (byte*)wrapper._hash;
            for (var i = 0; i < HASH_SIZE_IN_BYTES; i++)
            {
                bytes[i] = hash[i];
            }

            for (int i = 0; i < HASH_SIZE_IN_INTS; i++)
            {
                wrapper._hashcode ^= wrapper._hash[i];
            }
            return wrapper;
        }

        public void Serialize(BinaryWriter writer)
        {
            fixed (int* ptr = _hash)
            {
                var span = new ReadOnlySpan<byte>(ptr, HASH_SIZE_IN_BYTES);
                writer.Write(span);
            }
        }

        public int CompareTo(HashWrapper other)
        {
            for (int i = 0; i < HASH_SIZE_IN_INTS; ++i)
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
            for (int i = 0; i < HASH_SIZE_IN_INTS; ++i)
            {
                if (_hash[i] != other._hash[i])
                {
                    return false;
                }
            }
            return true;
        }

        public readonly override int GetHashCode()
        {
            return _hashcode;
        }

        public override string ToString()
        {
            fixed (int* ptr = _hash)
            {
                var span = new ReadOnlySpan<byte>(ptr, HASH_SIZE_IN_BYTES);
                return span.ToHexString(dashes: false);
            }
        }
    }
}
