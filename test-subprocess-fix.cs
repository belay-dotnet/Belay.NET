using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

Console.WriteLine("Testing subprocess raw REPL fix...");

// Create a simple console logger
using var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
var logger = loggerFactory.CreateLogger<DeviceConnection>();

try 
{
    // Find MicroPython executable
    var executablePath = "/home/corona/belay.net/micropython/ports/unix/build-standard/micropython";
    if (!File.Exists(executablePath))
    {
        Console.WriteLine($"MicroPython executable not found at: {executablePath}");
        return 1;
    }

    Console.WriteLine($"Using MicroPython executable: {executablePath}");

    // Create subprocess device connection
    var device = new DeviceConnection(
        DeviceConnection.ConnectionType.Subprocess, 
        executablePath, 
        logger);

    Console.WriteLine("Connecting to device...");
    await device.ConnectAsync();
    
    Console.WriteLine("Connection successful! Testing simple execution...");
    
    var result = await device.ExecuteAsync("print('Hello from subprocess!')");
    Console.WriteLine($"Execution result: {result}");
    
    Console.WriteLine("Testing mathematical operation...");
    var mathResult = await device.ExecuteAsync("print(2 + 3)");
    Console.WriteLine($"Math result: {mathResult}");
    
    Console.WriteLine("Disconnecting...");
    await device.DisconnectAsync();
    
    Console.WriteLine("Test completed successfully!");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Test failed: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    return 1;
}