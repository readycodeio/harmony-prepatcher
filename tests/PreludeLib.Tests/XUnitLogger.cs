using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace PreludeLib.Tests;

public sealed class XUnitLogger(ITestOutputHelper output, string category) : ILogger
{
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => NullScope.Instance;
    
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> formatter)
    {
        var msg = formatter?.Invoke(state, ex) ?? state?.ToString() ?? string.Empty;
        output.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss.fff}] {level,-11} {category}: {msg}");
        if (ex != null)
            output.WriteLine(ex.ToString());
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
            // empty
        }
    }
}