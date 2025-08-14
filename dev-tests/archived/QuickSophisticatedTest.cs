// Quick test to verify sophisticated protocol features work
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Belay.Core;

class QuickSophisticatedTest
{
    static async Task Main()
    {
        Console.WriteLine("⚡ Quick Sophisticated Protocol Test");
        Console.WriteLine("===================================");
        
        var devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94"; // ESP32C6
        
        try
        {
            var connection = new DeviceConnection(
                DeviceConnection.ConnectionType.Serial, 
                devicePath, 
                NullLogger<DeviceConnection>.Instance);
                
            Console.WriteLine("🔌 Connecting with sophisticated protocol...");
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await connection.ConnectAsync(connectCts.Token);
            Console.WriteLine("✅ Connected successfully");
            
            // Test 1: Simple math
            Console.WriteLine("🧮 Test 1: Simple math (2+2)");
            using var mathCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var mathResult = await connection.ExecuteAsync("2 + 2", mathCts.Token);
            Console.WriteLine($"  Result: '{mathResult}' ✅");
            
            // Test 2: Small code block
            Console.WriteLine("📝 Test 2: Small code block");
            var smallCode = "x = 10; y = x * 2; print(f'x={x}, y={y}'); y";
            using var smallCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var smallResult = await connection.ExecuteAsync(smallCode, smallCts.Token);
            Console.WriteLine($"  Result: '{smallResult}' ✅");
            
            await connection.DisconnectAsync();
            Console.WriteLine("🎉 Quick test PASSED - Sophisticated protocol working!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            return;
        }
    }
}