using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mono.Cecil;

namespace PreludeLib.CompileTime;

public class CompileTimePatchClassProcessor
{
    private readonly CompileTimePrelude _instance;
    
    private readonly TypeDefinition _containerType;
    private readonly CompileTimePreludeMethod _containerAttributes;
    private readonly List<CompileTimeAttributePatch> _patchMethods;

    public string? Category { get; set; }

    private static readonly List<string> auxilaryMethodNames =
    [
        "Prepare",
        "Cleanup",
        "TargetMethod",
        "TargetMethods"
    ];
    
    public CompileTimePatchClassProcessor(CompileTimePrelude instance, TypeReference typeRef)
        : this(instance, typeRef.Resolve())
    {
        // empty
    }
    
    public CompileTimePatchClassProcessor(CompileTimePrelude instance, TypeDefinition typeDef)
    {
        if (_instance is null)
            throw new ArgumentNullException(nameof(instance));
        if (typeDef is null)
            throw new ArgumentNullException(nameof(typeDef));

        _instance = instance;
        _containerType = typeDef;

        var attributesOnType = CompileTimePreludeMethodExtensions.GetFromTypeDef(typeDef);
        
        _containerAttributes = CompileTimePreludeMethod.Merge(attributesOnType);
        _containerAttributes.MethodType ??= MethodType.Normal;

        Category = _containerAttributes.Category;

        foreach (var auxMethodName in auxilaryMethodNames)
        {
            if (typeDef.Methods.Any(m => m.Name == auxMethodName))
                throw new ArgumentException($"Auxiliary methods are not supported in compile-time patching: {auxMethodName} in {typeDef.FullName}");
        }

        _patchMethods = CompileTimePatchTools.GetPatchMethods(typeDef.Module, _containerType);
        foreach (var patchMethod in _patchMethods)
        {
            var method = patchMethod.Info?.Method;
            patchMethod.Info = _containerAttributes.Merge(patchMethod.Info);
            patchMethod.Info.Method = method;
        }
    }
}