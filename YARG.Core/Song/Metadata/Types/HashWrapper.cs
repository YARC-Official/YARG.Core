using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    [Serializable]
    public readonly struct HashWrapper : IComparable<HashWrapper>, IEquatable<HashWrapper>
    {
        public static HashAlgorithm Algorithm => SHA1.Create();

        public  const int HASH_SIZE_IN_BYTES = 20;
        private const int INT_COUNT = 5;

        private readonly byte[] _hash;
        private readonly int    _hashcode;

        public byte[] HashBytes => _hash;

        public HashWrapper(YARGBinaryReader reader)
            : this(reader.ReadBytes(HASH_SIZE_IN_BYTES))
        {
        }

        public HashWrapper(BinaryReader reader)
            : this(reader.ReadBytes(HASH_SIZE_IN_BYTES))
        {
        }

        public static HashWrapper Create(byte[] buffer)
        {
            return new HashWrapper(Algorithm.ComputeHash(buffer));
        }

        public static HashWrapper Create(Stream stream)
        {
            stream.Position = 0;
            return new HashWrapper(Algorithm.ComputeHash(stream));
        }

        public HashWrapper(byte[] hash)
        {
            _hash = hash;
            _hashcode = 0;

            int count = hash.Length / 4;

            unsafe
            {
                fixed (byte* p = hash)
                {
                    int* integers = (int*) p;
                    for (int i = 0; i < count; i++)
                    {
                        _hashcode ^= integers[i];
                    }
                }
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(_hash);
        }

        public int CompareTo(HashWrapper other)
        {
            Debug.Assert(_hash.Length == other._hash.Length, "Two incompatible hash types used");

            unsafe
            {
                fixed(byte* p = _hash, p2 = other._hash)
                {
                    int* integers = (int*) p;
                    int* integers2 = (int*) p2;
                    for (int i = 0; i < INT_COUNT; i++)
                    {
                        if (integers[i] < integers2[i])
                            return -1;
                        if (integers[i] > integers2[i])
                            return 1;
                    }
                }
            }

            return 0;
        }

        public override int GetHashCode()
        {
            return _hashcode;
        }

        public bool Equals(HashWrapper other)
        {
            return _hash.SequenceEqual(other._hash);
        }

        public override string ToString()
        {
            return _hash.ToHexString(dashes: false);
        }
    }
}
