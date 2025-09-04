using HarmonyWeaver.Core.Interfaces;
using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace HarmonyWeaver.Core.Implementation
{
    /// <summary>
    /// Assembly loader with configurable retry logic for handling file locking issues
    /// (Windows antivirus scanning, indexing services, etc.)
    /// </summary>
    public class RetryRuntimeAssemblyLoader : IRuntimeAssemblyLoader
    {
        private readonly int _maxAttempts;
        private readonly int _baseDelayMs;

        /// <summary>
        /// Initialize the retry assembly loader
        /// </summary>
        /// <param name="maxAttempts">Maximum number of retry attempts</param>
        /// <param name="baseDelayMs">Base delay in milliseconds for exponential backoff</param>
        public RetryRuntimeAssemblyLoader(int maxAttempts = 5, int baseDelayMs = 50)
        {
            if (maxAttempts <= 0)
                throw new ArgumentException("Max attempts must be greater than 0", nameof(maxAttempts));
            if (baseDelayMs < 0)
                throw new ArgumentException("Base delay must be non-negative", nameof(baseDelayMs));

            _maxAttempts = maxAttempts;
            _baseDelayMs = baseDelayMs;
        }

        public Assembly LoadAssembly(string assemblyPath)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentNullException(nameof(assemblyPath));

            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");

            Exception? lastException = null;

            for (int attempt = 0; attempt < _maxAttempts; attempt++)
            {
                try
                {
                    // Try to load the assembly
                    return Assembly.LoadFrom(assemblyPath);
                }
                catch (Exception ex) when (IsTransientFileError(ex) && attempt < _maxAttempts - 1)
                {
                    // File is likely locked by antivirus or other system services
                    lastException = ex;
                    
                    // Exponential backoff: baseDelay, baseDelay*2, baseDelay*4, etc.
                    var delay = _baseDelayMs * (1 << attempt);
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
                $"Failed to load assembly after {_maxAttempts} attempts: {assemblyPath}. " +
                $"This may be due to antivirus software, file indexing, or other system processes locking the file. " +
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
                   (ex is SystemException && (
                       ex.Message.Contains("locked", StringComparison.OrdinalIgnoreCase) ||
                       ex.Message.Contains("access", StringComparison.OrdinalIgnoreCase) ||
                       ex.Message.Contains("use", StringComparison.OrdinalIgnoreCase))); // "file is in use"
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
            catch (Exception ex) when (IsTransientFileError(ex))
            {
                return false;
            }
        }
    }
}