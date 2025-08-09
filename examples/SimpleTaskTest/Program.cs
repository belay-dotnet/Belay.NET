// Simple test to isolate Task attribute functionality
using Belay.Core;
using Belay.Attributes;

Console.WriteLine("=== Simple Task Attribute Test ===");

try
{
    // Create device using subprocess
    var device = Device.FromConnectionString("subprocess:../../micropython/ports/unix/build-standard/micropython");
    
    Console.WriteLine("Connecting...");
    await device.ConnectAsync();
    Console.WriteLine("✓ Connected");

    // Test direct execution (no Task attribute)
    Console.WriteLine("Testing direct execution...");
    var directResult = await device.ExecuteAsync<int>("2 + 3");
    Console.WriteLine($"Direct result: {directResult}");

    // Test TaskExecutor policies without Task attribute
    Console.WriteLine("Testing TaskExecutor directly...");
    var taskResult = await device.Task.ApplyPoliciesAndExecuteAsync<string>("'hello from task executor'");
    Console.WriteLine($"Task executor result: {taskResult}");

    await device.DisconnectAsync();
    Console.WriteLine("✓ Test completed");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    if (ex.InnerException != null)
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}