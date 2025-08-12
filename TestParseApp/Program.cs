using System;

namespace TestParseApp
{
    class Program
    {
        static void Main(string[] args)
        {
            TestCase("OKtest1\r\n\x04\x04>", "test1");
            TestCase("OK\x04\x04>", "");
            TestCase("OK4\r\n\x04\x04>", "4");
            TestCase("OKhello world\r\n\x04\x04>", "hello world");
            TestCase("test without OK prefix>", "test without OK prefix");
            TestCase("OK>", "");
        }
        
        static void TestCase(string input, string expected)
        {
            // Test my implementation
            string result = ParseResponse(input);
            
            Console.WriteLine($"Input: '{input.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\x04", "\\x04")}'");
            Console.WriteLine($"Expected: '{expected}'");
            Console.WriteLine($"Got: '{result}'");
            Console.WriteLine($"Match: {result == expected}");
            Console.WriteLine();
        }
        
        static string ParseResponse(string output)
        {
            // Parse result from output
            string result = output;
            
            // Handle different response formats
            if (result.StartsWith("OK"))
            {
                // Raw REPL response format: "OK<content>\x04\x04>" or "OK>"
                result = result.Substring(2);
                
                // Remove trailing control characters and prompt
                int firstControlCharIndex = result.IndexOf('\x04');
                if (firstControlCharIndex >= 0)
                {
                    result = result.Substring(0, firstControlCharIndex);
                }
                else if (result.EndsWith('>'))
                {
                    result = result.Substring(0, result.Length - 1);
                }
                
                // Trim line ending characters but preserve content
                result = result.TrimEnd('\r', '\n');
            }
            else if (output.EndsWith('>'))
            {
                // Direct output without OK prefix (e.g., "test without OK prefix>")
                result = output.Substring(0, output.Length - 1);
            }
            
            return result;
        }
    }
}