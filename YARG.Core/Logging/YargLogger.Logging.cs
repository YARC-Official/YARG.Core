using System;
using System.Runtime.CompilerServices;
using Cysharp.Text;

namespace YARG.Core.Logging
{
    public static partial class YargLogger
    {
        public static void LogException(Exception ex, string? message, [CallerFilePath] string source = "", [CallerLineNumber] int line = -1, [CallerMemberName] string member = "")
        {
            const string exceptionLog = "{0}\n{1}";
            message ??= ex.Message;

            var builder = ZString.CreateStringBuilder();
            builder.AppendFormat(exceptionLog, message, ex.StackTrace);

            AddLogItemToQueue(LogLevel.Exception, source, line, member, builder.ToString());
        }
    }
}