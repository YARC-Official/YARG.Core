using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace YARG.Core.IO
{
    public enum ImageFormat
    {
        Grayscale = 1,
        GrayScale_Alpha = 2,
        RGB = 3,
        RGBA = 4,
        DXT1,
        DXT5,
    }

    public class YARGImage : IDisposable
    {
        public readonly IntPtr Data;
        public readonly int Length;

        public readonly int Width;
        public readonly int Height;
        public readonly ImageFormat Format;

        private readonly GCHandle Handle;
        private bool _disposed;

        public static YARGImage? Load(string file)
        {
            using var bytes = FixedArray<byte>.Load(file);
            if (bytes == null)
            {
                return null;
            }
            return Load(bytes);
        }

        public static YARGImage? Load(SngFileListing listing, SngFile sngFile)
        {
            var bytes = listing.LoadAllBytes(sngFile);
            if (bytes == null)
            {
                return null;
            }
            using var arr = FixedArray<byte>.Pin(bytes);
            return Load(arr);
        }

        private static unsafe YARGImage? Load(FixedArray<byte> file)
        {
            var result = LoadNative(file.Ptr, file.Length, out int width, out int height, out int components);
            if (result == IntPtr.Zero)
            {
                return null;
            }
            return new YARGImage(result, width, height, components);
        }

        public unsafe YARGImage(byte[] bytes)
        {
            Handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Data = Handle.AddrOfPinnedObject() + 32;
            Length = bytes.Length - 32;

            var ptr = (byte*) Data;
            for (int i = 0; i < Length; i += 2)
            {
                (ptr[i + 1], ptr[i]) = (ptr[i], ptr[i + 1]);
            }

            Width = BinaryPrimitives.ReadInt16LittleEndian(bytes[7..9]);
            Height = BinaryPrimitives.ReadInt16LittleEndian(bytes[9..11]);

            byte bitsPerPixel = bytes[1];
            int format = BinaryPrimitives.ReadInt32LittleEndian(bytes[2..6]);
            bool isDXT1 = bitsPerPixel == 0x04 && format == 0x08;
            Format = isDXT1 ? ImageFormat.DXT1 : ImageFormat.DXT5;
        }

        private YARGImage(IntPtr data, int width, int height, int components)
        {
            Data = data;
            Length = width * height * components;

            Width = width;
            Height = height;
            Format = (ImageFormat) components;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (Handle.IsAllocated)
                {
                    Handle.Free();
                }
                else
                {
                    FreeNative(Data);
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~YARGImage()
        {
            Dispose(false);
        }

        [DllImport("STB2CSharp.dll", EntryPoint = "load_image_from_memory")]
        private static extern unsafe IntPtr LoadNative(byte* data, int length, out int width, out int height, out int components);

        [DllImport("STB2CSharp.dll", EntryPoint = "free_image")]
        private static extern IntPtr FreeNative(IntPtr image);
    }
}
