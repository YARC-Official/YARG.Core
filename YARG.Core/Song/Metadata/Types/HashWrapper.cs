using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Utility;

namespace YARG.Core.Song
{
    [Serializable]
    public readonly struct HashWrapper : IComparable<HashWrapper>, IEquatable<HashWrapper>
    {
        public static HashAlgorithm Algorithm => SHA1.Create();

        public const int HASH_SIZE_IN_BYTES = 20;
        public const int HASH_SIZE_IN_INTS = HASH_SIZE_IN_BYTES / sizeof(int);

        private readonly FixedArray<byte> _hash;
        private readonly int          _hashcode;

        public byte[] HashBytes
        {
            get
            {
                return _hash.ToArray();
            }
        }

        public static HashWrapper Deserialize(YARGBinaryReader reader)
        {
            using var hash = DisposableCounter.Wrap(FixedArray<byte>.Alloc(HASH_SIZE_IN_BYTES));
            if (!reader.ReadBytes(hash.Value.Span))
            {
                throw new EndOfStreamException();
            }
            return new HashWrapper(hash.Release());
        }

        public static HashWrapper Deserialize(BinaryReader reader)
        {
            using var hash = DisposableCounter.Wrap(FixedArray<byte>.Alloc(HASH_SIZE_IN_BYTES));
            if (reader.Read(hash.Value.Span) != HASH_SIZE_IN_BYTES)
            {
                throw new EndOfStreamException();
            }
            return new HashWrapper(hash.Release());
        }

        public static HashWrapper Hash(ReadOnlySpan<byte> span)
        {
            using var algo = Algorithm;
            using var hash = DisposableCounter.Wrap(FixedArray<byte>.Alloc(HASH_SIZE_IN_BYTES));
            if (!algo.TryComputeHash(span, hash.Value.Span, out int written))
            {
                throw new Exception("fucking how??? Hash generation error");
            }
            return new HashWrapper(hash.Release());
        }

        public HashWrapper(byte[] hash)
            : this(FixedArray<byte>.Pin(hash)) { }

        private HashWrapper(FixedArray<byte> hash)
        {
            _hash = hash;
            _hashcode = 0;

            unsafe
            {
                int* integers = (int*) hash.Ptr;
                for (int i = 0; i < HASH_SIZE_IN_INTS; i++)
                {
                    _hashcode ^= integers[i];
                }
            }
        }

        public void Serialize(IBinaryDataWriter writer)
        {
            writer.Write(_hash.ReadOnlySpan);
        }

        public int CompareTo(HashWrapper other)
        {
            return _hash.ReadOnlySpan.SequenceCompareTo(other._hash.ReadOnlySpan);
        }

        public bool Equals(HashWrapper other)
        {
            return _hash.ReadOnlySpan.SequenceEqual(other._hash.ReadOnlySpan);
        }

        public override int GetHashCode()
        {
            return _hashcode;
        }

        public override string ToString()
        {
            return _hash.ReadOnlySpan.ToHexString(dashes: false);
        }
    }
}
