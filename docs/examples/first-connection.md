# First Connection

This tutorial walks you through making your first connection to a MicroPython device using Belay.NET. You'll learn the basics of device communication and remote code execution.

## Prerequisites

- .NET 8.0 or later installed
- MicroPython device (Raspberry Pi Pico, ESP32, etc.) with MicroPython firmware
- USB cable to connect your device

## Step 1: Create the Project

Create a new console application:

```bash
mkdir MyFirstBelayApp
cd MyFirstBelayApp
dotnet new console
dotnet add package Belay.NET
```

## Step 2: Find Your Device

Before connecting, you need to identify your device's port:

### Windows
Open Device Manager and look under "Ports (COM & LPT)". Your MicroPython device will appear as something like "USB Serial Device (COM3)".

### Linux  
Run `ls /dev/ttyUSB*` or `ls /dev/ttyACM*`. Your device will typically be `/dev/ttyUSB0` or `/dev/ttyACM0`.

### macOS
Run `ls /dev/cu.usbmodem*`. Your device will appear as `/dev/cu.usbmodem[identifier]`.

## Step 3: Basic Connection

Replace the contents of `Program.cs` with:

```csharp
using Belay.Core;
using System;

try
{
    // Connect to your MicroPython device
    using var device = await Device.ConnectAsync("COM3"); // Change to your port
    
    Console.WriteLine("Connected to MicroPython device!");
    
    // Test basic communication
    await device.ExecuteAsync("print('Hello from MicroPython!')");
    
    // Get device information  
    var version = await device.ExecuteAsync<string>("import sys; sys.version");
    Console.WriteLine($"MicroPython Version: {version}");
    
    var platform = await device.ExecuteAsync<string>("sys.platform");
    Console.WriteLine($"Platform: {platform}");
}
catch (DeviceConnectionException ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
    Console.WriteLine("Make sure:");
    Console.WriteLine("- Your device is connected via USB");
    Console.WriteLine("- MicroPython firmware is installed");
    Console.WriteLine("- No other applications are using the port");
    Console.WriteLine("- The port name is correct");
}
```

Run the application:

```bash
dotnet run
```

You should see output like:
```
Connected to MicroPython device!
Hello from MicroPython!
MicroPython Version: 3.4.0; MicroPython v1.20.0 on 2023-04-26; Raspberry Pi Pico with RP2040
Platform: rp2
```

## Step 4: Understanding the Code

Let's break down what's happening:

### Device Connection
```csharp
using var device = await Device.ConnectAsync("COM3");
```

This establishes a connection to your MicroPython device using the Raw REPL protocol. The `using` statement ensures the connection is properly closed when done.

### Remote Code Execution
```csharp
await device.ExecuteAsync("print('Hello from MicroPython!')");
```

This sends Python code to your device and executes it remotely. The `print` statement output appears in your .NET console.

### Type-Safe Return Values
```csharp
var version = await device.ExecuteAsync<string>("import sys; sys.version");
```

You can execute Python code and get strongly-typed return values. Belay.NET automatically converts Python data types to .NET types.

## Step 5: Exploring Device Capabilities

Extend your program to explore what your device can do:

