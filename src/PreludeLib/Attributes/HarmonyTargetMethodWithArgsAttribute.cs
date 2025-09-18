namespace PreludeLib.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class HarmonyTargetMethodHint : Attribute
{
    public HarmonyTargetMethodHint(string methodName, Type[] args)
    {
        // empty
    }
}
