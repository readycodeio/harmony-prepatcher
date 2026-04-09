using Microsoft.Extensions.Logging;
using Mono.Cecil;
using PreludeLib.CompileTime.Backend.WeaverCallback;
using PreludeLib.CompileTime.Public;
using PreludeLib.Tests.Utils;
using Xunit.Abstractions;

namespace PreludeLib.Tests.Preprocess;

public class WeaverPreprocessor(ILogger logger) : ITestPreprocessor
{
    public WeaverPreprocessor(ITestOutputHelper output)
        : this(new XUnitLogger(output, nameof(WeaverPreprocessor)))
    {
        // empty
    }
    
    public void Preprocess(string targetName, string patchName, string basePath)
    {
        var backend = new CompileTimeWeaverBackend(logger);
        var compileTime = new CompileTimePrelude(backend, logger);
        
        var resolver = new TestResolver(basePath);

        var version = GetType().Assembly.GetName().Version;
        
        var targetFileNameBase = Path.Combine(basePath, targetName);
        var targetFileName = targetFileNameBase + ".dll";
        var targetSymbolsFileName = Path.ChangeExtension(targetFileNameBase, "pdb");
        var hasSymbols = File.Exists(targetSymbolsFileName);

        using var targetAsmDef = AssemblyDefinition.ReadAssembly(
            targetFileName,
            new ReaderParameters()
            {
                ReadWrite = true,
                ReadSymbols = hasSymbols,
                AssemblyResolver = resolver,
            }
        );
        
        resolver.AddAssembly(targetAsmDef);
        using var patchAsmDef = resolver.Resolve(new AssemblyNameReference(patchName, version));
        compileTime.ScanAndPatchAll(patchAsmDef);
        compileTime.Commit();
        
        logger.LogInformation("Writing modified assembly to {Path}", targetFileName);
        
        targetAsmDef.Write(new WriterParameters()
        {
            WriteSymbols = targetAsmDef.MainModule.HasSymbols,
        });
    }
}