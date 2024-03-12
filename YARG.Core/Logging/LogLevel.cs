namespace YARG.Core.Logging
{
    public enum LogLevel
    {
        //Exception,
        Error,
        Warning,
        Info,
        Debug,
        Trace,
    }

    public static class LogLevelExtensions
    {
        public static string AsLevelString(this LogLevel level)
        {
            return level switch
            {
                //LogLevel.Exception => "Exception",
                LogLevel.Error     => "Error",
                LogLevel.Warning   => "Warning",
                LogLevel.Info      => "Info",
                LogLevel.Debug     => "Debug",
                LogLevel.Trace     => "Trace",
                _                  => "UNKNOWN",
            };
        }
    }
}