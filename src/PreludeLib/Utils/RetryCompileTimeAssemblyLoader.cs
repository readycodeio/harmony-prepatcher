extern alias OfficialCecil;
using OfficialCecil::Mono.Cecil;

namespace PreludeLib.Utils;

public class RetryCompileTimeAssemblyLoader : ICompileTimeAssemblyLoader
{
    private readonly int _baseDelayMs;
    private readonly int _maxAttempts;

    public RetryCompileTimeAssemblyLoader(int baseDelayMs = 25, int maxAttempts = 5)
    {
        if (baseDelayMs < 0)
            throw new ArgumentException("Base delay must be non-negative", nameof(baseDelayMs));

        _baseDelayMs = baseDelayMs;
        _maxAttempts = maxAttempts;
    }

    public AssemblyDefinition LoadAssemblyFrom(string assemblyPath, ReaderParameters readerParameters)
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
                return AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);
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
            $"This may be due to Windows antivirus software, file indexing, or other system processes locking the file. " +
            $"Last error: {lastException?.Message}", 
            lastException);
    }
}
