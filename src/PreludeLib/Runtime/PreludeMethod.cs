 using System.Reflection;
 using HarmonyLib;

 namespace PreludeLib.Runtime;

public class PreludeMethod
{
    public readonly MethodInfo MethodInfo;

    public PreludeMethod(MethodInfo methodInfo)
    {
        MethodInfo = methodInfo;
    }

    public HarmonyMethod ToHarmonyMethod()
        => new(MethodInfo);
}