namespace PreludeLib.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class HarmonyTargetMethodHint : Attribute
{
    public HarmonyTargetMethodHint(string methodName, params Type[] args)
    {
        // empty
    }
    
    public HarmonyTargetMethodHint(Type declaringType, string methodName, params Type[] args)
    {
        // empty
    }
}
