using PreludeLib.Tests.Preprocess;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Postfix;

public class WeaverPostfixTests(ITestOutputHelper output) : PostfixTestsBase(output, new WeaverPreprocessor(output))
{
    // empty
}