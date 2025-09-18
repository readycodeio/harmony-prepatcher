using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Finalizer;

public class WeaverFinalizerTests(ITestOutputHelper output) : FinalizerTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}