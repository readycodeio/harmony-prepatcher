namespace PreludeLib.Attributes;

/// <summary>
/// When declaring a <see href="https://harmony.pardeike.net/articles/patching-auxiliary.html#targetmethod">TargetMethod</see> method in your patch class, you must decorate it with this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class HarmonyTargetMethodHint : Attribute
{
    /// <summary>
    /// Use this constructor when the <see href="https://harmony.pardeike.net/articles/annotations.html">HarmonyPatch</see> attribute on the patch class already specifies the declaring type.
    /// </summary>
    /// <param name="methodName">Name of the target method.</param>
    /// <param name="args">Types of the target method parameters, in order. Use an empty array for parameterless methods.</param>
    public HarmonyTargetMethodHint(string methodName, params Type[] args)
    {
        // empty
    }
    
    /// <summary>
    /// The declaring type can be specified as a Type to allow for compile-time checking of the target method's existence and signature.
    /// However, this creates a hard reference to the assembly containing the target method, which may not be desirable in all cases.
    /// </summary>
    /// <param name="declaringType">Fully qualified name of the declaring type, including namespace.</param>
    /// <param name="methodName">Name of the target method.</param>
    /// <param name="args">Types of the target method parameters, in order. Use an empty array for parameterless methods.</param>
    public HarmonyTargetMethodHint(Type declaringType, string methodName, params Type[] args)
    {
        // empty
    }

    /// <summary>
    /// The declaring type can be specified as a string to avoid hard references to the assembly containing the target method.
    /// </summary>
    /// <param name="declaringType">Fully qualified name of the declaring type, including namespace.</param>
    /// <param name="methodName">Name of the target method.</param>
    /// <param name="args">Types of the target method parameters, in order. Use an empty array for parameterless methods.</param>
    public HarmonyTargetMethodHint(string declaringType, string methodName, params Type[] args)
    {
        // empty
    }
}
