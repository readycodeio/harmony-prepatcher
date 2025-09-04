using System.Collections.Generic;
using System.Collections.Concurrent;

namespace HarmonyWeaver.Core.Logging
{
    /// <summary>
    /// Test logger implementation that captures log messages for verification
    /// </summary>
    public class TestLogger : ILogger
    {
        private readonly ConcurrentQueue<LogEntry> _logEntries = new ConcurrentQueue<LogEntry>();

        /// <summary>
        /// All log entries captured by this logger
        /// </summary>
        public IEnumerable<LogEntry> LogEntries => _logEntries.ToArray();

        public void LogInfo(string message)
        {
            _logEntries.Enqueue(new LogEntry(LogLevel.Info, message));
        }

        public void LogWarning(string message)
        {
            _logEntries.Enqueue(new LogEntry(LogLevel.Warning, message));
        }

        public void LogError(string message)
        {
            _logEntries.Enqueue(new LogEntry(LogLevel.Error, message));
        }

        /// <summary>
        /// Clear all captured log entries
        /// </summary>
        public void Clear()
        {
            while (_logEntries.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Get the count of log entries
        /// </summary>
        public int Count => _logEntries.Count;

        /// <summary>
        /// Check if any log entry contains the specified message
        /// </summary>
        public bool ContainsMessage(string message)
        {
            foreach (var entry in _logEntries)
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
            foreach (var entry in _logEntries)
            {
                if (entry.Level == level && entry.Message.Contains(message))
                    return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Represents a log entry
    /// </summary>
    public class LogEntry
    {
        public LogLevel Level { get; }
        public string Message { get; }

        public LogEntry(LogLevel level, string message)
        {
            Level = level;
            Message = message;
        }

        public override string ToString() => $"[{Level}] {Message}";
    }

    /// <summary>
    /// Log levels
    /// </summary>
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }
}