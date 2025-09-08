namespace PreludeLib.Tests.Patches.Simple;

public static class TestLoggerProvider
{
    public static TestLogger Logger { get; } = new TestLogger();
}