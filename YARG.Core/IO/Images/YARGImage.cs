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

    public class YARGImage : IDisposable
    {
        private FixedArray<byte>? _handle;

        public unsafe byte*       Data { get; private set; }

        public        int         Width { get; private set; }

        public        int         Height { get; private set; }

        public        ImageFormat Format { get; private set; }

        public static YARGImage? Load(string path)
        {
            using var bytes = FixedArray.LoadFile(path);
            return Load(bytes);
        }

        public static unsafe YARGImage? Load(FixedArray<byte> file)
        {
            var result = LoadNative(file.Ptr, (int) file.Length, out int width, out int height, out int components);
            if (result == null)
            {
                return null;
            }
            return new YARGImage
            {
                Data = result,
                Width = width,
                Height = height,
                Format = (ImageFormat) components
            };
        }

        public static YARGImage LoadDXT(string path)
        {
            using var data = FixedArray.LoadFile(path);
            return TransferDXT(data);
        }

        public static unsafe YARGImage TransferDXT(FixedArray<byte> file)
        {
            byte bitsPerPixel = file[1];
            int format = *(int*) (file.Ptr + 2);
            bool isDXT1 = bitsPerPixel == 0x04 && format == 0x08;
            return new YARGImage()
            {
                _handle = file.TransferOwnership(),
                Data = file.Ptr + 32,
                Width = *(short*) (file.Ptr + 7),
                Height = *(short*) (file.Ptr + 9),
                Format = isDXT1 ? ImageFormat.DXT1 : ImageFormat.DXT5
            };
        }

        private void _Dispose(bool disposing)
        {
            unsafe
            {
                if (Data == null)
                {
                    return;
                }

                if (_handle == null)
                {
                    FreeNative(Data);
                }
                else if (disposing)
                {
                    _handle.Dispose();
                }
                Data = null;
            }
        }

        public void Dispose()
        {
            _Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~YARGImage()
        {
            _Dispose(false);
        }

        [DllImport("STB2CSharp", EntryPoint = "load_image_from_memory")]
        private static extern unsafe byte* LoadNative(byte* data, int length, out int width, out int height, out int components);

        [DllImport("STB2CSharp", EntryPoint = "free_image")]
        private static extern unsafe void FreeNative(byte* image);
    }
}
