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

        private readonly DisposableCounter<FixedArray<byte>> _counter;

        public readonly FixedArray<byte> Keys;
        public readonly unsafe Vector<byte>* Vectors;

        public SngMask(Stream stream)
        {
            _counter = DisposableCounter.Wrap(FixedArray<byte>.Alloc(NUM_KEYBYTES));

            Keys = _counter.Value;
            unsafe
            {
                byte* mask = stackalloc byte[MASKLENGTH];
                stream.Read(new Span<byte>(mask, MASKLENGTH));
                for (int i = 0; i < NUM_KEYBYTES;)
                    for (int j = 0; j < MASKLENGTH; i++, j++)
                        Keys.Ptr[i] = (byte) (mask[j] ^ i);
                Vectors = (Vector<byte>*) Keys.Ptr;
            }
        }

        public SngMask Clone()
        {
            return new SngMask(this);
        }

        private SngMask(SngMask other)
        {
            _counter = other._counter.AddRef();
            Keys = other.Keys;
            unsafe
            {
                Vectors = other.Vectors;
            }
        }

        public void Dispose()
        {
            _counter.Dispose();
        }
    }
}
