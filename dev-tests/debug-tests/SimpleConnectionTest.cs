// Test basic connection and simple execution
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Belay.Core;

class SimpleConnectionTest
{
    static async Task Main()
    {
        Console.WriteLine("🔧 Simple Connection Test");
        Console.WriteLine("==========================");
        
        var devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        try
        {
            Console.WriteLine($"Step 1: Creating DeviceConnection for {devicePath}");
            var connection = new DeviceConnection(
                DeviceConnection.ConnectionType.Serial, 
                devicePath, 
                NullLogger<DeviceConnection>.Instance);
            Console.WriteLine("✅ DeviceConnection created");
            
            Console.WriteLine("Step 2: Connecting...");
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await connection.ConnectAsync(connectCts.Token);
            Console.WriteLine("✅ Connected successfully!");
            
            Console.WriteLine("Step 3: Testing simple execution...");
            using var execCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var result = await connection.ExecuteAsync("print(2 + 2)", execCts.Token);
            Console.WriteLine($"✅ Execution result: '{result}'");
            
            await connection.DisconnectAsync();
            Console.WriteLine("🎉 Simple test PASSED!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"   Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
            Console.WriteLine($"   Stack: {ex.StackTrace}");
        }
    }
}