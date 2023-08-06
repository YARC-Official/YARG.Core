using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace YARG.Core.Song.Metadata
{
    public readonly struct HashWrapper : IComparable<HashWrapper>, IEquatable<HashWrapper>
    {
        public static readonly HashAlgorithm Algorithm;
        public static readonly int HashSizeInBytes = 20;
        static HashWrapper()
        {
            Algorithm = SHA1.Create();
        }

        private readonly byte[] _hash;
        private readonly int _hashcode;

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

        public int CompareTo(HashWrapper other)
        {
            Debug.Assert(_hash.Length == other._hash.Length, "Two incompatible hash types used");
            int count = _hash.Length / 4;
            unsafe
            {
                fixed(byte* p = _hash, p2 = other._hash)
                {
                    int* integers = (int*) p;
                    int* integers2 = (int*) p2;
                    for (int i = 0; i < count; i++)
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
            return BitConverter.ToString(_hash);
        }
    }
}
