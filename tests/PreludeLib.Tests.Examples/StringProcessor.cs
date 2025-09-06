namespace PreludeLib.Tests.Examples;

public class StringProcessor
{
    public string ProcessString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return input.Trim().ToUpperInvariant();
    }

    public string ConcatenateStrings(string first, string second)
    {
        return (first ?? "") + (second ?? "");
    }

    public bool ContainsSubstring(string? text, string? substring)
    {
        if (text == null || substring == null)
            return false;

        return text.Contains(substring, StringComparison.OrdinalIgnoreCase);
    }
}
