using System;
using System.Buffers.Binary;
using System.Collections.Generic;
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

        private readonly FixedArray<byte>? Managed = null;
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
            using var bytes = listing.LoadAllBytes(sngFile);
            if (bytes == null)
            {
                return null;
            }
            return Load(bytes);
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

        public unsafe YARGImage(FixedArray<byte> managed)
        {
            Managed = managed;
            Data = (IntPtr)managed.Ptr + 32;
            Length = managed.Length - 32;

            for (int i = 32; i < managed.Length; i += 2)
            {
                (managed.Ptr[i + 1], managed.Ptr[i]) = (managed.Ptr[i], managed.Ptr[i + 1]);
            }

            Width = BinaryPrimitives.ReadInt16LittleEndian(managed.Slice(7, 2));
            Height = BinaryPrimitives.ReadInt16LittleEndian(managed.Slice(9, 2));

            byte bitsPerPixel = managed[1];
            int format = BinaryPrimitives.ReadInt32LittleEndian(managed.Slice(2, 4));
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
                if (Managed != null)
                {
                    if (disposing)
                    {
                        Managed.Dispose();
                    }
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
