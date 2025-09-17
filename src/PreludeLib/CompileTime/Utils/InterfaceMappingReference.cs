using Mono.Cecil;

namespace PreludeLib.CompileTime.Utils;

internal class InterfaceMappingReference
{
    public TypeReference InterfaceType { get; }
    public MethodReference[] InterfaceMethods { get; }
    public TypeReference TargetType { get; }
    public MethodReference[] TargetMethods { get; }

    public InterfaceMappingReference(TypeReference interfaceType, MethodReference[] interfaceMethods,
        TypeReference targetType, MethodReference[] targetMethods)
    {
        InterfaceType = interfaceType;
        InterfaceMethods = interfaceMethods;
        TargetType = targetType;
        TargetMethods = targetMethods;
    }
    
    public InterfaceMappingDefinition Resolve()
    {
        return new InterfaceMappingDefinition(
            InterfaceType.Resolve(), 
            InterfaceMethods.Select(m => m.Resolve()).ToArray(), 
            TargetType.Resolve(),
            TargetMethods.Select(m => m.Resolve()).ToArray());
    }
}
