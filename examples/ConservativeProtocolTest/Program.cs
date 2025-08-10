using System;
using System.Threading.Tasks;
using Belay.Core;
using Microsoft.Extensions.DependencyInjection;
using Belay.Extensions.Configuration;

Console.WriteLine("=== Conservative Protocol Test ===");

if (args.Length == 0)
{
    Console.WriteLine("Usage: ConservativeProtocolTest <connection_string>");
    return;
}

var connectionString = args[0];
Console.WriteLine($"Testing connection: {connectionString}");

try
{
    // Configure with conservative settings
    var services = new ServiceCollection();
    services.AddBelay(config =>
    {
        // Disable all adaptive features
        config.Communication.RawRepl.EnableAdaptiveTiming = false;
        config.Communication.RawRepl.EnableAdaptiveFlowControl = false;
        config.Communication.RawRepl.EnableRawPasteAutoDetection = false;
        
        // Use conservative manual settings
        config.Communication.RawRepl.BaseResponseTimeout = TimeSpan.FromSeconds(5);
        config.Communication.RawRepl.InitializationTimeout = TimeSpan.FromSeconds(10);
        config.Communication.RawRepl.StartupDelay = TimeSpan.FromSeconds(3);
        config.Communication.RawRepl.InterruptDelay = TimeSpan.FromMilliseconds(500);
        config.Communication.RawRepl.PreferredWindowSize = 32;
        config.Communication.RawRepl.MaxRetryAttempts = 5;
        config.Communication.RawRepl.RetryDelay = TimeSpan.FromMilliseconds(500);
        
        // Enable verbose logging
        config.Communication.RawRepl.EnableVerboseLogging = true;
    });
    
    Console.WriteLine("✓ Conservative configuration applied");
    
    using var device = Device.FromConnectionString(connectionString);
    
    Console.WriteLine("Connecting to device...");
    await device.ConnectAsync();
    Console.WriteLine("✓ Connected successfully!");
    
    // Test simple execution
    Console.WriteLine("Testing simple code execution...");
    var result = await device.ExecuteAsync("2 + 2");
    Console.WriteLine($"✓ Simple math: {result}");
    
    // Test print
    Console.WriteLine("Testing print statement...");
    var printResult = await device.ExecuteAsync("print('Hello from Pico!')");
    Console.WriteLine($"✓ Print result: {printResult}");
    
    await device.DisconnectAsync();
    Console.WriteLine("✓ Disconnected successfully");
    
    Console.WriteLine("\n🎉 Conservative protocol test completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Test failed: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }
    
    Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
}