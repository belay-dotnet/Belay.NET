using System;
using System.Threading.Tasks;
using Belay.Core;

Console.WriteLine("=== Protocol Diagnostic Tool ===");

if (args.Length == 0)
{
    Console.WriteLine("Usage: ProtocolDiagnostic <connection_string>");
    return;
}

var connectionString = args[0];
Console.WriteLine($"Diagnosing: {connectionString}");

try
{
    using var device = Device.FromConnectionString(connectionString);
    
    Console.WriteLine("\n=== Step 1: Connection ===");
    await device.ConnectAsync();
    Console.WriteLine("✓ Connected");
    
    Console.WriteLine("\n=== Step 2: Simple Expression ===");
    try
    {
        var result = await device.ExecuteAsync("1");
        Console.WriteLine($"Result: '{result}'");
        Console.WriteLine($"Length: {result?.Length ?? 0}");
        Console.WriteLine($"Is null: {result == null}");
        Console.WriteLine($"Is empty: {string.IsNullOrEmpty(result)}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
    
    Console.WriteLine("\n=== Step 3: Print Statement ===");
    try
    {
        var result = await device.ExecuteAsync("print('hello')");
        Console.WriteLine($"Result: '{result}'");
        Console.WriteLine($"Length: {result?.Length ?? 0}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
    
    Console.WriteLine("\n=== Step 4: Math Expression ===");
    try
    {
        var result = await device.ExecuteAsync("2+2");
        Console.WriteLine($"Result: '{result}'");
        Console.WriteLine($"Length: {result?.Length ?? 0}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
    
    Console.WriteLine("\n=== Step 5: Typed Result ===");
    try
    {
        var result = await device.ExecuteAsync<int>("2+2");
        Console.WriteLine($"Typed result: {result}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Typed error: {ex.Message}");
    }
    
    await device.DisconnectAsync();
    Console.WriteLine("✓ Disconnected");
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}