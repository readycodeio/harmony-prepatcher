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
    /// Flexible Mono.Cecil assembly loader with explicit control over loading options
    /// Handles Windows file locking issues with configurable retry logic
    /// </summary>
    public class FlexibleCecilAssemblyLoader : ICecilAssemblyLoader
    {
        private readonly List<AssemblyDefinition> _loadedAssemblies = new List<AssemblyDefinition>();
        private readonly int _defaultBaseDelayMs;

        /// <summary>
        /// Initialize the flexible Cecil assembly loader
        /// </summary>
        /// <param name="defaultBaseDelayMs">Default base delay in milliseconds for retry logic</param>
        public FlexibleCecilAssemblyLoader(int defaultBaseDelayMs = 25)
        {
            if (defaultBaseDelayMs < 0)
                throw new ArgumentException("Base delay must be non-negative", nameof(defaultBaseDelayMs));

            _defaultBaseDelayMs = defaultBaseDelayMs;
        }

        public AssemblyDefinition LoadAssembly(string assemblyPath, bool readWrite = true, int maxRetries = 5)
        {
            var parameters = CreateReaderParameters(readWrite);
            return LoadAssemblyWithRetry(assemblyPath, parameters, maxRetries, readWrite ? "read-write" : "read-only");
        }

        public IEnumerable<AssemblyDefinition> LoadAssemblies(IEnumerable<string> assemblyPaths, bool readWrite = true, int maxRetries = 5)
        {
            if (assemblyPaths == null)
                throw new ArgumentNullException(nameof(assemblyPaths));

            var assemblies = new List<AssemblyDefinition>();
            foreach (var path in assemblyPaths)
            {
                assemblies.Add(LoadAssembly(path, readWrite, maxRetries));
            }
            return assemblies;
        }

        public AssemblyDefinition LoadAssemblyForScanning(string assemblyPath, int maxRetries = 10)
        {
            // Read-only for scanning patch assemblies - reduces Windows file locking conflicts
            return LoadAssembly(assemblyPath, readWrite: false, maxRetries: maxRetries);
        }

        public AssemblyDefinition LoadAssemblyForPatching(string assemblyPath, int maxRetries = 10)
        {
            // Read-write for target assemblies that need IL modification
            return LoadAssembly(assemblyPath, readWrite: true, maxRetries: maxRetries);
        }

        private ReaderParameters CreateReaderParameters(bool readWrite)
        {
            return new ReaderParameters
            {
                ReadWrite = readWrite,
                InMemory = true,    // Load into memory to release file handle quickly
                ReadingMode = ReadingMode.Immediate
            };
        }

        private AssemblyDefinition LoadAssemblyWithRetry(string assemblyPath, ReaderParameters parameters, int maxRetries, string mode)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
                throw new ArgumentNullException(nameof(assemblyPath));

            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");

            if (maxRetries <= 0)
                throw new ArgumentException("Max retries must be greater than 0", nameof(maxRetries));

            Exception? lastException = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, parameters);
                    _loadedAssemblies.Add(assembly);
                    return assembly;
                }
                catch (Exception ex) when (IsTransientFileError(ex) && attempt < maxRetries - 1)
                {
                    // File is likely locked by antivirus or other system services
                    lastException = ex;
                    
                    // Exponential backoff: baseDelay, baseDelay*2, baseDelay*4, etc.
                    var delay = _defaultBaseDelayMs * (1 << attempt);
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
                $"Failed to load assembly in {mode} mode after {maxRetries} attempts: {assemblyPath}. " +
                $"This may be due to Windows antivirus software, file indexing, or other system processes locking the file. " +
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