// Simple Raw REPL Test for RPI Pico
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

public class SimpleRpiPicoTest
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🔧 Simple RPI Pico Raw REPL Test");
        Console.WriteLine("================================");
        
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        
        var logger = loggerFactory.CreateLogger<DeviceConnection>();
        var devicePath = "/dev/usb/tty-Board_in_FS_mode-e6614c311b7e6f35";
        
        try
        {
            using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
            
            Console.WriteLine("🔌 Connecting to RPI Pico...");
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await connection.ConnectAsync(connectCts.Token);
            Console.WriteLine("✅ Connected successfully");
            
            Console.WriteLine("🧮 Testing math expression: 2 + 2");
            using var mathCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result = await connection.ExecuteAsync("2 + 2", mathCts.Token);
            Console.WriteLine($"📥 Result: '{result}'");
            
            if (result.Contains("4"))
            {
                Console.WriteLine("✅ TEST PASSED - Math result correct");
                return 0;
            }
            else
            {
                Console.WriteLine("❌ TEST FAILED - Math result incorrect");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TEST ERROR: {ex.Message}");
            return 1;
        }
    }
}