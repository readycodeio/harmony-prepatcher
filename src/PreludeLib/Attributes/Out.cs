namespace PreludeLib.Attributes;

/// <summary>
/// Use in <see cref="PreludeLib.Attributes.HarmonyTargetMethodHint"/> parameter list to indicate that the parameter is an <see langword="out"/> parameter.
/// This is required for Harmony to correctly identify the method when matching signatures, as <see langword="out"/> parameters are treated differently than regular parameters.
/// </summary>
/// <typeparam name="T">The type of the <see langword="out"/> parameter.</typeparam>
public struct Out<T>
{
    // empty
}