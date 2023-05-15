using System.Reflection;
using NAudio.Midi;

namespace YARG.Core
{
    public static class HelperMethods
    {

        public static byte[] GetSysexData(SysexEvent sysex)
        {
            var field = typeof(SysexEvent).GetField("data", BindingFlags.NonPublic | BindingFlags.Instance);
            return (byte[])field?.GetValue(sysex);
        }
        
    }
}