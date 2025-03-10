using StbImageSharp;
using System;

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
            _data = null,
            _width = 0,
            _height = 0,
            _format = 0,
        };

        private FixedArray<byte> _handle;
        private unsafe byte* _data;
        private int _width;
        private int _height;
        private ImageFormat _format;

        public readonly unsafe byte* Data => _data;
        public readonly int Width => _width;
        public readonly int Height => _height;
        public readonly ImageFormat Format => _format;
        public readonly unsafe bool IsAllocated => _handle.IsAllocated;

        public static YARGImage Load(string path)
        {
            using var bytes = FixedArray.LoadFile(path);
            return Load(in bytes);
        }

        public static unsafe YARGImage Load(in FixedArray<byte> file)
        {
            int comp;
            var image = Null;
            var context = new StbImage.stbi__context(file.Ptr, file.Length);
            if (StbImage.stbi__load_and_postprocess_8bit(&context, &image._width, &image._height, &comp, out image._handle))
            {
                image._data = image._handle.Ptr;
                image._format = (ImageFormat) comp;
            }
            return image;
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

        public void Dispose()
        {
            _handle.Dispose();
        }
    }
}
