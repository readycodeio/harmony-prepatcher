using HarmonyWeaver.Core.Implementation;
using System;
using System.IO;
using Xunit;

namespace HarmonyWeaver.Tests
{
    /// <summary>
    /// Tests for the AssemblyLoader implementation
    /// </summary>
    public class AssemblyLoaderTests : IDisposable
    {
        private readonly AssemblyLoader _assemblyLoader;

        public AssemblyLoaderTests()
        {
            _assemblyLoader = new AssemblyLoader();
        }

        [Fact]
        public void LoadAssembly_WithNullPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _assemblyLoader.LoadAssembly(null));
            Assert.Throws<ArgumentNullException>(() => _assemblyLoader.LoadAssembly(""));
            Assert.Throws<ArgumentNullException>(() => _assemblyLoader.LoadAssembly("   "));
        }

        [Fact]
        public void LoadAssembly_WithNonExistentFile_ThrowsFileNotFoundException()
        {
            var nonExistentPath = "nonexistent_file.dll";
            
            var exception = Assert.Throws<FileNotFoundException>(() => 
                _assemblyLoader.LoadAssembly(nonExistentPath));
            
            Assert.Contains("Assembly file not found", exception.Message);
        }

        [Fact]
        public void LoadAssemblies_WithNullPaths_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _assemblyLoader.LoadAssemblies(null));
        }

        // TODO: Add tests with actual assembly files once we have built assemblies to test with

        public void Dispose()
        {
            _assemblyLoader?.Dispose();
        }
    }
}