```csharp
using Belay.Core;
using System;

try
{
    using var device = await Device.ConnectAsync("COM3");
    
    Console.WriteLine("=== Device Information ===");
    
    // Basic system info
    var version = await device.ExecuteAsync<string>("import sys; sys.version");
    Console.WriteLine($"Version: {version}");
    
    var platform = await device.ExecuteAsync<string>("sys.platform");
    Console.WriteLine($"Platform: {platform}");
    
    // Memory information
    var freeMemory = await device.ExecuteAsync<int>("""
        import gc
        gc.collect()
        gc.mem_free()
        """);
    Console.WriteLine($"Free Memory: {freeMemory:N0} bytes");
    
    var allocatedMemory = await device.ExecuteAsync<int>("gc.mem_alloc()");
    Console.WriteLine($"Allocated Memory: {allocatedMemory:N0} bytes");
    
    // Available modules
    Console.WriteLine("\n=== Available Modules ===");
    var modules = await device.ExecuteAsync<List<string>>("""
        import sys
        sorted(sys.modules.keys())
        """);
    
    foreach (var module in modules.Take(10)) // Show first 10
    {
        Console.WriteLine($"- {module}");
    }
    
    if (modules.Count > 10)
    {
        Console.WriteLine($"... and {modules.Count - 10} more modules");
    }
    
    // Hardware-specific features (if available)
    Console.WriteLine("\n=== Hardware Features ===");
    
    try
    {
        await device.ExecuteAsync("from machine import Pin");
        Console.WriteLine("✓ GPIO pins available");
    }
    catch
    {
        Console.WriteLine("✗ GPIO not available");
    }
    
    try
    {
        await device.ExecuteAsync("from machine import ADC");
        Console.WriteLine("✓ Analog-to-Digital Converter available");
    }
    catch
    {
        Console.WriteLine("✗ ADC not available");
    }
    
    try
    {
        await device.ExecuteAsync("from machine import PWM");
        Console.WriteLine("✓ Pulse Width Modulation available");
    }
    catch
    {
        Console.WriteLine("✗ PWM not available");
    }
    
    try
    {
        await device.ExecuteAsync("from machine import I2C");
        Console.WriteLine("✓ I2C communication available");
    }
    catch
    {
        Console.WriteLine("✗ I2C not available");
    }
    
    try
    {
        await device.ExecuteAsync("from machine import SPI");
        Console.WriteLine("✓ SPI communication available");
    }
    catch
    {
        Console.WriteLine("✗ SPI not available");
    }
}
catch (DeviceConnectionException ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
}
catch (DeviceExecutionException ex)
{
    Console.WriteLine($"Execution error: {ex.Message}");
    Console.WriteLine($"Python error type: {ex.PythonExceptionType}");
}
```

## Step 6: Interactive Exploration

Create an interactive program to experiment with your device:

```csharp
using Belay.Core;
using System;

try
{
    using var device = await Device.ConnectAsync("COM3");
    
    Console.WriteLine("Connected to MicroPython device!");
    Console.WriteLine("Type Python code to execute, or 'quit' to exit.");
    Console.WriteLine("Examples:");
    Console.WriteLine("  print('Hello World')");
    Console.WriteLine("  2 + 2");
    Console.WriteLine("  import time; time.time()");
    Console.WriteLine();
    
    while (true)
    {
        Console.Write(">>> ");
        var input = Console.ReadLine();
        
        if (string.IsNullOrWhiteSpace(input))
            continue;
            
        if (input.Trim().ToLower() == "quit")
            break;
        
        try
        {
            // Try to get a return value first
            var result = await device.ExecuteAsync<object>(input);
            if (result != null)
            {
                Console.WriteLine(result);
            }
        }
        catch (DeviceExecutionException ex)
        {
            // If that fails, just execute without expecting a return value
            try
            {
                await device.ExecuteAsync(input);
            }
            catch (DeviceExecutionException ex2)
            {
                Console.WriteLine($"Error: {ex2.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }
    
    Console.WriteLine("Goodbye!");
}
catch (DeviceConnectionException ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
}
```

## Common Issues and Solutions

### Connection Failed
**Error**: `DeviceConnectionException: Could not connect to device`

**Solutions**:
1. Verify the port name is correct
2. Make sure no other applications (Arduino IDE, Thonny, etc.) are using the port
3. Try unplugging and reconnecting the device
4. Ensure MicroPython firmware is properly installed

### Permission Denied (Linux/macOS)
**Error**: Permission denied when accessing `/dev/ttyUSB0`

**Solutions**:
```bash
# Add your user to the dialout group (Linux)
sudo usermod -a -G dialout $USER
# Log out and back in for changes to take effect

# Or use sudo (not recommended for production)
sudo dotnet run
```

### Import Errors
**Error**: `ModuleNotFoundError: No module named 'machine'`

**Solution**: This indicates you're not running MicroPython. The `machine` module is specific to MicroPython, not standard Python.

