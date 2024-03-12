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

            if (!string.IsNullOrEmpty(item.Message))
            {
                output.Append(item.Message);
            }
            else if (!string.IsNullOrEmpty(item.Format))
            {
                var argCount = Array.IndexOf(item.Args, null);

                for(int i = argCount; i < item.Args.Length; i++)
                {
                    item.Args[i] = null;
                }

                output.AppendFormat(item.Format, item.Args[0], item.Args[1], item.Args[2], item.Args[3], item.Args[4],
                    item.Args[5], item.Args[6], item.Args[7], item.Args[8], item.Args[9]);
            }
        }
    }
}