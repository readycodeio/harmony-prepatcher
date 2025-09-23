namespace PreludeLib.Common;

public readonly struct Category(string? name) : IEquatable<Category>
{
    public readonly string? Name = name;

    public bool Equals(Category other)
        => Name == other.Name;

    public override bool Equals(object? obj)
        => obj is Category other && Equals(other);

    public override int GetHashCode()
        => (Name != null ? Name.GetHashCode() : 0);

    public override string ToString()
        => Name ?? "<Uncategorized>";
    
    public static bool operator ==(Category left, Category right)
        => left.Name == right.Name;
    
    public static bool operator !=(Category left, Category right)
        => left.Name != right.Name;

    public static Category Uncategorized => default;
}