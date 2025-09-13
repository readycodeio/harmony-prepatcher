using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime;

public interface IPreludePatchProcessor
{
    void AddPrefix(HarmonyMethod? prefix);
    void AddPostfix(HarmonyMethod? postfix);
    void AddTranspiler(HarmonyMethod? transpiler);
    void AddFinalizer(HarmonyMethod? finalizer);
    
    void Patch();
}