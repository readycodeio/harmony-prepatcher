using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.Registry;

public readonly struct PatchTarget : IEquatable<PatchTarget>
{
    public readonly PatchGroup Group;
    public readonly MethodBase? OriginalMethod;
    public readonly MethodInfo? TargetMethod;

    private PatchTarget(PatchGroup group, MethodBase? original, MethodInfo? targetMethod)
    {
        Group = group;
        OriginalMethod = original;
        TargetMethod = targetMethod;
    }

    public bool IsFromOriginal
        => OriginalMethod != null;
    
    public bool IsFromTargetMethod
        => TargetMethod != null;

    public static PatchTarget FromOriginal(MethodBase original, PatchGroup group)
        => new(group, original, null);
    
    public static PatchTarget FromTargetMethod(MethodInfo targetMethod, PatchGroup group)
        => new(group, null, targetMethod);

    public string FullDescription()
    {
        if (OriginalMethod != null)
            return OriginalMethod.FullDescription();
        if (TargetMethod != null)
            return TargetMethod.FullDescription();
        return "Empty patch target";
    }

    public bool Equals(PatchTarget other)
        => Group.Equals(other.Group) &&
           Equals(OriginalMethod, other.OriginalMethod) &&
           Equals(TargetMethod, other.TargetMethod);

    public override bool Equals(object? obj)
    {
        return obj is PatchTarget other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Group.GetHashCode();
            hashCode = (hashCode * 397) ^ (OriginalMethod != null ? OriginalMethod.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (TargetMethod != null ? TargetMethod.GetHashCode() : 0);
            return hashCode;
        }
    }
}