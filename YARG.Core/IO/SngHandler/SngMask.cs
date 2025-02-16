using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace YARG.Core.IO
{
    /// <summary>
    /// Handles the buffer of decryption keys, while also providing easy access
    /// to SIMD vector operations through pointers and fixed array behavior.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 32)]
    public unsafe struct SngMask
    {
        public const int MASK_SIZE = 256;
        public static readonly int NUM_VECTORS = MASK_SIZE / sizeof(Vector<byte>);

        public fixed byte Ptr[MASK_SIZE];
        public static SngMask LoadMask(Stream stream)
        {
            const int NUM_KEYS = 16;
            Span<byte> keys = stackalloc byte[NUM_KEYS];
            if (stream.Read(keys) < keys.Length)
            {
                throw new EndOfStreamException("Unable to read SNG mask");
            }

            var mask = default(SngMask);
            for (int i = 0; i < MASK_SIZE; ++i)
            {
                mask.Ptr[i] = (byte) (keys[i % NUM_KEYS] ^ i);
            }
            return mask;
        }
    }
}
