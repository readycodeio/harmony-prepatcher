using HarmonyLib;
using Mono.Cecil;

namespace PreludeLib.CompileTime;

public class CompileTimePatch(ModuleDefinition module, Patch patch)
{
    public readonly int Index = patch.index;
    public readonly string Owner = patch.owner;
    public readonly int Priority = patch.priority == -1 ? HarmonyLib.Priority.Normal : patch.priority;
    public readonly string[] Before = patch.before ?? [];
    public readonly string[] After = patch.after ?? [];
    public readonly bool Debug = patch.debug;
    public MethodReference PatchMethod = module.ImportReference(patch.PatchMethod);
}