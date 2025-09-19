using HarmonyLib;

namespace PreludeLib.Runtime.Registry;

public readonly struct PatchGroup(Type containerType) : IEquatable<PatchGroup>
{
    public readonly Type? ContainerType = containerType;

    public bool Equals(PatchGroup other)
        => ContainerType == other.ContainerType;

    public override bool Equals(object? obj)
        => obj is PatchGroup other && Equals(other);

    public override int GetHashCode()
        => (ContainerType != null ? ContainerType.GetHashCode() : 0);

    public string FullDescription()
        => ContainerType != null ? ContainerType.FullDescription() : "Default";
}