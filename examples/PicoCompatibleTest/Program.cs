using System;
using System.Linq;
using System.Threading.Tasks;
using Belay.Core;

Console.WriteLine("=== Pico Compatible Test (Raw Protocol Analysis) ===");

if (args.Length == 0)
{
    Console.WriteLine("Usage: PicoCompatibleTest <connection_string>");
    return;
}

var connectionString = args[0];
Console.WriteLine($"Testing: {connectionString}");

try
{
    using var device = Device.FromConnectionString(connectionString);
    
    await device.ConnectAsync();
    Console.WriteLine("✓ Connected");
    
    // Let's manually test what the Pico sends back
    Console.WriteLine("\n=== Raw Communication Test ===");
    Console.WriteLine("This will help us understand what the Pico actually sends...");
    
    // Test simple cases to see what we actually get back
    var testCases = new[]
    {
        "1",
        "print('hello')", 
        "2+2",
        "import sys; sys.platform"
    };
    
    foreach (var testCase in testCases)
    {
        Console.WriteLine($"\nTesting: {testCase}");
        try
        {
            // Try different approaches to see what works
            var result = await device.ExecuteAsync(testCase);
            Console.WriteLine($"  Raw result: '{result}'");
            Console.WriteLine($"  Length: {result?.Length ?? 0}");
            Console.WriteLine($"  Is null: {result == null}");
            Console.WriteLine($"  Is empty: {string.IsNullOrEmpty(result)}");
            Console.WriteLine($"  Bytes: [{string.Join(", ", (result ?? "").Select(c => ((int)c).ToString()))}]");
            
            if (result?.Length > 0)
            {
                Console.WriteLine($"  Hex: {string.Join(" ", result.Select(c => $"0x{((int)c):X2}"))}");
                Console.WriteLine($"  Escaped: {result.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
            Console.WriteLine($"  Exception Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner Exception: {ex.InnerException.Message}");
            }
        }
        
        // Small delay between tests
        await Task.Delay(500);
    }
    
    await device.DisconnectAsync();
    Console.WriteLine("\n✓ Test completed");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}