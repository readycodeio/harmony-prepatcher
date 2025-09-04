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
            // Retry logic for Windows file locking issues
            var maxAttempts = 3;
            var baseDelay = 10; // Start with 10ms
            
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    lock (_lockObject)
                    {
                        // Use FileStream with explicit control over file sharing and flushing
                        using var fileStream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                        using var writer = new StreamWriter(fileStream);
                        writer.WriteLine(logEntry);
                        writer.Flush();
                        fileStream.Flush(true); // Force OS-level flush
                    }
                    return; // Success, exit retry loop
                }
                catch (Exception ex) when (IsTransientFileError(ex) && attempt < maxAttempts - 1)
                {
                    // File might be locked, wait briefly and retry
                    // Use exponential backoff: 10ms, 20ms, 40ms
                    System.Threading.Thread.Sleep(baseDelay * (1 << attempt));
                }
                catch
                {
                    // Other exceptions or final attempt - ignore to avoid breaking main functionality
                    return;
                }
            }
        }

        /// <summary>
        /// Determine if an exception represents a transient file locking issue
        /// </summary>
        private static bool IsTransientFileError(Exception ex)
        {
            return ex is IOException ||
                   ex is UnauthorizedAccessException ||
                   ex is InvalidOperationException ||
                   (ex is SystemException && ex.Message.Contains("locked")) ||
                   (ex is SystemException && ex.Message.Contains("access"));
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