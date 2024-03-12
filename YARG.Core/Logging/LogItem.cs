using System;

namespace YARG.Core.Logging
{
    public class LogItem
    {
        public LogLevel Level;

        public string Message = "";
        public string Source  = "";
        public string Method  = "";

        public int Line = -1;

        public DateTime Time;

        public string Format = "";
        public object?[] Args = new object[10];
    }
}