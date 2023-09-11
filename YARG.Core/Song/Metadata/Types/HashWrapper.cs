using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song
{
    [Serializable]
    public readonly struct HashWrapper : IComparable<HashWrapper>, IEquatable<HashWrapper>
    {
        public static HashAlgorithm Algorithm => SHA1.Create();
        public const int HASHSIZEINBYTES = 20;
        private const int NUMINTEGERS = 5;

        public static HashWrapper Create(byte[] buffer)
        {
            return new HashWrapper(Algorithm.ComputeHash(buffer));
        }

        public static HashWrapper Create(Stream stream)
        {
            stream.Position = 0;
            return new HashWrapper(Algorithm.ComputeHash(stream));
        }

        private readonly byte[] _hash;
        private readonly int _hashcode;

        public HashWrapper(YARGBinaryReader reader) : this (reader.ReadBytes(HASHSIZEINBYTES)) {}

        public HashWrapper(BinaryReader reader) : this(reader.ReadBytes(HASHSIZEINBYTES)) { }

        public HashWrapper(byte[] hash)
        {
            _hash = hash;
            _hashcode = 0;
            int count = hash.Length / 4;
            unsafe
            {
                fixed(byte* p = hash)
                {
                    int* integers = (int*)p;
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
                    for (int i = 0; i < NUMINTEGERS; i++)
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
            return BitConverter.ToString(_hash).Replace("-", "");
        }
    }
}
