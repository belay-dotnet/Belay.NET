using System;
using Belay.Core.Security;

public class DebugSanitize {
    public static void Main() {
        var input = "test'string\"with\r\n\t\\special\x00chars";
        Console.WriteLine($"Input: [{input}]");
        Console.WriteLine($"Input length: {input.Length}");
        
        var result = InputValidator.SanitizePythonString(input);
        Console.WriteLine($"Result: [{result}]");
        Console.WriteLine($"Result length: {result.Length}");
        
        // Check each character
        for (int i = 0; i < Math.Min(input.Length, 50); i++) {
            Console.WriteLine($"Input[{i}]: '{input[i]}' (0x{(int)input[i]:X2})");
        }
        
        for (int i = 0; i < Math.Min(result.Length, 50); i++) {
            Console.WriteLine($"Result[{i}]: '{result[i]}' (0x{(int)result[i]:X2})");
        }
    }
}