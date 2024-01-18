using System;
using System.Diagnostics;

namespace YARG.Core
{
    public class YargDebugTraceListener : IYargTraceListener
    {
        public void LogMessage(YargTraceType type, string? message)
        {
            if (type == YargTraceType.AssertFail)
                Debug.Fail(message);
            else
                Debug.WriteLine($"[{type}] {message}");
        }

        public void LogException(Exception ex, string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                Debug.Write(message);

            Debug.WriteLine(ex);
        }
    }
}