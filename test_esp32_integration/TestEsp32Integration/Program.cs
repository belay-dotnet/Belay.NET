// Test script to validate ESP32 integration with new DI architecture
using Belay.Core;
using Belay.Core.Communication;
using Belay.Extensions;
using Belay.Extensions.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Create a service collection with Belay.NET services
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

// Add Belay services with configuration
services.AddBelay(config =>
{
    config.Communication.Serial.DefaultBaudRate = 115200;
    config.Communication.Serial.ReadTimeoutMs = 5000;
});

// Build the service provider
var serviceProvider = services.BuildServiceProvider();

// Get the device factory from DI
var deviceFactory = serviceProvider.GetRequiredService<IDeviceFactory>();

try
{
    Console.WriteLine("Creating ESP32 device via DI factory...");
    
    // Create device using factory (should use new refactored architecture)
    var device = deviceFactory.CreateSerialDevice("/dev/ttyACM2");
    
    Console.WriteLine("Connecting to ESP32...");
    await device.ConnectAsync();
    
    Console.WriteLine("Executing test command...");
    var result = await device.ExecuteAsync<string>("print('Hello from ESP32 via DI!')");
    Console.WriteLine($"Result: {result}");
    
    Console.WriteLine("Testing sessions property...");
    var sessionStats = await device.Sessions.GetSessionStatsAsync();
    Console.WriteLine($"Active sessions: {sessionStats.ActiveSessionCount}");
    
    Console.WriteLine("ESP32 DI integration test PASSED!");
    
    device.Dispose();
}
catch (Exception ex)
{
    Console.WriteLine($"ESP32 DI integration test FAILED: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}
finally
{
    serviceProvider.Dispose();
}
