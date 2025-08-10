using System;
using System.Linq;

Console.WriteLine("=== Parse Response Test ===");

// Test the parsing logic with known Pico responses
TestParseResponse("OKtest1\r\n\x04\x04>", "test1", "print('test1') response");
TestParseResponse("OK\x04\x04>", "", "2+2 response (empty)");
TestParseResponse("OK4\r\n\x04\x04>", "4", "print(2+2) response");
TestParseResponse("OKrp2\r\n\x04\x04>", "rp2", "sys.platform response");

Console.WriteLine("\n✓ All parsing tests completed");

static void TestParseResponse(string input, string expected, string testName)
{
    Console.WriteLine($"\n=== {testName} ===");
    Console.WriteLine($"Input: '{input}'");
    Console.WriteLine($"Input bytes: [{string.Join(", ", input.Select(c => ((int)c).ToString()))}]");
    Console.WriteLine($"Input hex: {string.Join(" ", input.Select(c => $"0x{((int)c):X2}"))}");
    
    string result = ParseRawReplResponse(input);
    
    Console.WriteLine($"Expected: '{expected}'");
    Console.WriteLine($"Actual: '{result}'");
    Console.WriteLine($"Match: {result == expected}");
    
    if (result != expected)
    {
        Console.WriteLine($"❌ FAIL: Expected '{expected}', got '{result}'");
    }
    else
    {
        Console.WriteLine($"✅ PASS");
    }
}

static string ParseRawReplResponse(string output)
{
    // Simulate the parsing logic from AdaptiveRawReplProtocol
    string result = output;
    
    Console.WriteLine($"  DEBUG: Input length: {result.Length}");
    
    // Remove "OK" prefix if present
    if (result.StartsWith("OK"))
    {
        result = result.Substring(2);
        Console.WriteLine($"  DEBUG: After OK removal: '{result}' (length: {result.Length})");
    }
    
    // Remove trailing control characters and prompt
    // Find the first \x04 character (start of end sequence)
    int firstControlCharIndex = result.IndexOf('\x04');
    
    Console.WriteLine($"  DEBUG: First control char index: {firstControlCharIndex}");
    
    if (firstControlCharIndex >= 0)
    {
        Console.WriteLine($"  DEBUG: Cutting at first control char index {firstControlCharIndex}");
        result = result.Substring(0, firstControlCharIndex);
        Console.WriteLine($"  DEBUG: After control char cut: '{result}' (length: {result.Length})");
    }
    else if (result.EndsWith(">"))
    {
        result = result.Substring(0, result.Length - 1);
        Console.WriteLine($"  DEBUG: After > removal: '{result}' (length: {result.Length})");
    }
    
    // Trim whitespace and control characters
    string finalResult = result.Trim('\r', '\n', ' ', '\t');
    Console.WriteLine($"  DEBUG: After final trim: '{finalResult}' (length: {finalResult.Length})");
    return finalResult;
}