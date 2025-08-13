// Cross-Platform Hardware Integration Test
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

public class CrossPlatformHardwareTest
{
    private static readonly string[] TestDevices = {
        "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94",   // ESP32C6
        "/dev/usb/tty-STM32_STLink-066FFF303430484257255318"             // STM32WB55
    };
    
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🔧 CROSS-PLATFORM HARDWARE TEST");
        Console.WriteLine("=================================");
        
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var logger = loggerFactory.CreateLogger<DeviceConnection>();
        
        for (int i = 0; i < TestDevices.Length; i++)
        {
            var devicePath = TestDevices[i];
            var deviceName = i == 0 ? "ESP32C6" : "STM32WB55";
            
            Console.WriteLine($"\n🔍 Testing {deviceName}: {devicePath}");
            Console.WriteLine(new string('=', 60));
            
            try
            {
                await TestDevice(devicePath, deviceName, logger);
                Console.WriteLine($"✅ {deviceName} PASSED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {deviceName} FAILED: {ex.Message}");
                Console.WriteLine($"Exception: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner: {ex.InnerException.Message}");
                }
            }
        }
        
        return 0;
    }
    
    private static async Task TestDevice(string devicePath, string deviceName, ILogger<DeviceConnection> logger)
    {
        using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
        
        // Test 1: Basic connection
        Console.WriteLine("🔌 Basic Connection Test");
        await connection.ConnectAsync();
        Console.WriteLine("   ✅ Connected");
        
        // Test 2: Simple command
        Console.WriteLine("📝 Simple Command Test");
        var result1 = await connection.ExecuteAsync("2 + 2");
        Console.WriteLine($"   Result: {result1.Trim()}");
        
        await connection.DisconnectAsync();
        Console.WriteLine("   ✅ Disconnected");
    }
}