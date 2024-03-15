using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace YARG.Core
{
    public enum YargTraceType
    {
        Info,
        Warning,
        Error,
        AssertFail,
    }

    public interface IYargTraceListener
    {
        void LogMessage(YargTraceType type, string? message);
        void LogException(Exception ex, string? message);
    }

    public static class YargTrace
    {
        private static readonly List<IYargTraceListener> _listeners = new();

        // public static void Assert(bool condition, string? message)
        // {
        //     if (condition)
        //         return;
        //
        //     foreach (var listener in _listeners)
        //     {
        //         listener.LogMessage(YargTraceType.AssertFail, message);
        //     }
        // }

        // public static void Fail(string? message)
        //     => Assert(false, message);
    }
}