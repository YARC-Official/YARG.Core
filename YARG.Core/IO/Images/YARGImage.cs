using System;
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

    public struct YARGImage : IDisposable
    {
        public static readonly YARGImage Null = new()
        {
            _handle = FixedArray<byte>.Null,
            _disposed = true,
            _data = null,
            _width = 0,
            _height = 0,
            _format = 0,
        };

        private FixedArray<byte> _handle;
        private bool _disposed;

        private unsafe byte* _data;
        private int _width;
        private int _height;
        private ImageFormat _format;

        public readonly unsafe byte* Data => _data;
        public readonly int Width => _width;
        public readonly int Height => _height;
        public readonly ImageFormat Format => _format;
        public readonly unsafe bool IsAllocated => _data != null;

        public static YARGImage Load(string path)
        {
            using var bytes = FixedArray.LoadFile(path);
            return Load(in bytes);
        }

        public static unsafe YARGImage Load(in FixedArray<byte> file)
        {
            var result = LoadNative(file.Ptr, (int) file.Length, out int width, out int height, out int components);
            if (result == null)
            {
                return Null;
            }
            return new YARGImage()
            {
                _data = result,
                _width = width,
                _height = height,
                _format = (ImageFormat) components
            };
        }

        public static YARGImage LoadDXT(string path)
        {
            var data = FixedArray.LoadFile(path);
            return TransferDXT(ref data);
        }

        public static unsafe YARGImage TransferDXT(ref FixedArray<byte> file)
        {
            byte bitsPerPixel = file[1];
            int format = *(int*) (file.Ptr + 2);
            bool isDXT1 = bitsPerPixel == 0x04 && format == 0x08;
            return new YARGImage()
            {
                _handle = file.TransferOwnership(),
                _data = file.Ptr + 32,
                _width = *(short*) (file.Ptr + 7),
                _height = *(short*) (file.Ptr + 9),
                _format = isDXT1 ? ImageFormat.DXT1 : ImageFormat.DXT5
            };
        }

        private void _Dispose()
        {
            
            if (!_disposed)
            {
                unsafe
                {
                    if (_handle.IsAllocated)
                    {
                        _handle.Dispose();
                    }
                    else if (_data != null)
                    {
                        FreeNative(_data);
                    }
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            _Dispose();
            GC.SuppressFinalize(this);
        }

        [DllImport("STB2CSharp", EntryPoint = "load_image_from_memory")]
        private static extern unsafe byte* LoadNative(byte* data, int length, out int width, out int height, out int components);

        [DllImport("STB2CSharp", EntryPoint = "free_image")]
        private static extern unsafe void FreeNative(byte* image);
    }
}
