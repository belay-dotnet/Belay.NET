using System;
using System.Reflection;
using Belay.Core;
using Belay.Core.Execution;
using Belay.Core.Examples;

// Simple test to reproduce the ambiguous method resolution error
class TestProxy
{
    static async Task Main()
    {
        try
        {
            Console.WriteLine("Testing DeviceProxy method resolution...");
            
            // Use subprocess for testing
            using var device = Device.FromConnectionString("subprocess:micropython");
            await device.ConnectAsync();
            Console.WriteLine("✓ Connected to device");
            
            // Create proxy - this should trigger the error
            var monitor = device.CreateProxy<IEnvironmentMonitor>();
            Console.WriteLine("✓ Created proxy successfully");
            
            // Try to call a method
            await monitor.InitializeHardwareAsync();
            Console.WriteLine("✓ Method call succeeded");
            
            await device.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"\nInner exception: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                Console.WriteLine($"Inner stack trace:\n{ex.InnerException.StackTrace}");
            }
        }
    }
}