using System.Runtime.InteropServices;

namespace YARG.Core.Native
{
    public static partial class YARGNative
    {
        [DllImport(DLL_NAME, EntryPoint = "YARGCrashHandler_Install")]
        public static extern void CrashHandler_Install();

        [DllImport(DLL_NAME, EntryPoint = "YARGCrash")]
        public static extern void Crash();
    }
}