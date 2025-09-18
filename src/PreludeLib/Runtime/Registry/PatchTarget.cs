using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.Registry;

public readonly struct PatchTarget : IEquatable<PatchTarget>
{
    public readonly MethodBase? OriginalMethod;
    public readonly MethodInfo? TargetMethod;

    private PatchTarget(MethodBase? original, MethodInfo? target)
    {
        OriginalMethod = original;
        TargetMethod = target;
    }
    
    public static PatchTarget FromOriginal(MethodBase original)
        => new(original, null);
    
    public static PatchTarget FromTargetMethod(MethodInfo target)
        => new(null, target);

    public bool Equals(PatchTarget other)
        => Equals(OriginalMethod, other.OriginalMethod) && Equals(TargetMethod, other.TargetMethod);

    public override bool Equals(object? obj)
        => obj is PatchTarget other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return ((OriginalMethod != null ? OriginalMethod.GetHashCode() : 0) * 397) ^ (TargetMethod != null ? TargetMethod.GetHashCode() : 0);
        }
    }

    public string FullDescription()
    {
        if (OriginalMethod != null)
            return OriginalMethod.FullDescription();
        if (TargetMethod != null)
            return TargetMethod.FullDescription();
        return "Empty patch target";
    }
}