using Mono.Cecil;

namespace PreludeLib.CompileTime.Utils
{
    internal class InterfaceMappingDefinition
    {
        public TypeDefinition InterfaceType { get; }
        public MethodDefinition[] InterfaceMethods { get; }
        public TypeDefinition TargetType { get; }
        public MethodDefinition[] TargetMethods { get; }

        public InterfaceMappingDefinition(TypeDefinition interfaceType, MethodDefinition[] interfaceMethods,
            TypeDefinition targetType, MethodDefinition[] targetMethods)
        {
            InterfaceType = interfaceType;
            InterfaceMethods = interfaceMethods;
            TargetType = targetType;
            TargetMethods = targetMethods;
        }

        public static implicit operator InterfaceMappingReference(InterfaceMappingDefinition mapping)
        {
            return new InterfaceMappingReference(
                mapping.InterfaceType, 
                mapping.InterfaceMethods.ToArray<MethodReference>(),
                mapping.TargetType,
                mapping.TargetMethods.ToArray<MethodReference>());
        }
    }
}