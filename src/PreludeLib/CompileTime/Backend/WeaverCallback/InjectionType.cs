namespace PreludeLib.CompileTime.Backend.WeaverCallback;

public enum InjectionType
{
    Unknown,
    Instance,
    OriginalMethod,
    ArgsArray,
    Result,
    ResultRef,
    State,
    Exception,
    RunOriginal
}