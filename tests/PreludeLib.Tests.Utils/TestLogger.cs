using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace PreludeLib.Tests.Utils;

public class TestLogger : ILogger
{
    private readonly object _lock = new();
    public readonly List<string> Entries = new();

    /// <summary>
    /// Minimum level to record (default Trace).
    /// </summary>
    public LogLevel MinLevel { get; set; } = LogLevel.Trace;

    public void Clear()
    {
        lock (_lock) Entries.Clear();
    }

    // ---- ILogger implementation ----
    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= MinLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        string message = formatter != null
            ? formatter(state, exception)
            : state?.ToString() ?? string.Empty;

        Append(logLevel, message, exception);
    }

    private void Append(LogLevel level, string message, Exception? ex)
    {
        var prefix = level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT",
            _ => level.ToString().ToUpperInvariant()
        };

        var line = ex == null
            ? $"[{prefix}] {message}"
            : $"[{prefix}] {message} | Exception: {ex.GetType().Name}: {ex.Message}";

        lock (_lock) Entries.Add(line);
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        private NullScope() { }
        public void Dispose() { }
    }
}