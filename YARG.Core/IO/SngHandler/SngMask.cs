using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace YARG.Core.IO
{
    /// <summary>
    /// Handles the buffer of decryption keys, while also providing easy access
    /// to SIMD vector operations through pointers and fixed array behavior.
    /// </summary>
    public class SngMask : IDisposable
    {
        public const int NUM_KEYBYTES = 256;
        public const int MASKLENGTH = 16;
        public static readonly int VECTORBYTE_COUNT = Vector<byte>.Count;
        public static readonly int NUMVECTORS = NUM_KEYBYTES / VECTORBYTE_COUNT;

        private readonly DisposableCounter<FixedArray<byte>> _keys;

        public FixedArray<byte> Keys => _keys.Value;
        public readonly unsafe Vector<byte>* Vectors;

        public SngMask(Stream stream)
        {
            _keys = DisposableCounter.Wrap(FixedArray<byte>.Alloc(NUM_KEYBYTES));

            unsafe
            {
                Vectors = (Vector<byte>*)_keys.Value.Ptr;
            }

            using var mask = FixedArray<byte>.Load(stream, MASKLENGTH);
            for (int i = 0; i < NUM_KEYBYTES;)
                for (int j = 0; j < MASKLENGTH; i++, j++)
                    _keys.Value[i] = (byte) (mask[j] ^ i);
        }

        public SngMask Clone()
        {
            return new SngMask(this);
        }

        private SngMask(SngMask other)
        {
            _keys = other._keys.AddRef();
            unsafe
            {
                Vectors = other.Vectors;
            }
        }

        public void Dispose()
        {
            Keys.Dispose();
        }
    }
}
