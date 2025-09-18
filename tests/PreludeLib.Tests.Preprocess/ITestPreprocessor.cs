namespace PreludeLib.Tests.Preprocess;

public interface ITestPreprocessor
{
    void Preprocess(string targetName, string patchName, string basePath);
}