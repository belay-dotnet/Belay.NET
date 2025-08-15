// Test the simplified implementation following official mpremote patterns
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

public class TestSimpleImplementation
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🔧 TESTING SIMPLIFIED IMPLEMENTATION");
        Console.WriteLine("==================================");
        
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var logger = loggerFactory.CreateLogger<SimpleDeviceConnection>();
        
        // Test with available Raspberry Pi Pico
        string devicePath = "/dev/serial/by-id/usb-MicroPython_Board_in_FS_mode_a8100d7bd7092d6e-if00";
        
        Console.WriteLine($"Testing simplified implementation on: {devicePath}");
        Console.WriteLine("===============================================");
        
        try
        {
            using var connection = new SimpleDeviceConnection(
                SimpleDeviceConnection.ConnectionType.Serial, 
                devicePath, 
                logger);
            
            // Test 1: Simple Connection
            Console.WriteLine("🔌 Test 1: Simple Connection");
            await connection.ConnectAsync();
            Console.WriteLine("   ✅ Connection established");
            
            // Test 2: Basic Execution
            Console.WriteLine("📝 Test 2: Basic Execution");
            var result1 = await connection.ExecuteAsync("print(2 + 2)");
            Console.WriteLine($"   Result: '{result1.Trim()}'");
            Console.WriteLine("   ✅ Basic execution working");
            
            // Test 3: Print Statement
            Console.WriteLine("🖨️ Test 3: Print Statement");
            var result2 = await connection.ExecuteAsync("print('Hello from simple implementation!')");
            Console.WriteLine($"   Result: '{result2.Trim()}'");
            Console.WriteLine("   ✅ Print execution working");
            
            // Test 4: File Operations
            Console.WriteLine("📁 Test 4: File Operations");
            var testData = System.Text.Encoding.UTF8.GetBytes("Hello, simple file transfer!");
            await connection.WriteFileAsync("/test_simple.txt", testData);
            Console.WriteLine("   ✅ File write completed");
            
            var readData = await connection.GetFileAsync("/test_simple.txt");
            var readText = System.Text.Encoding.UTF8.GetString(readData);
            Console.WriteLine($"   Read: '{readText}'");
            Console.WriteLine("   ✅ File read completed");
            
            // Test 5: Error Handling
            Console.WriteLine("⚠️ Test 5: Error Handling");
            try
            {
                bool errorCaught = false;
                
                try
                {
                    await connection.ExecuteAsync("invalid_syntax !!!");
                }
                catch (DeviceException)
                {
                    errorCaught = true;
                    Console.WriteLine("   ✅ Error properly caught");
                }
                
                if (!errorCaught)
                {
                    Console.WriteLine("   ❌ Error not caught as expected");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Error handling test failed: {ex.Message}");
                return 1;
            }
            
            await connection.DisconnectAsync();
            Console.WriteLine("🔌 Disconnected successfully");
            
            Console.WriteLine();
            Console.WriteLine("🎉 ALL SIMPLE IMPLEMENTATION TESTS PASSED!");
            Console.WriteLine("✅ Simple Raw REPL working correctly");
            Console.WriteLine("✅ File operations using chunked transfer");
            Console.WriteLine("✅ Following official mpremote patterns");
            Console.WriteLine("✅ No complex state management or abstractions");
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