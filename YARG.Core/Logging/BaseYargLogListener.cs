using Cysharp.Text;

namespace YARG.Core.Logging
{
    public abstract class BaseYargLogListener
    {
        private readonly IYargLogFormatter _formatter;

        protected BaseYargLogListener(IYargLogFormatter formatter)
        {
            _formatter = formatter;
        }

        public abstract void WriteLogItem(ref Utf16ValueStringBuilder builder);

        public void FormatLogItem(ref Utf16ValueStringBuilder builder, LogItem item)
        {
            _formatter.FormatLogItem(ref builder, item);
        }
    }
}