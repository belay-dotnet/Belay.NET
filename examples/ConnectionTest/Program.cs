using System;
using System.Threading.Tasks;
using Belay.Core;

Console.WriteLine("=== Connection Test ===");

if (args.Length == 0)
{
    Console.WriteLine("Usage: ConnectionTest <connection_string>");
    return;
}

var connectionString = args[0];
Console.WriteLine($"Testing connection: {connectionString}");

try
{
    using var device = Device.FromConnectionString(connectionString);
    
    Console.WriteLine("✓ Device created");
    
    Console.WriteLine("Attempting to connect...");
    await device.ConnectAsync();
    Console.WriteLine("✓ Connected successfully!");
    
    Console.WriteLine("Attempting disconnect...");
    await device.DisconnectAsync();
    Console.WriteLine("✓ Disconnected successfully");
    
    Console.WriteLine("\n🎉 Connection test completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Connection test failed: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }
    
    Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
}