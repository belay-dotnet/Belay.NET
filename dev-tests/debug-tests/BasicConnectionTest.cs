// Basic connection test to verify device path and simple protocol
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Belay.Core;

class BasicConnectionTest
{
    static async Task Main()
    {
        Console.WriteLine("🔧 Basic Connection Test");
        Console.WriteLine("========================");
        
        var devicePath = "/dev/usb/tty-Board_in_FS_mode-a8100d7bd7092d6e"; // RPI Pico - known working
        
        try
        {
            // Test with subprocess first to verify device availability
            Console.WriteLine("🧪 Testing subprocess connection...");
            var subprocessConnection = new DeviceConnection(
                DeviceConnection.ConnectionType.Subprocess,
                "/home/corona/belay.net/micropython/ports/unix/build-standard/micropython",
                NullLogger<DeviceConnection>.Instance);
                
            using var subprocessCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await subprocessConnection.ConnectAsync(subprocessCts.Token);
            Console.WriteLine("✅ Subprocess connection works");
            
            var subprocessResult = await subprocessConnection.ExecuteAsync("2 + 2", subprocessCts.Token);
            Console.WriteLine($"  Subprocess result: '{subprocessResult}' ✅");
            
            await subprocessConnection.DisconnectAsync();
            
            // Now test basic serial connection WITHOUT sophisticated protocol
            Console.WriteLine($"🔌 Testing basic serial connection to {devicePath}...");
            
            // Create connection but disable sophisticated protocol by not setting it up
            var serialConnection = new DeviceConnection(
                DeviceConnection.ConnectionType.Serial, 
                devicePath, 
                NullLogger<DeviceConnection>.Instance);
                
            using var serialCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await serialConnection.ConnectAsync(serialCts.Token);
            Console.WriteLine("✅ Serial connection established");
            
            // Test simple execution using basic Raw REPL
            Console.WriteLine("🧮 Testing basic Raw REPL execution...");
            using var execCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var serialResult = await serialConnection.ExecuteAsync("2 + 2", execCts.Token);
            Console.WriteLine($"  Serial result: '{serialResult}' ✅");
            
            await serialConnection.DisconnectAsync();
            Console.WriteLine("🎉 Basic connection test PASSED!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"   Stack: {ex.StackTrace}");
            return;
        }
    }
}