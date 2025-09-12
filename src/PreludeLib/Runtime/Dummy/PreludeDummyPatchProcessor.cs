using System.Reflection;
using HarmonyLib;
using PreludeLib.Runtime.Utils;

namespace PreludeLib.Runtime.DummyBackend;

public class PreludeDummyPatchProcessor : IPreludePatchProcessor
{
    private readonly MethodBase _original;
    
    public PreludeDummyPatchProcessor(PreludeDummyBackend instance, MethodBase original)
    {
        _original = original;
    }
    
    public IPreludePatchProcessor AddPrefix(HarmonyMethod? prefix)
        => this;

    public IPreludePatchProcessor AddPostfix(HarmonyMethod? postfix)
        => this;

    public IPreludePatchProcessor AddTranspiler(HarmonyMethod? transpiler)
        => this;

    public IPreludePatchProcessor AddFinalizer(HarmonyMethod? finalizer)
        => this;

    public MethodInfo Patch()
        => MethodUtils.WrapMethod(_original);

    public void Unpatch(HarmonyPatchType patchType)
    {
        // no-op
    }
}