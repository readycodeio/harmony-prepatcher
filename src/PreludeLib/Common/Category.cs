namespace PreludeLib.Common;

public readonly struct Category(string name) : IEquatable<Category>
{
    public readonly string Name = name;

    public bool Equals(Category other)
        => Name == other.Name;

    public override bool Equals(object? obj)
        => obj is Category other && Equals(other);

    public override int GetHashCode()
        => Name.GetHashCode();

    public static Category Uncategorized => default;
}