### Device Not Responding
If your device stops responding:

1. Disconnect and reconnect the USB cable
2. Press the reset button on your device (if available)
3. Try a different USB port or cable

## Understanding Data Types

Belay.NET automatically converts between Python and .NET data types:

```csharp
// Basic types
var integer = await device.ExecuteAsync<int>("42");
var floating = await device.ExecuteAsync<float>("3.14159");
var text = await device.ExecuteAsync<string>("'Hello World'");
var boolean = await device.ExecuteAsync<bool>("True");

// Collections
var list = await device.ExecuteAsync<List<int>>("[1, 2, 3, 4, 5]");
var dictionary = await device.ExecuteAsync<Dictionary<string, object>>("""
    {'name': 'device1', 'value': 42, 'active': True}
    """);

// Complex objects using anonymous types
var deviceInfo = await device.ExecuteAsync<dynamic>("""
    {
        'platform': sys.platform,
        'version': sys.version[:20],
        'memory_free': gc.mem_free()
    }
    """);

Console.WriteLine($"Platform: {deviceInfo.platform}");
Console.WriteLine($"Version: {deviceInfo.version}");
Console.WriteLine($"Free Memory: {deviceInfo.memory_free}");
```

## Next Steps

Now that you have basic communication working, explore these tutorials:

1. **[LED Control](./led-control.md)** - Learn GPIO control and hardware interaction
2. **[Sensor Reading](./sensor-reading.md)** - Work with analog sensors and data processing
3. **[Device Communication Guide](../guide/device-communication.md)** - Deep dive into the communication protocol
4. **[Attribute Programming](../guide/attributes.md)** - Learn the powerful attribute-based programming model

## Complete Example Code

Here's the complete code for a device information explorer:

```csharp
using Belay.Core;
using System;

class Program
{
    static async Task Main(string[] args)
    {
        var portName = args.Length > 0 ? args[0] : "COM3";
        
        try
        {
            using var device = await Device.ConnectAsync(portName);
            
            Console.WriteLine($"Connected to MicroPython device on {portName}");
            
            await DisplayDeviceInfo(device);
            await ExploreCapabilities(device);
            
            Console.WriteLine("\nConnection test completed successfully!");
        }
        catch (DeviceConnectionException ex)
        {
            Console.WriteLine($"Failed to connect to {portName}: {ex.Message}");
            Console.WriteLine("\nTroubleshooting:");
            Console.WriteLine("- Verify the port name is correct");
            Console.WriteLine("- Ensure MicroPython firmware is installed");
            Console.WriteLine("- Check that no other applications are using the port");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }
    
    static async Task DisplayDeviceInfo(Device device)
    {
        Console.WriteLine("\n=== Device Information ===");
        
        var version = await device.ExecuteAsync<string>("import sys; sys.version");
        var platform = await device.ExecuteAsync<string>("sys.platform");
        var freeMemory = await device.ExecuteAsync<int>("import gc; gc.mem_free()");
        
        Console.WriteLine($"Version: {version}");
        Console.WriteLine($"Platform: {platform}");
        Console.WriteLine($"Free Memory: {freeMemory:N0} bytes");
    }
    
    static async Task ExploreCapabilities(Device device)
    {
        Console.WriteLine("\n=== Hardware Capabilities ===");
        
        var capabilities = new[]
        {
            ("GPIO", "from machine import Pin"),
            ("ADC", "from machine import ADC"),
            ("PWM", "from machine import PWM"),
            ("I2C", "from machine import I2C"),
            ("SPI", "from machine import SPI"),
            ("Timer", "from machine import Timer"),
            ("RTC", "from machine import RTC")
        };
        
        foreach (var (name, importCode) in capabilities)
        {
            try
            {
                await device.ExecuteAsync(importCode);
                Console.WriteLine($"✓ {name} available");
            }
            catch
            {
                Console.WriteLine($"✗ {name} not available");
            }
        }
    }
}
```

Run with: `dotnet run COM3` (or your device's port)