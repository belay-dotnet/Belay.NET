// Test using the simple working protocol from tagged release
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Belay.Core;

class SimpleProtocolTest  
{
    static async Task Main()
    {
        Console.WriteLine("🔧 Simple Protocol Test");
        Console.WriteLine("=======================");
        
        // Use the working subprocess first
        Console.WriteLine("🧪 Testing subprocess (known working)...");
        var subprocessConnection = new DeviceConnection(
            DeviceConnection.ConnectionType.Subprocess,
            "/home/corona/belay.net/micropython/ports/unix/build-standard/micropython",
            NullLogger<DeviceConnection>.Instance);
            
        try
        {
            using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await subprocessConnection.ConnectAsync(cts1.Token);
            Console.WriteLine("✅ Subprocess connected");
            
            var result1 = await subprocessConnection.ExecuteAsync("2 + 2", cts1.Token);
            Console.WriteLine($"  Result: '{result1}' ✅");
            
            await subprocessConnection.DisconnectAsync();
            Console.WriteLine("✅ Subprocess test complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Subprocess failed: {ex.Message}");
            return;
        }
        
        // Now test simple serial protocol on RPI Pico
        Console.WriteLine("🔌 Testing simple serial protocol on RPI Pico...");
        var devicePath = "/dev/usb/tty-Board_in_FS_mode-a8100d7bd7092d6e";
        
        try
        {
            var serialConnection = new DeviceConnection(
                DeviceConnection.ConnectionType.Serial,
                devicePath,
                NullLogger<DeviceConnection>.Instance);
                
            using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Console.WriteLine("  Connecting...");
            await serialConnection.ConnectAsync(cts2.Token);
            Console.WriteLine("✅ Serial connected");
            
            Console.WriteLine("  Executing 2+2...");
            var result2 = await serialConnection.ExecuteAsync("2 + 2", cts2.Token);
            Console.WriteLine($"  Result: '{result2}' ✅");
            
            await serialConnection.DisconnectAsync();
            Console.WriteLine("✅ Serial test complete");
            
            Console.WriteLine("🎉 All tests PASSED!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Serial test failed: {ex.Message}");
            Console.WriteLine($"   Stack: {ex.StackTrace}");
            
            // Check if it's a device connection issue or protocol issue
            if (ex.Message.Contains("stty") || ex.Message.Contains("No such file"))
            {
                Console.WriteLine("💡 Device path issue - device may have disconnected or changed path");
            }
            else if (ex.Message.Contains("timeout") || ex.Message.Contains("Timeout"))
            {
                Console.WriteLine("💡 Communication timeout - device may be in stuck state");
            }
        }
    }
}