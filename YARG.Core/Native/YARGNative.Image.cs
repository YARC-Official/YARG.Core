using System.Runtime.InteropServices;

namespace YARG.Core.Native
{
    internal static unsafe partial class YARGNative
    {
        [DllImport(DLL_NAME, EntryPoint = "YARGImage_Load")]
        public static extern byte* Image_Load(byte* data, int length, out int width, out int height, out int components);

        [DllImport(DLL_NAME, EntryPoint = "YARGImage_Free")]
        public static extern void Image_Free(byte* image);
    }
}