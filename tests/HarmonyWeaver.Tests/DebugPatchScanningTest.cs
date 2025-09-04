using HarmonyWeaver.Core.Implementation;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace HarmonyWeaver.Tests
{
    /// <summary>
    /// Debug test to understand what's happening with patch scanning
    /// </summary>
    [Collection("IntegrationTests")]
    public class DebugPatchScanningTest
    {
        private readonly ITestOutputHelper _output;

        public DebugPatchScanningTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void DebugPatchScanning_ShouldShowWhatAttributesAreFound()
        {
            // Arrange
            var cecilAssemblyLoader = new FlexibleCecilAssemblyLoader();
            var patchScanner = new PatchScanner();
            
            var patchesAssemblyPath = GetAssemblyPath("HarmonyWeaver.Tests.Patches.dll");
            
            if (!File.Exists(patchesAssemblyPath))
            {
                _output.WriteLine($"Patches assembly not found at: {patchesAssemblyPath}");
                return;
            }

            _output.WriteLine($"Loading patches from: {patchesAssemblyPath}");

            // Act
            var patchAssembly = cecilAssemblyLoader.LoadAssemblyForScanning(patchesAssemblyPath, maxRetries: 10);
            _output.WriteLine($"Loaded assembly: {patchAssembly.FullName}");

            // Debug: Look at the types and their attributes
            foreach (var type in patchAssembly.MainModule.Types)
            {
                _output.WriteLine($"Type: {type.FullName}");
                
                foreach (var attr in type.CustomAttributes)
                {
                    _output.WriteLine($"  Type Attribute: {attr.AttributeType.FullName}");
                    if (attr.HasConstructorArguments)
                    {
                        for (int i = 0; i < attr.ConstructorArguments.Count; i++)
                        {
                            var arg = attr.ConstructorArguments[i];
                            _output.WriteLine($"    Constructor Arg {i}: {arg.Type.FullName} = {arg.Value}");
                        }
                    }
                }

                foreach (var method in type.Methods)
                {
                    if (method.HasCustomAttributes)
                    {
                        _output.WriteLine($"  Method: {method.Name}");
                        foreach (var attr in method.CustomAttributes)
                        {
                            _output.WriteLine($"    Method Attribute: {attr.AttributeType.FullName}");
                        }
                    }
                }
            }

            // Try to scan for patches
            var patches = patchScanner.ScanForPatches(patchAssembly).ToList();
            _output.WriteLine($"Found {patches.Count} patches");

            foreach (var patch in patches)
            {
                _output.WriteLine($"Patch: Target={patch.PatchAttribute.TargetTypeName}, Method={patch.PatchAttribute.MethodName}");
                _output.WriteLine($"  Has Prefix: {patch.Prefix != null}");
                _output.WriteLine($"  Has Postfix: {patch.Postfix != null}");
                _output.WriteLine($"  Has Finalizer: {patch.Finalizer != null}");
                
                // Debug prefix method parameters
                if (patch.Prefix != null)
                {
                    _output.WriteLine($"  Prefix Method: {patch.Prefix.Method.Name}");
                    _output.WriteLine($"  Prefix Return Type: {patch.Prefix.Method.ReturnType.Name}");
                    foreach (var param in patch.Prefix.Method.Parameters)
                    {
                        _output.WriteLine($"    Param: {param.Name} ({param.ParameterType.FullName}) IsByRef: {param.ParameterType.IsByReference}");
                    }
                }
            }

            // Test specific patches we're interested in
            var multiplyPatch = patches.FirstOrDefault(p => p.PatchAttribute.MethodName == "Multiply");
            if (multiplyPatch != null)
            {
                _output.WriteLine($"Found Multiply patch with prefix: {multiplyPatch.Prefix != null}");
                if (multiplyPatch.Prefix != null)
                {
                    _output.WriteLine($"Multiply prefix method name: {multiplyPatch.Prefix.Method.Name}");
                    _output.WriteLine($"Multiply prefix returns bool: {multiplyPatch.Prefix.Method.ReturnType.Name == "Boolean"}");
                }
            }

            // Don't fail the test, just output debug info
            Assert.True(true);
        }

        private string GetAssemblyPath(string assemblyFileName)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var possiblePaths = new[]
            {
                Path.Combine(baseDir, assemblyFileName),
                Path.Combine(baseDir, "..", "..", "..", "..", "tests", "HarmonyWeaver.Tests.Patches", "bin", "Debug", "net8.0", assemblyFileName)
            };

            foreach (var path in possiblePaths)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return assemblyFileName;
        }
    }
}