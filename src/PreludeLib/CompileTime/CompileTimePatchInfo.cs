using System.Linq;
using HarmonyLib;
using Mono.Cecil;

namespace PreludeLib.CompileTime;

public class CompileTimePatchInfo
{
    public CompileTimePatch[] Prefixes = [];
    public CompileTimePatch[] Postfixes = [];
    public CompileTimePatch[] Transpilers = [];
    public CompileTimePatch[] Finalizers = [];
    public CompileTimePatch[] Innerprefixes = [];
    public CompileTimePatch[] Innerpostfixes = [];

    public CompileTimePatchInfo(ModuleDefinition module, PatchInfo patchInfo)
    {
        Prefixes = patchInfo.prefixes.Select(x => new CompileTimePatch(module, x)).ToArray();
        Postfixes = patchInfo.postfixes.Select(x => new CompileTimePatch(module, x)).ToArray();
        Transpilers = patchInfo.transpilers.Select(x => new CompileTimePatch(module, x)).ToArray();
        Finalizers = patchInfo.finalizers.Select(x => new CompileTimePatch(module, x)).ToArray();
        Innerprefixes = patchInfo.innerpostfixes.Select(x => new CompileTimePatch(module, x)).ToArray();
        Innerpostfixes = patchInfo.innerpostfixes.Select(x => new CompileTimePatch(module, x)).ToArray();
    }
}