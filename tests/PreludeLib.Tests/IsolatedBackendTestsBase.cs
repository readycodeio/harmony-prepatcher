using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace PreludeLib.Tests; 

public abstract class IsolatedBackendTestsBase(ITestOutputHelper output)
{
    private void RunTestInner(Action testFunc, bool shouldPass)
    {
        if (shouldPass)
        {
            testFunc();
        }
        else
        {
            Assert.ThrowsAny<XunitException>(testFunc);
        }
    }
    
    private void RunTestIsolatedInner(ILogger logger, out WeakReference weakRef, string methodName, bool isBaseline)
    {
        var basePayloadPath = ResolvePayloadPath();
        
        var tempPath = Path.GetTempPath();
        var tempFolderName = Path.GetRandomFileName();
        var tempTestPath = Path.Combine(tempPath, tempFolderName);
        
        Directory.CreateDirectory(tempTestPath);
        
        var payloadPath = Path.Combine(tempTestPath, Path.GetFileName(basePayloadPath));
        File.Copy(basePayloadPath, payloadPath);
 
        var basePatchesPath = basePayloadPath.Replace("PreludeLib.Tests.Payload.dll", "PreludeLib.Tests.Patches.dll");
        var patchesPath = Path.Combine(tempTestPath, Path.GetFileName(basePatchesPath));
        File.Copy(basePatchesPath, patchesPath, true);

        var baseExamplesPath = basePayloadPath.Replace("PreludeLib.Tests.Payload.dll", "PreludeLib.Tests.Examples.dll");
        var examplesPath = Path.Combine(tempTestPath, Path.GetFileName(baseExamplesPath));
        File.Copy(baseExamplesPath, examplesPath, true);
        
        var destPath = examplesPath.Replace("PreludeLib.Tests.Examples.dll", "PreludeLib.Tests.Examples_patched.dll");
        
        var alc = new IsolatedAssemblyLoadContext(payloadPath, basePayloadPath);
        weakRef = new WeakReference(alc);

        try
        {
            var asm = alc.LoadFromAssemblyPath(payloadPath);
            var type = asm.GetType(GetType().FullName!.Replace("Tests", "Payload"), throwOnError: true)!;
            var typeInst = Activator.CreateInstance(type, logger);

            var t = type;
            MethodInfo? testMethod = null;
            while (testMethod == null && t != null)
            {
                testMethod = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                t = t.BaseType;
            }
            Assert.NotNull(testMethod);

            t = type;
            MethodInfo? preprocessMethod = null;
            while (preprocessMethod == null && t != null)
            {
                preprocessMethod = t.GetMethod("Preprocess", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                t = t.BaseType;
            }
            
            var shouldPassProp = type.GetProperty("ShouldPass", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var shouldPass = isBaseline || (shouldPassProp != null && (bool)shouldPassProp.GetValue(typeInst!)!);

            AlcAssert.AssertTypeInDefaultALC(typeof(Harmony));

            AlcAssert.AssertTypeInALC(type, alc);
            
            if (preprocessMethod != null)
            {
                RunTestInner(() =>
                {
                    try
                    {
                        preprocessMethod.Invoke(typeInst, ["PreludeLib.Tests.Examples", "PreludeLib.Tests.Patches", tempTestPath, destPath]);
                    }
                    catch (TargetInvocationException ex)
                    {
                        ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
                    }
                }, shouldPass);
            }
            
            var examplesAsm = alc.LoadFromAssemblyPath(examplesPath);
            AlcAssert.AssertAssemblyInALC(examplesAsm, alc);

            var patchesAsm = alc.LoadFromAssemblyPath(patchesPath);
            AlcAssert.AssertAssemblyInALC(patchesAsm, alc);

            RunTestInner(() =>
            {
                try
                {
                    testMethod.Invoke(typeInst, null);
                }
                catch (TargetInvocationException ex)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
                }
            }, shouldPass);
        }
        finally
        {
            alc.Unload();
        }
    }

    private static string ResolvePayloadPath()
    {
        // Assumes the payload dll is copied next to the test assembly.
        var baseDir = AppContext.BaseDirectory.Replace("Tests\\bin", "Tests.Payload\\bin");
        var path = Path.Combine(baseDir, "PreludeLib.Tests.Payload.dll");
        Assert.True(File.Exists(path), $"Payload not found at {path}");
        return path;
    }

    protected void RunTestIsolated(string methodName, bool isBaseline = false)
    {
        var logger = new XUnitLogger(output, methodName);
        WeakReference? weakRef = null;
        
        try
        {
            logger.LogDebug("Starting isolated test {MethodName}", methodName);
            // NOTE: This is necessary, because local variables don't get collected in Debug mode until a method exists.
            RunTestIsolatedInner(logger, out weakRef, methodName, isBaseline);
            logger.LogDebug("Ended isolated test {MethodName}", methodName);
        }
        finally
        {
            for (var i = 0; i < 10 && weakRef?.IsAlive == true; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            if (weakRef?.IsAlive == true)
            {
                logger.LogWarning("ALC still alive after unload!");
            }
        }
    }
}