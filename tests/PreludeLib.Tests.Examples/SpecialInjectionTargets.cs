namespace PreludeLib.Tests.Examples;

public class SpecialInjectionTargets
{
    private int _offset;

    public SpecialInjectionTargets(int offset = 0) => _offset = offset;

    public void SetOffset(int value) => _offset = value;
    public int GetOffset() => _offset;

    // 26) instance method so we can use __instance
    public int SumWithOffset(int a) => a + _offset;

    // 27) simple two-arg method to mutate via __args
    public int Add(int a, int b) => a + b;

    // 28 & 29) method to observe __originalMethod and exercise HarmonyArgument binding
    public int Combine(int left, int right) => left * 100 + right;
}