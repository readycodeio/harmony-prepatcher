using Xunit.Abstractions;

namespace PreludeLib.Tests.OverloadArg;

public class DummyOverloadArgTests(ITestOutputHelper output) : OverloadArgTestsBase(output)
{
    // empty
}