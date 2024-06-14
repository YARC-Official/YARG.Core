using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using YARG.Core.IO.Disposables;

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

        private readonly FixedArray<byte>? _handle;
        private bool _disposed;

        public static YARGImage? Load(FileInfo file)
        {
            using var bytes = MemoryMappedArray.Load(file);
            if (bytes == null)
            {
                return null;
            }
            return Load(bytes);
        }

        public static YARGImage? Load(SngFileListing listing, SngFile sngFile)
        {
            using var bytes = listing.LoadAllBytes(sngFile);
            if (bytes == null)
            {
                return null;
            }
            return Load(bytes);
        }

        private static YARGImage? Load(FixedArray<byte> file)
        {
            var result = LoadNative(file.Ptr, (int)file.Length, out int width, out int height, out int components);
            if (result == null)
            {
                return null;
            }
            return new YARGImage(result, width, height, components);
        }

        public unsafe YARGImage(FixedArray<byte> bytes)
        {
            _handle = bytes;
            Data = bytes.Ptr + 32;

            Width = *(short*)(bytes.Ptr + 7);
            Height = *(short*)(bytes.Ptr + 9);

            byte bitsPerPixel = bytes[1];
            int format = *(int*)(bytes.Ptr + 2);
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
                if (_handle != null)
                {
                    _handle.Dispose();
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
