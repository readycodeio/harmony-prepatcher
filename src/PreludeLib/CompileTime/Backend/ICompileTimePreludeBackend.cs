using PreludeLib.CompileTime.Registry;

namespace PreludeLib.CompileTime.Backend;

public interface ICompileTimePreludeBackend
{
    void Commit(ICompileTimePatchRegistry registry);
}