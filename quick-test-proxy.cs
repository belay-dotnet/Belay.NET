using System;
using System.Threading.Tasks;
using Belay.Core;
using Belay.Core.Examples;

// Quick test to reproduce the void method issue
class TestVoidMethod
{
    static async Task Main()
    {
        try
        {
            Console.WriteLine("Testing DeviceProxy void method handling...");
            
            // Use subprocess for testing (known to work)
            using var device = Device.FromConnectionString("subprocess:micropython");
            await device.ConnectAsync();
            Console.WriteLine("✓ Connected to subprocess device");
            
            // Create proxy
            var sensor = device.CreateProxy<ISimpleSensorDevice>();
            Console.WriteLine("✓ Created proxy successfully");
            
            // Try to call a void method (returns Task)
            Console.WriteLine("Calling void method SetLEDAsync...");
            await sensor.SetLEDAsync(25, true);
            Console.WriteLine("✓ Void method call succeeded");
            
            await device.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
        }
    }
}