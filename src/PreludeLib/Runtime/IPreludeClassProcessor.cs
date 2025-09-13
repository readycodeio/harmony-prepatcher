namespace PreludeLib.Runtime;

public interface IPreludeClassProcessor
{
    string? Category { get; }
    
    void Patch();
}