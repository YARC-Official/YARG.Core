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

    public unsafe class YARGImage : IDisposable
    {
        public readonly byte* Data;
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

        private static YARGImage? Load(FixedArray<byte> file)
        {
            var result = LoadNative(file.Ptr, file.Length, out int width, out int height, out int components);
            if (result == null)
            {
                return null;
            }
            return new YARGImage(result, width, height, components);
        }

        public unsafe YARGImage(byte[] bytes)
        {
            Handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            Data = (byte*)Handle.AddrOfPinnedObject() + 32;

            Width = BinaryPrimitives.ReadInt16LittleEndian(bytes[7..9]);
            Height = BinaryPrimitives.ReadInt16LittleEndian(bytes[9..11]);

            byte bitsPerPixel = bytes[1];
            int format = BinaryPrimitives.ReadInt32LittleEndian(bytes[2..6]);
            bool isDXT1 = bitsPerPixel == 0x04 && format == 0x08;
            Format = isDXT1 ? ImageFormat.DXT1 : ImageFormat.DXT5;
        }

        private YARGImage(byte* data, int width, int height, int components)
        {
            Data = data;
            Width = width;
            Height = height;
            Format = (ImageFormat) components;
        }

        private void _Dispose()
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
            _Dispose();
            GC.SuppressFinalize(this);
        }

        ~YARGImage()
        {
            _Dispose();
        }

        [DllImport("STB2CSharp", EntryPoint = "load_image_from_memory")]
        private static extern byte* LoadNative(byte* data, int length, out int width, out int height, out int components);

        [DllImport("STB2CSharp", EntryPoint = "free_image")]
        private static extern void FreeNative(byte* image);
    }
}
