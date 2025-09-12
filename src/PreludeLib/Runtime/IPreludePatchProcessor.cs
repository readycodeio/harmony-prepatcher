using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime;

public interface IPreludePatchProcessor
{
    IPreludePatchProcessor AddPrefix(HarmonyMethod? prefix);
    IPreludePatchProcessor AddPostfix(HarmonyMethod? postfix);
    IPreludePatchProcessor AddTranspiler(HarmonyMethod? transpiler);
    IPreludePatchProcessor AddFinalizer(HarmonyMethod? finalizer);
    
    MethodInfo Patch();
}