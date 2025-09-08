namespace PreludeLib.Tests;

[CollectionDefinition("HarmonyNonParallel", DisableParallelization = true)]
public class HarmonyNonParallelCollection : ICollectionFixture<object> { }