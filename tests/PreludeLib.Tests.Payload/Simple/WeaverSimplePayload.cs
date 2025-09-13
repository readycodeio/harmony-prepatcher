using Microsoft.Extensions.Logging;
using Mono.Cecil;
using PreludeLib.CompileTime.Backend;
using PreludeLib.CompileTime.Public;
using PreludeLib.Runtime;
using PreludeLib.Runtime.Backend.WeaverCallback;

namespace PreludeLib.Payload.Simple;

public class WeaverSimplePayload(ILogger logger) : SimplePayloadBase(true, logger)
{
    protected override IPreludeBackend CreateBackend(string id)
        => new PreludeWeaverBackend(id);

    public void Preprocess(string path)
    {
        var backend = new CompileTimeWeaverBackend();
        var compileTime = new CompileTimePrelude(backend);
        
        var symbolPath = Path.ChangeExtension(path, "pdb");
        var asmDef = AssemblyDefinition.ReadAssembly(path, new ReaderParameters()
        {
            ReadWrite = true,
            ReadSymbols = File.Exists(symbolPath),
        });
        compileTime.ScanAndPatchAll(asmDef);
        compileTime.Commit();
    }
}