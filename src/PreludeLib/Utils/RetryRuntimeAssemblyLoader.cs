using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace PreludeLib.Utils;

public class RetryRuntimeAssemblyLoader : IRuntimeAssemblyLoader
{
    private readonly int _baseDelayMs;
    private readonly int _maxAttempts;

    public RetryRuntimeAssemblyLoader(int baseDelayMs = 25, int maxAttempts = 5)
    {
        if (maxAttempts <= 0)
            throw new ArgumentException("Max attempts must be greater than 0", nameof(maxAttempts));
        if (baseDelayMs < 0)
            throw new ArgumentException("Base delay must be non-negative", nameof(baseDelayMs));

        _baseDelayMs = baseDelayMs;
        _maxAttempts = maxAttempts;
    }

    public Assembly LoadAssemblyFrom(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentNullException(nameof(assemblyPath));

        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");

        Exception? lastException = null;

        for (var attemptIndex = 0; attemptIndex < _maxAttempts; attemptIndex++)
        {
            try
            {
                return Assembly.LoadFrom(assemblyPath);
            }
            catch (Exception ex) when (RetryUtils.IsTransientException(ex) && attemptIndex < _maxAttempts - 1)
            {
                lastException = ex;
                
                var delay = _baseDelayMs * (1 << attemptIndex);
                Thread.Sleep(delay);
            }
        }

        throw new InvalidOperationException(
            $"Failed to load assembly after {_maxAttempts} attempts: {assemblyPath}. " +
            $"This may be due to antivirus software, file indexing, or other system processes locking the file. " +
            $"Last error: {lastException?.Message}", 
            lastException);
    }
}
