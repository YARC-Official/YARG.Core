using System;
using System.IO;
using System.Security.Cryptography;

namespace YARG.Core.Song
{
    [Serializable]
    public unsafe struct HashWrapper : IComparable<HashWrapper>, IEquatable<HashWrapper>
    {
        public static HashAlgorithm Algorithm => SHA1.Create();

        public const int HASH_SIZE_IN_BYTES = 20;
        public const int HASH_SIZE_IN_INTS = HASH_SIZE_IN_BYTES / sizeof(int);

        private fixed int _hash[HASH_SIZE_IN_INTS];

        public readonly byte[] HashBytes
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
            return wrapper;
        }

        public static HashWrapper Create(ReadOnlySpan<byte> hash)
        {
            var wrapper = new HashWrapper();
            var bytes = (byte*)wrapper._hash;
            for (var i = 0; i < HASH_SIZE_IN_BYTES; i++)
            {
                bytes[i] = hash[i];
            }
            return wrapper;
        }

        public readonly void Serialize(BinaryWriter writer)
        {
            for (int i = 0; i < HASH_SIZE_IN_INTS; ++i)
            {
                writer.Write(_hash[i]);
            }
        }

        public readonly int CompareTo(HashWrapper other)
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

        public readonly bool Equals(HashWrapper other)
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
            int hashcode = 0;
            for (int i = 0; i < HASH_SIZE_IN_INTS; ++i)
            {
                hashcode ^= _hash[i];
            }
            return hashcode;
        }

        public readonly override string ToString()
        {
            string str = string.Empty;
            for (int i = 0; i < HASH_SIZE_IN_INTS; ++i)
            {
                str += _hash[i].ToString("X8");
            }
            return str;
        }
    }
}
