using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace HarmonyWeaver.Core.Loading
{
    /// <summary>
    /// Assembly loader with Windows-specific retry logic for antivirus/file locking issues
    /// </summary>
    public static class WindowsAssemblyLoader
    {
        /// <summary>
        /// Load assembly with retry logic to handle Windows file locking issues
        /// (antivirus scanning, indexing services, etc.)
        /// </summary>
        /// <param name="assemblyPath">Path to the assembly to load</param>
        /// <param name="maxAttempts">Maximum number of retry attempts</param>
        /// <param name="baseDelayMs">Base delay in milliseconds (exponential backoff)</param>
        /// <returns>Loaded assembly</returns>
        public static Assembly LoadFromWithRetry(string assemblyPath, int maxAttempts = 5, int baseDelayMs = 50)
        {
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");

            Exception? lastException = null;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    // Try to load the assembly
                    return Assembly.LoadFrom(assemblyPath);
                }
                catch (Exception ex) when (IsTransientFileError(ex) && attempt < maxAttempts - 1)
                {
                    // File is likely locked by antivirus or other Windows services
                    lastException = ex;
                    
                    // Exponential backoff: 50ms, 100ms, 200ms, 400ms
                    var delay = baseDelayMs * (1 << attempt);
                    Thread.Sleep(delay);
                }
                catch (BadImageFormatException ex)
                {
                    // This is a real error, not a timing issue - don't retry
                    throw new InvalidOperationException($"Invalid assembly format: {assemblyPath}", ex);
                }
                catch (Exception ex)
                {
                    // Non-transient error or final attempt
                    lastException = ex;
                    break;
                }
            }

            // All attempts failed
            throw new InvalidOperationException(
                $"Failed to load assembly after {maxAttempts} attempts: {assemblyPath}. " +
                $"This may be due to Windows antivirus software or file system delays. " +
                $"Last error: {lastException?.Message}", 
                lastException);
        }

        /// <summary>
        /// Determine if an exception represents a transient file locking issue that should be retried
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
        /// Check if a file is ready for reading (not locked)
        /// </summary>
        public static bool IsFileReady(string filePath)
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Wait for a file to become available for reading
        /// </summary>
        public static void WaitForFileReady(string filePath, int timeoutMs = 5000, int checkIntervalMs = 50)
        {
            var startTime = DateTime.UtcNow;
            
            while (DateTime.UtcNow.Subtract(startTime).TotalMilliseconds < timeoutMs)
            {
                if (IsFileReady(filePath))
                    return;
                
                Thread.Sleep(checkIntervalMs);
            }
            
            throw new TimeoutException($"File was not ready for reading within {timeoutMs}ms: {filePath}");
        }
    }
}