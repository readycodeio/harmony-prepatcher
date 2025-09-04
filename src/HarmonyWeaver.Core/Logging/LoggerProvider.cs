using System.Collections.Concurrent;
using System.Threading;

namespace HarmonyWeaver.Core.Logging
{
    /// <summary>
    /// Thread-safe logger provider for patch methods to access logging
    /// Uses thread-local storage to avoid conflicts in parallel test execution
    /// </summary>
    public static class LoggerProvider
    {
        private static readonly ThreadLocal<ILogger?> _threadLocalLogger = new ThreadLocal<ILogger?>();
        private static readonly ConcurrentDictionary<string, ILogger> _namedLoggers = new ConcurrentDictionary<string, ILogger>();
        private static ILogger? _globalLogger;

        /// <summary>
        /// Set the logger instance for the current thread
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public static void SetLogger(ILogger logger)
        {
            _threadLocalLogger.Value = logger;
        }

        /// <summary>
        /// Set a named logger that can be accessed by name
        /// </summary>
        /// <param name="name">Unique name for the logger</param>
        /// <param name="logger">The logger instance</param>
        public static void SetNamedLogger(string name, ILogger logger)
        {
            _namedLoggers[name] = logger;
        }

        /// <summary>
        /// Get a named logger
        /// </summary>
        /// <param name="name">The name of the logger</param>
        /// <returns>The logger instance or null if not found</returns>
        public static ILogger? GetNamedLogger(string name)
        {
            _namedLoggers.TryGetValue(name, out var logger);
            return logger;
        }

        /// <summary>
        /// Set the global fallback logger
        /// </summary>
        /// <param name="logger">The logger instance</param>
        public static void SetGlobalLogger(ILogger logger)
        {
            _globalLogger = logger;
        }

        /// <summary>
        /// Get the current logger instance (thread-local first, then global, then null logger)
        /// </summary>
        public static ILogger Logger => 
            _threadLocalLogger.Value ?? 
            _globalLogger ?? 
            new NullLogger();

        /// <summary>
        /// Clear the current thread's logger
        /// </summary>
        public static void ClearLogger()
        {
            _threadLocalLogger.Value = null;
        }

        /// <summary>
        /// Clear all loggers
        /// </summary>
        public static void ClearAllLoggers()
        {
            _threadLocalLogger.Value = null;
            _globalLogger = null;
            _namedLoggers.Clear();
            GlobalTestLogger.ClearAllGlobalLogs();
        }
    }

    /// <summary>
    /// Null logger that does nothing (used as default)
    /// </summary>
    internal class NullLogger : ILogger
    {
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message) { }
    }
}