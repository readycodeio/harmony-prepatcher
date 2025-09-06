using HarmonyLib;
using Mono.Cecil;

namespace PreludeLib;

public class Prelude
{
    public string Id { get; }
    
    public Prelude(string id)
    {
        Id = id;
    }

    public CompileTimePatchProcessor CreateProcessor(MethodReference original) => new(this, original);
    public CompileTimePatchProcessor CreateProcessor(MethodDefinition original) => new(this, original);
    
    public CompileTimePatchClassProcessor CreateClassProcessor(TypeReference type) => new(this, type);
    public CompileTimePatchClassProcessor CreateClassProcessor(TypeDefinition type) => new(this, type);
}
