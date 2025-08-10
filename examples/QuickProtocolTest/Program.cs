using System;
using System.Threading.Tasks;
using Belay.Core;

Console.WriteLine("=== Quick Protocol Test ===");

if (args.Length == 0)
{
    Console.WriteLine("Usage: QuickProtocolTest <connection_string>");
    return;
}

var connectionString = args[0];
Console.WriteLine($"Testing: {connectionString}");

try
{
    using var device = Device.FromConnectionString(connectionString);
    
    Console.WriteLine("\n=== Connection Test ===");
    var connectTask = device.ConnectAsync();
    if (await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask)
    {
        Console.WriteLine("✓ Connected successfully");
    }
    else
    {
        Console.WriteLine("❌ Connection timeout");
        return;
    }
    
    Console.WriteLine("\n=== Simple Expression Test ===");
    try
    {
        var executeTask = device.ExecuteAsync("1");
        if (await Task.WhenAny(executeTask, Task.Delay(10000)) == executeTask)
        {
            var result = await executeTask;
            Console.WriteLine($"✓ Expression result: '{result}'");
            Console.WriteLine($"  Length: {result?.Length ?? 0}");
            Console.WriteLine($"  Is empty: {string.IsNullOrEmpty(result)}");
        }
        else
        {
            Console.WriteLine("❌ Execution timeout");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Execution error: {ex.Message}");
    }
    
    Console.WriteLine("\n=== Math Expression Test ===");
    try
    {
        var executeTask = device.ExecuteAsync("2+2");
        if (await Task.WhenAny(executeTask, Task.Delay(10000)) == executeTask)
        {
            var result = await executeTask;
            Console.WriteLine($"✓ Math result: '{result}'");
            Console.WriteLine($"  Length: {result?.Length ?? 0}");
            Console.WriteLine($"  Is empty: {string.IsNullOrEmpty(result)}");
        }
        else
        {
            Console.WriteLine("❌ Math execution timeout");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Math execution error: {ex.Message}");
    }
    
    Console.WriteLine("\n=== Print Statement Test ===");
    try
    {
        var executeTask = device.ExecuteAsync("print('hello')");
        if (await Task.WhenAny(executeTask, Task.Delay(10000)) == executeTask)
        {
            var result = await executeTask;
            Console.WriteLine($"✓ Print result: '{result}'");
            Console.WriteLine($"  Length: {result?.Length ?? 0}");
            Console.WriteLine($"  Is empty: {string.IsNullOrEmpty(result)}");
        }
        else
        {
            Console.WriteLine("❌ Print execution timeout");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Print execution error: {ex.Message}");
    }
    
    await device.DisconnectAsync();
    Console.WriteLine("\n✓ Test completed");
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}");
}