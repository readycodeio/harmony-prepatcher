namespace PreludeLib.Tests.Examples;

public static class ForeachTargets
{
    public static int Example(out int k)
    {
        k = 0;
        var numbers = new Dictionary<int, string>
        {
            { 1, "one" },
            { 2, "two" },
            { 3, "three" }
        };
        
        Console.WriteLine("Huh");
        
        if (numbers[1] == "two")
        {
            return 18;
        }
        
        foreach (var number in numbers)
        {
            Console.WriteLine($"{number.Key}: {number.Value}");

            if (number.Key == 2)
            {
                k = 5;   
                return 7;
            }
        }
        
        foreach (var number in numbers)
        {
            Console.WriteLine($"{number.Key}: {number.Value}");
            k = 3;
        }
        
        Console.WriteLine("Huh");

        k = 3;
        return 7;
    }
}