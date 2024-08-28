using System;
using System.Buffers.Binary;
using System.IO;
using YARG.Core.IO.Disposables;

namespace YARG.Core.IO
{
    public enum ImageFormat
    {
        Grayscale,
        Grayscale_Alpha,
        RGB,
        RGBA,
        DXT1,
        DXT5,
    }

    public interface IImageDecoder
    {
        unsafe FixedArray<byte>? Decode(byte* data, int length, out int width, out int height, out ImageFormat format);
    }

    public unsafe class YARGImage : IDisposable
    {
        public static IImageDecoder? Decoder { get; set; }

        public readonly byte* Data;
        public readonly long DataLength;

        public readonly int Width;
        public readonly int Height;
        public readonly ImageFormat Format;

        private readonly FixedArray<byte> _handle;
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

        public static YARGImage? Load(in SngFileListing listing, SngFile sngFile)
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
            var decoder = Decoder;
            if (decoder == null)
            {
                return null;
            }

            var result = decoder.Decode(file.Ptr, (int) file.Length, out int width, out int height, out var format);
            if (result == null)
            {
                return null;
            }

            return new YARGImage(result, width, height, format);
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

        private YARGImage(FixedArray<byte> handle, int width, int height, ImageFormat format)
            : this(handle, handle.Ptr, handle.Length, width, height, format)
        { }

        private YARGImage(FixedArray<byte> handle, byte* data, long length, int width, int height, ImageFormat format)
        {
            _handle = handle;
            Data = data;
            DataLength = length;
            Width = width;
            Height = height;
            Format = format;
        }

        private void _Dispose()
        {
            if (!_disposed)
            {
                _handle?.Dispose();
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
    }
}
