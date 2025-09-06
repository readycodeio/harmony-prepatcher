using System.Reflection;

namespace PreludeLib.Tests;

public abstract class IsolatedTestsBase
{
    protected void RunInsideIsolatedContext(string methodName)
    {
        var payloadPath = Path.GetDirectoryName(GetType().Assembly.Location)!;
        
        var alc = new IsolatedAssemblyLoadContext(payloadPath);
        var weakRef = new WeakReference(alc);

        try
        {
            var asm = alc.LoadFromAssemblyPath(GetType().Assembly.Location);
            var type = asm.GetType(GetType().FullName!, throwOnError: true)!;
            var typeInst = Activator.CreateInstance(type);
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method);

            // Invoke the test method INSIDE the ALC. It will throw if it fails.
            method.Invoke(typeInst, null);
        }
        finally
        {
            // Prepare for unload
            alc.Unload();
            alc = null!;

            // Force unload + verify collectible context actually unloaded
            for (var i = 0; weakRef.IsAlive && i < 10; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            Assert.False(weakRef.IsAlive, "ALC failed to unload; a reference is being held.");
        }
    }
}