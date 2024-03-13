﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Text;

namespace YARG.Core.Logging
{
    public static partial class YargLogger
    {
        // How often the logging thread should output logs (milliseconds)
        private const int LOG_INTERVAL = 10;

        private static readonly List<BaseYargLogListener> Listeners;

        // Queue for log items. Maybe we should use a concurrent queue? Depends on how many threads will log at the same time
        private static readonly Queue<LogItem> LogQueue;

        private static readonly ConcurrentBag<LogItem> LogPool;

        /// <summary>
        /// Any <see cref="LogItem"/> with a level lower than this will not be logged
        /// </summary>
        public static LogLevel MinimumLogLevel;

        private static Utf16ValueStringBuilder _logBuilder;

        private static bool _isLoggingEnabled;

        static YargLogger()
        {
            Listeners = new List<BaseYargLogListener>();
            LogQueue = new Queue<LogItem>();

            LogPool = new ConcurrentBag<LogItem>();

            _logBuilder = ZString.CreateStringBuilder();

            var logOutputterThread = new Thread(LogOutputter);
            logOutputterThread.Start();

            MinimumLogLevel = LogLevel.Info;
            _isLoggingEnabled = true;
        }

        /// <summary>
        /// Add a new listener to the logger. This listener will receive all log items.
        /// </summary>
        public static void AddLogListener(BaseYargLogListener listener)
        {
            lock (Listeners)
            {
                Listeners.Add(listener);
            }
        }

        /// <summary>
        /// Remove a listener from the logger. This listener will no longer receive log items.
        /// </summary>
        public static void RemoveLogListener(BaseYargLogListener listener)
        {
            lock (Listeners)
            {
                Listeners.Remove(listener);
            }

            listener.Dispose();
        }

        /// <summary>
        /// This method will stop the logging thread and prevent any further log items from being queued.
        /// </summary>
        /// <remarks>
        /// This should be called when the application is shutting down to prevent any log items from being lost.
        /// </remarks>
        public static void KillLogger()
        {
            _isLoggingEnabled = false;

            lock (LogQueue)
            {
                while (LogQueue.TryDequeue(out var item))
                {
                    // Send it to all listeners that are currently registered
                    lock (Listeners)
                    {
                        foreach (var listener in Listeners)
                        {
                            _logBuilder.Clear();
                            listener.FormatLogItem(ref _logBuilder, item);
                            listener.WriteLogItem(ref _logBuilder, item);
                        }
                    }

                    LogPool.Add(item);
                }
            }

            foreach(var listener in Listeners)
            {
                listener.Dispose();
            }
        }

        private static void LogOutputter()
        {
            // Keep thread running until logging is disabled and the queue is empty
            // In the event logging is disabled, we still want to process all remaining log items
            while (_isLoggingEnabled || LogQueue.Count > 0)
            {
                // Lock the queue and process all items
                lock (LogQueue)
                {
                    while (LogQueue.TryDequeue(out var item))
                    {
                        // Send it to all listeners that are currently registered
                        foreach (var listener in Listeners)
                        {
                            _logBuilder.Clear();
                            listener.FormatLogItem(ref _logBuilder, item);
                            listener.WriteLogItem(ref _logBuilder, item);
                        }

                        LogPool.Add(item);
                    }
                }

                // Sleep for a short time. Logs will process at most every LOG_INTERVAL milliseconds
                Thread.Sleep(LOG_INTERVAL);
            }
        }

        private static void AddLogItemToQueue(LogLevel level, string source, int line, string method,
            string message = "")
        {
            // If logging is disabled, don't queue anymore log items
            // This will usually happen when the application is shutting down
            if (!_isLoggingEnabled)
            {
                return;
            }

            // If there's no available log items in the pool, create a new one
            if (!LogPool.TryTake(out var item))
            {
                item = new LogItem();
            }

            item.Level = level;
            item.Message = message;
            item.Source = source;
            item.Method = method;
            item.Line = line;
            item.Time = DateTime.Now;

            // Lock while enqueuing. This prevents the log outputter from processing the queue while we're adding to it
            lock(LogQueue)
            {
                LogQueue.Enqueue(item);
            }
        }
    }
}