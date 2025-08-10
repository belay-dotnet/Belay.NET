// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Belay.Core;

Console.WriteLine("=== Simple Hardware Validation ===");

if (args.Length == 0)
{
    Console.WriteLine("Usage: SimpleHardwareValidation <connection_string>");
    Console.WriteLine("Examples:");
    Console.WriteLine("  serial:COM3");
    Console.WriteLine("  serial:/dev/ttyUSB0");
    Console.WriteLine("  serial:/dev/ttyACM0");
    return;
}

var connectionString = args[0];
Console.WriteLine($"Testing connection: {connectionString}");

try
{
    using var device = Device.FromConnectionString(connectionString);

    Console.WriteLine("Attempting connection...");
    await device.ConnectAsync();
    Console.WriteLine("✓ Connected successfully!");

    // Test basic execution
    Console.WriteLine("Testing basic execution...");
    var result = await device.ExecuteAsync("print('Hello from device!')");
    Console.WriteLine($"✓ Device response: {result}");

    // Test platform detection
    try
    {
        var platform = await device.ExecuteAsync<string>("import sys; sys.platform");
        Console.WriteLine($"✓ Platform: {platform}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ Platform detection failed: {ex.Message}");
    }

    // Test simple math
    try
    {
        var math = await device.ExecuteAsync<int>("2 + 2");
        Console.WriteLine($"✓ Math test: 2 + 2 = {math}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ Math test failed: {ex.Message}");
    }

    await device.DisconnectAsync();
    Console.WriteLine("✓ Disconnected successfully");

    Console.WriteLine("\n🎉 Hardware validation completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Validation failed: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }

    Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
}
