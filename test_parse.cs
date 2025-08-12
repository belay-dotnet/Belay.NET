using System;

public class Test {
    public static void Main() {
        TestCase("OKtest1\r\n\x04\x04>", "test1");
        TestCase("OK\x04\x04>", "");
        TestCase("OK4\r\n\x04\x04>", "4");
        TestCase("test without OK prefix>", "test without OK prefix");
    }
    
    static void TestCase(string input, string expected) {
        // My implementation
        string result = input;
        string output = input; // Preserve original
        
        if (result.StartsWith("OK")) {
            result = result.Substring(2);
            int firstControlCharIndex = result.IndexOf('\x04');
            if (firstControlCharIndex >= 0) {
                result = result.Substring(0, firstControlCharIndex);
            }
            else if (result.EndsWith('>')) {
                result = result.Substring(0, result.Length - 1);
            }
            result = result.TrimEnd('\r', '\n');
        }
        else if (input.EndsWith('>')) {
            result = input.Substring(0, input.Length - 1);
        }
        
        Console.WriteLine($"Input: '{input.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\x04", "\\x04")}'");
        Console.WriteLine($"Expected: '{expected}'");
        Console.WriteLine($"Got: '{result}'");
        Console.WriteLine($"Match: {result == expected}");
        Console.WriteLine();
    }
}