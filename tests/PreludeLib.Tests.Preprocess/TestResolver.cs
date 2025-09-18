using Mono.Cecil;

namespace PreludeLib.Tests.Preprocess;

public class TestResolver : DefaultAssemblyResolver
{
    public TestResolver(string basePath)
    {
        AddSearchDirectory(basePath);
        AddSearchDirectory(typeof(ITestPreprocessor).Assembly.Location);
    }

    public void AddAssembly(AssemblyDefinition asm)
    {
        RegisterAssembly(asm);
    }
}