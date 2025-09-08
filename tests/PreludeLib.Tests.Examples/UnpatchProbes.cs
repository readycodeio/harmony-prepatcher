using Microsoft.Extensions.Logging;

namespace PreludeLib.Tests.Examples;

public class UnpatchProbes
{
    public static readonly List<string> Steps = new();
    public static void Reset() => Steps.Clear();
}