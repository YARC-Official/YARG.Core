using System;
using System.Runtime.CompilerServices;

namespace YARG.Core.Logging
{
    public static partial class YargLogger
    {
        public static void LogException(Exception ex, string? message, [CallerFilePath] string source = "", [CallerLineNumber] int line = -1, [CallerMemberName] string member = "")
        {
            const string exceptionLog = "{0}\n{1}";
            AddLogItemToQueue(LogLevel.Exception, exceptionLog, source, line, member, message, ex);
        }
    }
}