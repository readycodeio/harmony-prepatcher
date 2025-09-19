using Mono.Cecil;
using PreludeLib.CompileTime.Utils;

namespace PreludeLib.CompileTime.Registry;

public readonly struct CompileTimePatchTarget : IEquatable<CompileTimePatchTarget>
{
    public readonly CompileTimePatchGroup Group;
    public readonly MethodDefinition? OriginalMethodDef;
    public readonly MethodDefinition? TargetMethodDef;
    public readonly TypeDefinition? OriginalMethodsDeclaringTypeDef;

    public bool IsFromOriginal
        => OriginalMethodDef != null;
    
    public bool IsFromTargetMethod
        => TargetMethodDef != null;

    private CompileTimePatchTarget(CompileTimePatchGroup group, MethodDefinition? original, MethodDefinition? targetMethodDef, TypeDefinition? originalMethodsDeclaringTypeDef)
    {
        Group = group;
        OriginalMethodDef = original;
        TargetMethodDef = targetMethodDef;
        OriginalMethodsDeclaringTypeDef = originalMethodsDeclaringTypeDef;
    }
    
    public static CompileTimePatchTarget FromOriginal(MethodDefinition original, CompileTimePatchGroup group)
        => new(group, original, null, null);
    
    public static CompileTimePatchTarget FromTargetMethod(MethodDefinition targetMethod, TypeDefinition declaringTypeDef, CompileTimePatchGroup group)
        => new(group, null, targetMethod, declaringTypeDef);

    public string FullDescription()
    {
        if (OriginalMethodDef != null)
            return OriginalMethodDef.FullDescription();
        if (TargetMethodDef != null)
            return TargetMethodDef.FullDescription();
        return "Empty patch target";
    }

    public override string ToString()
    {
        if (OriginalMethodDef != null)
            return $"{nameof(CompileTimePatchTarget)}.{nameof(FromOriginal)}({OriginalMethodDef.FullName})";
        else if (TargetMethodDef != null)
            return $"{nameof(CompileTimePatchTarget)}.{nameof(FromTargetMethod)}({TargetMethodDef.FullName})";
        else
            return $"{nameof(CompileTimePatchTarget)}.Empty";
    }

    public bool Equals(CompileTimePatchTarget other)
        => Group.Equals(other.Group) && 
           Equals(OriginalMethodDef, other.OriginalMethodDef) &&
           Equals(TargetMethodDef, other.TargetMethodDef) && 
           Equals(OriginalMethodsDeclaringTypeDef, other.OriginalMethodsDeclaringTypeDef);

    public override bool Equals(object? obj)
        => obj is CompileTimePatchTarget other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Group.GetHashCode();
            hashCode = (hashCode * 397) ^ (OriginalMethodDef != null ? OriginalMethodDef.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (TargetMethodDef != null ? TargetMethodDef.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (OriginalMethodsDeclaringTypeDef != null ? OriginalMethodsDeclaringTypeDef.GetHashCode() : 0);
            return hashCode;
        }
    }
}