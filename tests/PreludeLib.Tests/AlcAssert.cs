using System.Reflection;
using System.Runtime.Loader;

namespace PreludeLib.Tests;

public static class AlcAssert
{
    /// <summary>
    /// Assert that the given Type is loaded in the Default ALC.
    /// </summary>
    public static void AssertTypeInDefaultALC(Type type)
    {
        AssemblyLoadContext? ctx = AssemblyLoadContext.GetLoadContext(type.Assembly);
        Assert.Same(AssemblyLoadContext.Default, ctx);
    }

    /// <summary>
    /// Assert that the given Assembly is loaded in the Default ALC.
    /// </summary>
    public static void AssertAssemblyInDefaultALC(Assembly assembly)
    {
        AssemblyLoadContext? ctx = AssemblyLoadContext.GetLoadContext(assembly);
        Assert.Same(AssemblyLoadContext.Default, ctx);
    }

    /// <summary>
    /// Assert that the given Type is loaded in the specified ALC instance.
    /// </summary>
    public static void AssertTypeInALC(Type type, AssemblyLoadContext expectedAlc)
    {
        AssemblyLoadContext? actual = AssemblyLoadContext.GetLoadContext(type.Assembly);
        Assert.Same(expectedAlc, actual);
    }

    /// <summary>
    /// Assert that the given Assembly is loaded in the specified ALC instance.
    /// </summary>
    public static void AssertAssemblyInALC(Assembly assembly, AssemblyLoadContext expectedAlc)
    {
        AssemblyLoadContext? actual = AssemblyLoadContext.GetLoadContext(assembly);
        Assert.Same(expectedAlc, actual);
    }

    /// <summary>
    /// Assert that the given Type is NOT loaded in the specified ALC instance.
    /// </summary>
    public static void AssertTypeNotInALC(Type type, AssemblyLoadContext notExpectedAlc)
    {
        AssemblyLoadContext? actual = AssemblyLoadContext.GetLoadContext(type.Assembly);
        Assert.NotSame(notExpectedAlc, actual);
    }

    /// <summary>
    /// Assert that two Types come from the same ALC.
    /// </summary>
    public static void AssertSameALC(Type a, Type b)
    {
        AssemblyLoadContext? ca = AssemblyLoadContext.GetLoadContext(a.Assembly);
        AssemblyLoadContext? cb = AssemblyLoadContext.GetLoadContext(b.Assembly);
        Assert.Same(ca, cb);
    }

    /// <summary>
    /// Assert that two Assemblies come from the same ALC.
    /// </summary>
    public static void AssertSameALC(Assembly a, Assembly b)
    {
        AssemblyLoadContext? ca = AssemblyLoadContext.GetLoadContext(a);
        AssemblyLoadContext? cb = AssemblyLoadContext.GetLoadContext(b);
        Assert.Same(ca, cb);
    }

    /// <summary>
    /// Assert that two Types come from different ALCs.
    /// </summary>
    public static void AssertDifferentALCs(Type a, Type b)
    {
        AssemblyLoadContext? ca = AssemblyLoadContext.GetLoadContext(a.Assembly);
        AssemblyLoadContext? cb = AssemblyLoadContext.GetLoadContext(b.Assembly);
        Assert.NotSame(ca, cb);
    }

    /// <summary>
    /// Assert that the given Assembly is collectible (i.e., can be unloaded) or not.
    /// </summary>
    public static void AssertCollectible(Assembly assembly, bool expectedCollectible)
    {
        AssemblyLoadContext? ctx = AssemblyLoadContext.GetLoadContext(assembly);
        Assert.NotNull(ctx);
        Assert.Equal(expectedCollectible, ctx!.IsCollectible);
    }

    /// <summary>
    /// Asserts there is exactly one loaded assembly instance with the given simple name
    /// across the entire AppDomain (helps catch duplicate loads across ALCs).
    /// </summary>
    public static void AssertSingleAssemblyIdentityLoaded(string simpleName)
    {
        var matches = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => string.Equals(a.GetName().Name, simpleName, StringComparison.Ordinal))
            .ToArray();

        Assert.True(matches.Length == 1,
            $"Expected exactly one loaded assembly named '{simpleName}', found {matches.Length}:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, matches.Select(a =>
            {
                AssemblyLoadContext? ctx = AssemblyLoadContext.GetLoadContext(a);
                string ctxLabel = ReferenceEquals(ctx, AssemblyLoadContext.Default) ? "Default" : ctx?.ToString() ?? "<null>";
                return $" - {a.FullName} @ {a.Location} | ALC: {ctxLabel}";
            })));
    }

    /// <summary>
    /// Convenience: dumps where a Type's assembly is loaded (for troubleshooting),
    /// then asserts it matches expectation (Default vs not Default).
    /// </summary>
    public static void AssertTypeIsInDefaultAndDump(Type type, bool expectedInDefault)
    {
        Assembly asm = type.Assembly;
        AssemblyLoadContext? ctx = AssemblyLoadContext.GetLoadContext(asm);
        string ctxLabel = ReferenceEquals(ctx, AssemblyLoadContext.Default) ? "Default" : ctx?.ToString() ?? "<null>";
        Console.WriteLine($"[ALC] Type {type.FullName} -> ALC: {ctxLabel} | Assembly: {asm.Location}");

        bool inDefault = ReferenceEquals(ctx, AssemblyLoadContext.Default);
        Assert.Equal(expectedInDefault, inDefault);
    }

    /// <summary>
    /// Convenience: dumps where an Assembly is loaded (for troubleshooting),
    /// then asserts it matches the expected ALC instance.
    /// </summary>
    public static void AssertAssemblyInALCAndDump(Assembly assembly, AssemblyLoadContext expectedAlc)
    {
        AssemblyLoadContext? ctx = AssemblyLoadContext.GetLoadContext(assembly);
        string ctxLabel = ReferenceEquals(ctx, AssemblyLoadContext.Default) ? "Default" : ctx?.ToString() ?? "<null>";
        Console.WriteLine($"[ALC] Assembly {assembly.FullName} -> ALC: {ctxLabel} | Location: {assembly.Location}");

        Assert.Same(expectedAlc, ctx);
    }
}