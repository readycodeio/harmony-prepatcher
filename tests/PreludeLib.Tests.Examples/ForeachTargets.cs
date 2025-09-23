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

    public static int NestedExample(int searchX, int searchY, int searchZ)
    {
        var lst = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var lst2 = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var result = false;
        
        foreach (var x in lst)
        {
            foreach (var y in lst2)
            {
                foreach (var z in lst2)
                {
                    if (x > 7 && y > 7 && z > 7)
                        throw new InvalidOperationException("abc");
                    
                    if (x == searchX && y == searchY && z == searchZ)
                    {
                        result = true;
                        goto earlyExit;
                    }
                }

                if (searchZ == 9)
                    throw new InvalidOperationException("def");
            }
        }
        earlyExit:
        if (result)
            return 333;

        return -1;
    }
}