// Test basic simplified implementation functionality
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

public class TestSimpleBasic
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🔧 TESTING BASIC SIMPLIFIED IMPLEMENTATION");
        Console.WriteLine("=========================================");
        
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var logger = loggerFactory.CreateLogger<SimpleDeviceConnection>();
        
        // Test with available Raspberry Pi Pico
        string devicePath = "/dev/serial/by-id/usb-MicroPython_Board_in_FS_mode_a8100d7bd7092d6e-if00";
        
        Console.WriteLine($"Testing basic simplified on: {devicePath}");
        Console.WriteLine("=======================================");
        
        try
        {
            using var connection = new SimpleDeviceConnection(
                SimpleDeviceConnection.ConnectionType.Serial, 
                devicePath, 
                logger);
            
            // Test 1: Simple Connection
            Console.WriteLine("🔌 Test 1: Basic Connection");
            await connection.ConnectAsync();
            Console.WriteLine("   ✅ Connection established");
            
            // Test 2: Basic Math
            Console.WriteLine("📝 Test 2: Basic Math");
            var result1 = await connection.ExecuteAsync("2 + 2");
            Console.WriteLine($"   Result: '{result1}'");
            
            // Test 3: Basic Print  
            Console.WriteLine("🖨️ Test 3: Basic Print");
            var result2 = await connection.ExecuteAsync("print('Hello World')");
            Console.WriteLine($"   Result: '{result2}'");
            
            await connection.DisconnectAsync();
            Console.WriteLine("🔌 Disconnected successfully");
            
            Console.WriteLine();
            Console.WriteLine("🎉 BASIC TESTS COMPLETED!");
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