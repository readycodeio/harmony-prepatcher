using System.Reflection;
using HarmonyLib;
using PreludeLib.Attributes;

namespace PreludeLib.Tests.Patches.Categories;

[HarmonyPatchCategory("noHarmonyPatch")]
public static class PatchWithoutHarmonyPatch
{
    [HarmonyTargetMethodHint("PreludeLib.Tests.Examples.OtherCategoryTargets", "OtherMethod")]
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method("PreludeLib.Tests.Examples.OtherCategoryTargets:OtherMethod");
    }

    public static void Prefix(ref int __result, int x)
    {
        __result = x * x;
    }
    
    public static Exception? Finalizer(Exception? __exception)
    {
        return null;
    }
}