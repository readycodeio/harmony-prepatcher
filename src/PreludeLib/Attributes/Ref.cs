namespace PreludeLib.Attributes;

/// <summary>
/// Use in <see cref="PreludeLib.Attributes.HarmonyTargetMethodHint"/> parameter list to indicate that the parameter is an <see langword="ref"/> parameter.
/// This is required for Harmony to correctly identify the method when matching signatures, as <see langword="ref"/> parameters are treated differently than regular parameters.
/// </summary>
/// <typeparam name="T">The type of the <see langword="ref"/> parameter.</typeparam>
public struct Ref<T>
{
    // empty
}