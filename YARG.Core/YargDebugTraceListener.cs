using System;
using System.Diagnostics;

namespace YARG.Core
{
    public class YargDebugTraceListener : IYargTraceListener
    {
        public void Assert(bool condition, string message)
        {
            Debug.Assert(condition, message);
        }

        public void LogMessage(YargTraceType type, string message)
        {
            Debug.WriteLine($"{type}: {message}");
        }

        public void LogException(Exception ex, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                Debug.Write(message);

            Debug.WriteLine(ex);
        }
    }
}