using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace PreludeLib.Tests.Simple;

public abstract class SimpleTestsBase(ITestOutputHelper output) : IsolatedBackendTestsBase(output)
{
    [Fact]
    public void BaseBehavior_WithoutPatches_IsUnmodified()
        => RunTestIsolated(nameof(BaseBehavior_WithoutPatches_IsUnmodified), true);

    [Fact]
    public void Patched_Add_LogsPrefixAndPostfix_AndReturnsSameResult()
        => RunTestIsolated(nameof(Patched_Add_LogsPrefixAndPostfix_AndReturnsSameResult));

    [Fact]
    public void Patched_Divide_ByZero_IsHandledByPrefix_ReturnsZero_AndFinalizerLogsSuccess()
        => RunTestIsolated(nameof(Patched_Divide_ByZero_IsHandledByPrefix_ReturnsZero_AndFinalizerLogsSuccess));

    [Fact]
    public void FinalizerOnly_CatchesException_AndDoesNotSuppressIt()
        => RunTestIsolated(nameof(FinalizerOnly_CatchesException_AndDoesNotSuppressIt));

    [Fact]
    public void Patched_Divide_NormalOperands_UnchangedResult_AndFinalizerLogsSuccess()
        => RunTestIsolated(nameof(Patched_Divide_NormalOperands_UnchangedResult_AndFinalizerLogsSuccess));
}