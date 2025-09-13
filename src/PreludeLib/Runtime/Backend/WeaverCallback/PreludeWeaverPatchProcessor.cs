using System.Reflection;
using HarmonyLib;

namespace PreludeLib.Runtime.Backend.WeaverCallback;

public class PreludeWeaverPatchProcessor : IPreludePatchProcessor
{
    private readonly PreludeWeaverBackend _instance;
    private readonly MethodBase _original;

    private HarmonyMethod? _prefix;
    private HarmonyMethod? _postfix;
    private HarmonyMethod? _finalizer;
    
    public PreludeWeaverPatchProcessor(PreludeWeaverBackend instance, MethodBase original)
    {
        _instance = instance;
        _original = original;
    }
    
    public void Patch()
    {
        if (_original is null)
            throw new NullReferenceException($"Null method for {_instance.Id}");

        if (!_original.IsDeclaredMember())
        {
            var declaredMember = _original.GetDeclaredMember();
            throw new ArgumentException($"You can only patch implemented methods/constructors. Patch the declared method {declaredMember.FullDescription()} instead.");
        }

        // lock (locker)
        {
            if (_prefix != null)
                _instance.DoPatch(_original, _prefix);
            if (_postfix != null)
                _instance.DoPatch(_original, _postfix);
            if (_finalizer != null)
                _instance.DoPatch(_original, _finalizer);
        }
    }

    public void AddPrefix(HarmonyMethod? prefix)
    {
        _prefix = prefix;
    }

    public void AddPostfix(HarmonyMethod? postfix)
    {
        _postfix = postfix;
    }

    public void AddTranspiler(HarmonyMethod? transpiler)
    {
        if (transpiler != null)
            throw new NotSupportedException("Weaver backend does not support transpilers");
    }

    public void AddFinalizer(HarmonyMethod? finalizer)
    {
        _finalizer = finalizer;
    }
}