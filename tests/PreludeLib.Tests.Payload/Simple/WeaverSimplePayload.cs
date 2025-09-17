using Microsoft.Extensions.Logging;
using Mono.Cecil;
using PreludeLib.CompileTime.Backend.WeaverCallback;
using PreludeLib.CompileTime.Public;
using PreludeLib.Runtime.Backend;
using PreludeLib.Runtime.Backend.WeaverCallback;

namespace PreludeLib.Payload.Simple;

public class WeaverSimplePayload(ILogger logger) : SimplePayloadBase(true, logger)
{
    protected override IRuntimeBackend CreateBackend()
        => new RuntimeWeaverBackend(Logger);

    public void Preprocess(string targetName, string patchName, string basePath, string destPath)
    {
        var backend = new CompileTimeWeaverBackend(Logger);
        var compileTime = new CompileTimePrelude(backend, Logger);
        
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(basePath);

        var version = GetType().Assembly.GetName().Version;
        var targetAsmDef = resolver.Resolve(new AssemblyNameReference(targetName, version));
        var patchAsmDef = resolver.Resolve(new AssemblyNameReference(patchName, version));
        compileTime.ScanAndPatchAll(patchAsmDef);
        compileTime.Commit();
        
        Logger.LogInformation("Writing modified assembly to {Path}", destPath);
        
        targetAsmDef.Write(destPath, new WriterParameters()
        {
            WriteSymbols = targetAsmDef.MainModule.HasSymbols,
        });
        
        
        patchAsmDef.Dispose();
        targetAsmDef.Dispose();
    }
}