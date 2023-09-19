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
    }

    public interface IYargTraceListener
    {
        void Assert(bool condition, string? message);
        void LogMessage(YargTraceType type, string? message);
        void LogException(Exception ex, string? message);
    }

    public static class YargTrace
    {
        private static readonly List<IYargTraceListener> _listeners = new();

        public static void AddListener(IYargTraceListener listener)
        {
            _listeners.Add(listener);
        }

        public static void RemoveListener(IYargTraceListener listener)
        {
            _listeners.Remove(listener);
        }

        public static void Assert(bool condition, string? message)
        {
            foreach (var listener in _listeners)
            {
                listener.Assert(condition, message);
            }
        }

        public static void Fail(string? message)
            => Assert(false, message);

        public static void LogMessage(YargTraceType type, string? message)
        {
            foreach (var listener in _listeners)
            {
                listener.LogMessage(type, message);
            }
        }

        public static void LogInfo(string? message)
            => LogMessage(YargTraceType.Info, message);

        public static void LogWarning(string? message)
            => LogMessage(YargTraceType.Warning, message);

        public static void LogError(string? message)
            => LogMessage(YargTraceType.Error, message);

        public static void LogException(Exception ex, string? message)
        {
            foreach (var listener in _listeners)
            {
                listener.LogException(ex, message);
            }
        }

        [Conditional("DEBUG")]
        public static void DebugAssert(bool condition, string? message)
            => Assert(condition, message);

        [Conditional("DEBUG")]
        public static void DebugFail(string? message)
            => Fail(message);

        [Conditional("DEBUG")]
        public static void DebugMessage(YargTraceType type, string? message)
            => LogMessage(type, message);

        [Conditional("DEBUG")]
        public static void DebugInfo(string? message)
            => LogInfo(message);

        [Conditional("DEBUG")]
        public static void DebugWarning(string? message)
            => LogWarning(message);

        [Conditional("DEBUG")]
        public static void DebugError(string? message)
            => LogError(message);

        [Conditional("DEBUG")]
        public static void DebugException(Exception ex, string? message)
            => LogException(ex, message);
    }
}