using System;
using System.Runtime.InteropServices;

namespace YARG.Core.Song.Deserialization
{
    public unsafe static class MemoryManipulation
    {
        [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr MemCpy(void* dest, void* src, UIntPtr count);

        [DllImport("msvcrt.dll", EntryPoint = "memmove", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr MemMove(void* dest, void* src, UIntPtr count);
    }
}
