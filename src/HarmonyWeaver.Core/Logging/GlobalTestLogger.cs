using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace HarmonyWeaver.Core.Logging
{
    /// <summary>
    /// Global test logger that works across assembly boundaries using static concurrent collections
    /// This avoids file I/O issues while providing cross-assembly logging capability
    /// </summary>
    public class GlobalTestLogger : ILogger
    {
        private static readonly ConcurrentDictionary<string, ConcurrentQueue<LogEntry>> _globalLogQueues 
            = new ConcurrentDictionary<string, ConcurrentQueue<LogEntry>>();

        private readonly string _loggerId;
        private readonly ConcurrentQueue<LogEntry> _logQueue;

        public GlobalTestLogger(string loggerId)
        {
            _loggerId = loggerId;
            _logQueue = _globalLogQueues.GetOrAdd(loggerId, _ => new ConcurrentQueue<LogEntry>());
        }

        public void LogInfo(string message)
        {
            _logQueue.Enqueue(new LogEntry(LogLevel.Info, message));
        }

        public void LogWarning(string message)
        {
            _logQueue.Enqueue(new LogEntry(LogLevel.Warning, message));
        }

        public void LogError(string message)
        {
            _logQueue.Enqueue(new LogEntry(LogLevel.Error, message));
        }

        /// <summary>
        /// Get all log entries for this logger
        /// </summary>
        public IEnumerable<LogEntry> LogEntries => _logQueue.ToArray();

        /// <summary>
        /// Clear all log entries for this logger
        /// </summary>
        public void Clear()
        {
            while (_logQueue.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Get the count of log entries
        /// </summary>
        public int Count => _logQueue.Count;

        /// <summary>
        /// Check if any log entry contains the specified message
        /// </summary>
        public bool ContainsMessage(string message)
        {
            foreach (var entry in _logQueue)
            {
                if (entry.Message.Contains(message))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if any log entry contains the specified message at the specified level
        /// </summary>
        public bool ContainsMessage(LogLevel level, string message)
        {
            foreach (var entry in _logQueue)
            {
                if (entry.Level == level && entry.Message.Contains(message))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get a global logger by ID (works across assembly boundaries)
        /// </summary>
        public static GlobalTestLogger GetGlobalLogger(string loggerId)
        {
            return new GlobalTestLogger(loggerId);
        }

        /// <summary>
        /// Clear all global log queues (for cleanup)
        /// </summary>
        public static void ClearAllGlobalLogs()
        {
            _globalLogQueues.Clear();
        }

        /// <summary>
        /// Get all logger IDs currently in use
        /// </summary>
        public static IEnumerable<string> GetActiveLoggerIds()
        {
            return _globalLogQueues.Keys.ToArray();
        }
    }
}