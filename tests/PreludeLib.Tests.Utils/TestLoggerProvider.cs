namespace PreludeLib.Tests.Utils;

public static class TestLoggerProvider
{
    public static TestLogger Logger { get; } = new TestLogger();
}