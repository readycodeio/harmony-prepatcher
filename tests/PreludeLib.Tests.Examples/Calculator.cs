namespace PreludeLib.Tests.Examples;

public class Calculator
{
    public int Add(int a, int b) => a + b;

    public int Subtract(int a, int b) => a - b;

    public int Multiply(int a, int b) => a * b;

    public int Divide(int a, int b)
    {
        if (b == 0)
            throw new System.DivideByZeroException("Cannot divide by zero");
        
        return a / b;
    }

    public double CalculateCircleArea(double radius) => System.Math.PI * radius * radius;
}
