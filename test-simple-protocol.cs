// Test simple protocol without capability detection
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

public class TestSimpleProtocol
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🔧 TESTING SIMPLE PROTOCOL (NO CAPABILITY DETECTION)");
        Console.WriteLine("===================================================");
        
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var logger = loggerFactory.CreateLogger<DeviceConnection>();
        
        // Test with available Raspberry Pi Pico
        string devicePath = "/dev/serial/by-id/usb-MicroPython_Board_in_FS_mode_a8100d7bd7092d6e-if00";
        
        Console.WriteLine($"Testing simple protocol on: {devicePath}");
        Console.WriteLine("==========================================");
        
        try
        {
            using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
            
            // First, let's try the DeviceConnection's basic connection without the sophisticated protocol
            Console.WriteLine("🔌 Test 1: Basic Connection");
            await connection.ConnectAsync();
            Console.WriteLine("   ✅ Basic connection established");
            
            await connection.DisconnectAsync();
            Console.WriteLine("🔌 Disconnected successfully");
            
            Console.WriteLine();
            Console.WriteLine("🎉 SIMPLE CONNECTION TEST PASSED!");
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