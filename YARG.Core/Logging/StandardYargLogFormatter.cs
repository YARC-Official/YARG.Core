using System;
using System.IO;
using Cysharp.Text;

namespace YARG.Core.Logging
{
    public class StandardYargLogFormatter : IYargLogFormatter
    {
        public void FormatLogItem(ref Utf16ValueStringBuilder output, LogItem item)
        {
            var source = item.Source.AsSpan();
            var separator = Path.DirectorySeparatorChar;

            int lastSeparatorIndex = source.LastIndexOf(separator);
            var fileName = source[(lastSeparatorIndex + 1)..];

            if (item.Level != LogLevel.Exception)
            {
                // "[Level] [Year-Month-Day HH:MM:SS File:Method:Line] Message"
                output.Append("[");

                // Append Level
                output.AppendFormat("{0}] [", item.Level.AsLevelString());

                // Append DateTime in format "Year-Month-Day Hour:Minute:Second"
                output.AppendFormat("{0:0000}-{1:00}-{2:00} {3:00}:{4:00}:{5:00} ",
                    item.Time.Year,
                    item.Time.Month,
                    item.Time.Day,
                    item.Time.Hour,
                    item.Time.Minute,
                    item.Time.Second);

                // Append File
                output.Append(fileName);

                // Append :Method:Line
                output.AppendFormat(":{0}:{1}] ", item.Method, item.Line);
            }
            else
            {
                output.Append("--------------- EXCEPTION ---------------\nat ");

                // Append File
                output.Append(fileName);

                output.AppendFormat(":{0}:{1}\n", item.Method, item.Line);
            }

            item.FormatMessage(ref output);

            if (item.Level == LogLevel.Exception)
            {
                output.AppendLine("\n-----------------------------------------");
            }
        }
    }
}