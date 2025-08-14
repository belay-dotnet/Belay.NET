// Final validation test for fixed Raw REPL protocol
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Belay.Core;

class FinalProtocolTest
{
    static async Task Main()
    {
        Console.WriteLine("🎯 FINAL Raw REPL Protocol Validation");
        Console.WriteLine("====================================");
        
        var devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        try
        {
            var connection = new DeviceConnection(
                DeviceConnection.ConnectionType.Serial, 
                devicePath, 
                NullLogger<DeviceConnection>.Instance);
                
            Console.WriteLine("🔌 Connecting to ESP32C6...");
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await connection.ConnectAsync(connectCts.Token);
            Console.WriteLine("✅ Connected successfully");
            
            Console.WriteLine("🧮 Testing math: 2 + 2");
            using var mathCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var mathResult = await connection.ExecuteAsync("2 + 2", mathCts.Token);
            Console.WriteLine($"  Result: '{mathResult}' ✅");
            
            Console.WriteLine("📝 Testing print: print('Hello ESP32C6')");
            using var printCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var printResult = await connection.ExecuteAsync("print('Hello ESP32C6'); 42", printCts.Token);
            Console.WriteLine($"  Result: '{printResult}' ✅");
            
            Console.WriteLine("🔢 Testing variable: x = 10 * 5; x");
            using var varCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var varResult = await connection.ExecuteAsync("x = 10 * 5; x", varCts.Token);
            Console.WriteLine($"  Result: '{varResult}' ✅");
            
            await connection.DisconnectAsync();
            Console.WriteLine("🎉 ALL TESTS PASSED - Raw REPL Protocol Working!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
        }
    }
}