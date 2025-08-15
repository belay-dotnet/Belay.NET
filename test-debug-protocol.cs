// Debug the protocol step by step to understand what's happening
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

public class TestDebugProtocol
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🔧 DEBUG PROTOCOL STEP BY STEP");
        Console.WriteLine("==============================");
        
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var logger = loggerFactory.CreateLogger<DeviceConnection>();
        
        // Use the working minimal approach first to confirm communication
        string devicePath = "/dev/serial/by-id/usb-MicroPython_Board_in_FS_mode_a8100d7bd7092d6e-if00";
        
        Console.WriteLine($"Testing protocol debugging on: {devicePath}");
        Console.WriteLine("==========================================");
        
        try
        {
            // Test the updated DeviceConnection (now using SimpleRawRepl internally)
        using var connection = new DeviceConnection(
                DeviceConnection.ConnectionType.Serial, 
                devicePath, 
                logger);
            
            // Just test connection
            Console.WriteLine("🔌 Test: Connection Only");
            await connection.ConnectAsync();
            Console.WriteLine("   ✅ Connection established");
            
            // Try one simple command
            Console.WriteLine("📝 Test: One Simple Command");
            try
            {
                var result = await connection.ExecuteAsync("1+1");
                Console.WriteLine($"   Result: '{result}' (Length: {result.Length})");
                
                // Show hex dump of result to understand what we're getting
                var hexDump = string.Join(" ", result.Select(c => $"{(int)c:X2}"));
                Console.WriteLine($"   Hex dump: {hexDump}");
                
                // Also test a more explicit command that should give more obvious output
                Console.WriteLine("📝 Test: Print Statement");
                var printResult = await connection.ExecuteAsync("print('hello world')");
                Console.WriteLine($"   Print result: '{printResult}' (Length: {printResult.Length})");
                var printHex = string.Join(" ", printResult.Select(c => $"{(int)c:X2}"));
                Console.WriteLine($"   Print hex dump: {printHex}");
                
                // Note: DeviceConnection doesn't have WriteFileAsync, 
                // it uses PutFileAsync with different signature. 
                // Focus on basic protocol testing for now.
                
                if (string.IsNullOrEmpty(result))
                {
                    Console.WriteLine("   ⚠️ Result is empty - this confirms the parsing issue");
                }
                else
                {
                    Console.WriteLine("   ✅ Got actual result!");
                    
                    // Try to parse expected result
                    var trimmed = result.Trim();
                    if (trimmed == "2")
                    {
                        Console.WriteLine("   🎯 Perfect! Got expected result '2'");
                    }
                    else
                    {
                        Console.WriteLine($"   ⚠️ Unexpected result, expected '2' but got '{trimmed}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Execution failed: {ex.Message}");
                Console.WriteLine($"   Exception type: {ex.GetType().Name}");
            }
            
            await connection.DisconnectAsync();
            Console.WriteLine("🔌 Disconnected successfully");
            
            Console.WriteLine();
            Console.WriteLine("🎯 PROTOCOL DEBUG COMPLETED");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"Exception Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            return 1;
        }
    }
}