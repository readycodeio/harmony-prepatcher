using System;
using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime;

public interface IPreludeBackend
{
    IPreludeClassProcessor CreateClassProcessor(Type type);

    void PatchAll(Assembly patchAssembly);
    void PatchCategory(Assembly patchAssembly, string category);
    void PatchAllUncategorized(Assembly patchAssembly);
    void Patch(
        MethodInfo original,
        PreludeMethod? prefix = null,
        PreludeMethod? postfix = null,
        PreludeMethod? finalizer = null,
        PreludeMethod? transpiler = null
    );
    
    public void UnpatchAll();
    void UnpatchAll(Assembly patchAssembly);
    void UnpatchCategory(Assembly patchAssembly, string category);
    void UnpatchUncategorized(Assembly patchAssembly);
    void UnpatchCategory(string category);
    void UnpatchUncategorized();
    void Unpatch(MethodBase original, HarmonyPatchType patchType);
    void Unpatch(MethodBase original, MethodInfo patch);
}
