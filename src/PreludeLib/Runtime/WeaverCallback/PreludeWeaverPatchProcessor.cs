using System.Reflection;
using HarmonyLib;
using PreludeLib.Runtime.Utils;

namespace PreludeLib.Runtime.WeaverCallback;

public class PreludeWeaverPatchProcessor : IPreludePatchProcessor
{
    private readonly PreludeWeaverBackend _instance;
    private readonly MethodBase _original;

    private HarmonyMethod? Prefix;
    private HarmonyMethod? Postfix;
    private HarmonyMethod? Finalizer;
    
    public PreludeWeaverPatchProcessor(PreludeWeaverBackend instance, MethodBase original)
    {
        _instance = instance;
        _original = original;
    }
    
    public MethodInfo Patch()
    {
        if (_original is null)
            throw new NullReferenceException($"Null method for {_instance.Id}");

        if (_original.IsDeclaredMember() is false)
        {
            var declaredMember = _original.GetDeclaredMember();
            throw new ArgumentException($"You can only patch implemented methods/constructors. Patch the declared method {declaredMember.FullDescription()} instead.");
        }

        // lock (locker)
        {
            if (Prefix != null)
                _instance.DoPatch(_original, Prefix);
            if (Postfix != null)
                _instance.DoPatch(_original, Postfix);
            if (Finalizer != null)
                _instance.DoPatch(_original, Finalizer);

            return MethodUtils.WrapMethod(_original);
        }
    }

    public IPreludePatchProcessor AddPrefix(HarmonyMethod? prefix)
    {
        Prefix = prefix;
        return this;
    }

    public IPreludePatchProcessor AddPostfix(HarmonyMethod? postfix)
    {
        Postfix = postfix;
        return this;
    }

    public IPreludePatchProcessor AddTranspiler(HarmonyMethod? transpiler)
    {
        if (transpiler != null)
            throw new NotSupportedException("Weaver backend does not support transpilers");
        return this;
    }

    public IPreludePatchProcessor AddFinalizer(HarmonyMethod? finalizer)
    {
        Finalizer = finalizer;
        return this;
    }
}