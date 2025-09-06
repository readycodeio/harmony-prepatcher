using System;
using System.IO;
using Mono.Cecil;

namespace PreludeLib.Utils;

public class SimpleCompileTimeAssemblyLoader : ICompileTimeAssemblyLoader
{
    public AssemblyDefinition LoadAssemblyFrom(string assemblyPath, ReaderParameters readerParameters)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentNullException(nameof(assemblyPath));

        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Assembly file not found: {assemblyPath}");

        return AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);
    }
}