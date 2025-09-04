using System;
using System.IO;

namespace HarmonyWeaver.Core.Logging
{
    /// <summary>
    /// Simple file-based logger that works across assembly boundaries
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();

        public FileLogger(string logFilePath)
        {
            _logFilePath = logFilePath;
            
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public void LogInfo(string message)
        {
            WriteToFile($"[INFO] {DateTime.Now:HH:mm:ss.fff} {message}");
        }

        public void LogWarning(string message)
        {
            WriteToFile($"[WARN] {DateTime.Now:HH:mm:ss.fff} {message}");
        }

        public void LogError(string message)
        {
            WriteToFile($"[ERROR] {DateTime.Now:HH:mm:ss.fff} {message}");
        }

        private void WriteToFile(string logEntry)
        {
            try
            {
                lock (_lockObject)
                {
                    // Use synchronous file operations with explicit flushing
                    using var writer = new StreamWriter(_logFilePath, append: true);
                    writer.WriteLine(logEntry);
                    writer.Flush(); // Ensure data is written immediately
                }
            }
            catch
            {
                // Ignore logging errors to avoid breaking the main functionality
            }
        }

        /// <summary>
        /// Read all log entries from the file
        /// </summary>
        public string[] ReadAllEntries()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    return File.ReadAllLines(_logFilePath);
                }
            }
            catch
            {
                // Ignore errors
            }
            
            return new string[0];
        }

        /// <summary>
        /// Clear the log file
        /// </summary>
        public void Clear()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    File.Delete(_logFilePath);
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        /// <summary>
        /// Check if the log contains a specific message
        /// </summary>
        public bool ContainsMessage(string message)
        {
            var entries = ReadAllEntries();
            foreach (var entry in entries)
            {
                if (entry.Contains(message))
                    return true;
            }
            return false;
        }
    }
}