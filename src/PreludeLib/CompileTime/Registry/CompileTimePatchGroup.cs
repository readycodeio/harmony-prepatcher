extern alias OfficialCecil;
using OfficialCecil::Mono.Cecil;
using PreludeLib.CompileTime.Utils;

namespace PreludeLib.CompileTime.Registry;

public readonly struct CompileTimePatchGroup(TypeDefinition containerTypeDef) : IEquatable<CompileTimePatchGroup>
{
    public readonly TypeDefinition? ContainerTypeDef = containerTypeDef;

    public bool Equals(CompileTimePatchGroup other)
        => ContainerTypeDef == other.ContainerTypeDef;

    public override bool Equals(object? obj)
        => obj is CompileTimePatchGroup other && Equals(other);

    public override int GetHashCode()
        => (ContainerTypeDef != null ? ContainerTypeDef.GetHashCode() : 0);

    public string FullDescription()
        => ContainerTypeDef != null ? ContainerTypeDef.FullDescription() : "Default";

    public override string ToString()
        => $"{nameof(CompileTimePatchGroup)}[{containerTypeDef.FullName}]";
}