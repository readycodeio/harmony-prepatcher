using HarmonyWeaver.Core.Interfaces;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace HarmonyWeaver.Core.Implementation
{
    /// <summary>
    /// Mono.Cecil assembly loader with configurable retry logic for handling file locking issues
    /// (Windows antivirus scanning, indexing services, etc.)
    /// </summary>
    public class RetryAssemblyLoader : IAssemblyLoader
    {
        private readonly List<AssemblyDefinition> _loadedAssemblies = new List<AssemblyDefinition>();
        private readonly ReaderParameters _readOnlyParameters;
        private readonly ReaderParameters _readWriteParameters;
        private readonly int _maxAttempts;
        private readonly int _baseDelayMs;

        /// <summary>
        /// Initialize the retry assembly loader
        /// </summary>
        /// <param name="maxAttempts">Maximum number of retry attempts</param>
        /// <param name="baseDelayMs">Base delay in milliseconds for exponential backoff</param>
        public RetryAssemblyLoader(int maxAttempts = 5, int baseDelayMs = 50)
        {
            if (maxAttempts <= 0)
                throw new ArgumentException("Max attempts must be greater than 0", nameof(maxAttempts));
            if (baseDelayMs < 0)
                throw new ArgumentException("Base delay must be non-negative", nameof(baseDelayMs));

            _maxAttempts = maxAttempts;
            _baseDelayMs = baseDelayMs;
            
            // Read-only parameters for scanning/discovery (less likely to conflict)
            _readOnlyParameters = new ReaderParameters
            {
                ReadWrite = false,
                InMemory = true,
                ReadingMode = ReadingMode.Immediate
            };

            // Read-write parameters for patching (when we need to modify)
            _readWriteParameters = new ReaderParameters
            {
                ReadWrite = true,
                InMemory = true,
                ReadingMode = ReadingMode.Immediate
            };
        }

        public AssemblyDefinition LoadAssembly(string assemblyPath)
        {
            // For target assemblies that need patching, we need read-write access
            // Try with sharing-friendly file stream first
            return LoadAssemblyWithSharedStream(assemblyPath);
        }

        /// <summary>
        /// Load assembly for patching (requires read-write access)
        /// </summary>
        public AssemblyDefinition LoadAssemblyForPatching(string assemblyPath)
        {
            return LoadAssemblyWithParameters(assemblyPath, _readWriteParameters, "read-write");
        }

        /// <summary>
        /// Load assembly for reading only (scanning patches, less likely to conflict)
        /// </summary>
        public AssemblyDefinition LoadAssemblyForReading(string assemblyPath)
        {
            return LoadAssemblyWithParameters(assemblyPath, _readOnlyParameters, "read-only");
        }

        private AssemblyDefinition LoadAssemblyWithParameters(string assemblyPath, ReaderParameters parameters, string mode)
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
                    var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, parameters);
                    _loadedAssemblies.Add(assembly);
                    return assembly;
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
                $"Failed to load assembly in {mode} mode after {_maxAttempts} attempts: {assemblyPath}. " +
                $"This may be due to antivirus software, file indexing, or other system processes locking the file. " +
                $"Last error: {lastException?.Message}", 
                lastException);
        }

        public IEnumerable<AssemblyDefinition> LoadAssemblies(IEnumerable<string> assemblyPaths)
        {
            if (assemblyPaths == null)
                throw new ArgumentNullException(nameof(assemblyPaths));

            var paths = assemblyPaths.ToList();
            var assemblies = new List<AssemblyDefinition>();

            foreach (var path in paths)
            {
                assemblies.Add(LoadAssembly(path));
            }

            return assemblies;
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

        public void Dispose()
        {
            foreach (var assembly in _loadedAssemblies)
            {
                assembly?.Dispose();
            }
            _loadedAssemblies.Clear();
        }
    }
}