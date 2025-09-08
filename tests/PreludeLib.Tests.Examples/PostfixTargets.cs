namespace PreludeLib.Tests.Examples;

public class PostfixTargets
{
    // Test 5: returns a value; postfix will modify __result
    public int Double(int x) => x * 2;

    // Test 6: void method; postfix should still run
    public void NoOp() { /* intentionally empty */ }

    // Test 7: value-returning method paired with __state usage
    public int Echo(int v) => v;

    // Test 8: arguments mutated by prefix should be seen by postfix
    public int Combine(int a, int b) => a + b;
}