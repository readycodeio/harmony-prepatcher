using System.Reflection;

namespace PreludeLib.Runtime;

public interface IPreludeClassProcessor
{
    string? Category { get; }
    
    List<MethodInfo> Patch();
}