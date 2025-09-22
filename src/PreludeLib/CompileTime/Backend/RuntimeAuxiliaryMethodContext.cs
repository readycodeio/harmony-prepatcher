extern alias OfficialCecil;
using Microsoft.Extensions.Logging;
using OfficialCecil::Mono.Cecil;

namespace PreludeLib.CompileTime.Backend;

internal struct CompileTimeAuxiliaryMethodContext(TypeDefinition? containerType, MethodDefinition? originalDef, MethodDefinition? patchMethodDef, ILogger logger)
{
    public readonly TypeDefinition? ContainerType = containerType;
    public readonly MethodDefinition? OriginalDef = originalDef;
    public MethodDefinition? PatchMethodDef = patchMethodDef;

    public readonly ILogger Logger = logger;
}