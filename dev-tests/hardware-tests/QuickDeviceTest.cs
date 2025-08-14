// Quick device connectivity test
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Belay.Core;

class QuickDeviceTest
{
    static async Task Main()
    {
        Console.WriteLine("🔧 Quick Device Connectivity Test");
        Console.WriteLine("==================================");
        
        var devices = new[]
        {
            ("ESP32C6", "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94"),
            ("STM32WB55", "/dev/usb/tty-Board_in_FS_mode-a8100d7bd7092d6e")
        };

        foreach (var (name, path) in devices)
        {
            Console.WriteLine($"\n🎯 Testing {name}: {path}");
            Console.WriteLine("=" + new string('=', 30 + name.Length));
            
            try
            {
                var connection = new DeviceConnection(
                    DeviceConnection.ConnectionType.Serial, 
                    path, 
                    NullLogger<DeviceConnection>.Instance);

                Console.WriteLine("  Step 1: Creating connection...");
                
                Console.WriteLine("  Step 2: Attempting connection with 20s timeout...");
                using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                await connection.ConnectAsync(connectCts.Token);
                Console.WriteLine("  ✅ Connected successfully!");

                Console.WriteLine("  Step 3: Testing simple execution...");
                using var execCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var result = await connection.ExecuteAsync("print('test')", execCts.Token);
                Console.WriteLine($"  ✅ Execution result: '{result}'");

                await connection.DisconnectAsync();
                Console.WriteLine($"  ✅ {name} test PASSED!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ {name} test FAILED: {ex.Message}");
                Console.WriteLine($"     Type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"     Inner: {ex.InnerException.Message}");
                }
            }
        }

        Console.WriteLine("\n📊 Quick connectivity test complete");
    }
}