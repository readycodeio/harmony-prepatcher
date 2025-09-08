using Xunit.Abstractions;

namespace PreludeLib.Tests.Finalizer;

public class DummyFinalizerTests(ITestOutputHelper output) : FinalizerTestsBase(output)
{
    // empty
}