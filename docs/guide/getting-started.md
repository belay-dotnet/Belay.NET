# Getting Started

Welcome to Belay.NET! This guide will help you set up your first MicroPython device connection and execute remote code in just a few minutes.

## Prerequisites

Before you begin, ensure you have:

- **.NET 8.0 SDK** or later installed
- A **MicroPython device** (Raspberry Pi Pico, ESP32, PyBoard, etc.)
- **USB cable** to connect your device to your computer
- **MicroPython firmware** installed on your device

## Installation

### 1. Create a New Project

```bash
dotnet new console -n MyBelayApp
cd MyBelayApp
```

### 2. Add Belay.NET Package

```bash
dotnet add package Belay.NET
```

### 3. Update Program.cs

Replace the contents of `Program.cs`:

```csharp
using Belay.Core;

// Connect to your MicroPython device
using var device = await Device.ConnectAsync("COM3"); // Windows
// using var device = await Device.ConnectAsync("/dev/ttyUSB0"); // Linux
// using var device = await Device.ConnectAsync("/dev/cu.usbmodem*"); // macOS

// Test the connection
await device.ExecuteAsync("print('Hello from MicroPython!')");

// Read some device information
var version = await device.ExecuteAsync<string>("import sys; sys.version");
Console.WriteLine($"MicroPython Version: {version}");

Console.WriteLine("Successfully connected to MicroPython device!");
```

### 4. Run Your Application

```bash
dotnet run
```

If everything is set up correctly, you should see:
```
Hello from MicroPython!
MicroPython Version: 3.4.0; MicroPython v1.20.0 on 2023-04-26
Successfully connected to MicroPython device!
```

## Finding Your Device Port

### Windows
- Open **Device Manager**
- Look under **Ports (COM & LPT)**
- Your device will appear as something like "USB Serial Device (COM3)"

### Linux
- Run `ls /dev/ttyUSB*` or `ls /dev/ttyACM*`
- Your device will typically be `/dev/ttyUSB0` or `/dev/ttyACM0`

### macOS  
- Run `ls /dev/cu.usbmodem*`
- Your device will appear as `/dev/cu.usbmodem[identifier]`

## Your First IoT Application

Let's create a more interesting example that controls an LED:

```csharp
using Belay.Core;
using Belay.Attributes;

public class LedController : Device
{
    [Setup]
    public async Task InitializeLedAsync()
    {
        await ExecuteAsync("""
            from machine import Pin
            led = Pin(25, Pin.OUT)  # Pin 25 for Raspberry Pi Pico
            """);
    }

    [Task]
    public async Task TurnOnAsync()
    {
        await ExecuteAsync("led.on()");
    }

    [Task] 
    public async Task TurnOffAsync()
    {
        await ExecuteAsync("led.off()");
    }

    [Task]
    public async Task BlinkAsync(int times = 3, int delayMs = 500)
    {
        await ExecuteAsync($"""
            import time
            for i in range({times}):
                led.on()
                time.sleep_ms({delayMs})
                led.off() 
                time.sleep_ms({delayMs})
            """);
    }
}

// Usage
var controller = new LedController();
await controller.ConnectAsync("COM3");
await controller.InitializeLedAsync();

await controller.TurnOnAsync();
await Task.Delay(1000);

await controller.BlinkAsync(times: 5, delayMs: 200);

await controller.TurnOffAsync();
```

## Understanding the Code

### Device Connection
```csharp
using var device = await Device.ConnectAsync("COM3");
```
This creates a connection to your MicroPython device using the Raw REPL protocol. The `using` statement ensures proper cleanup.

### Remote Execution
```csharp
await device.ExecuteAsync("print('Hello!')");
```
Executes Python code remotely on your device. The code runs in the device's Python interpreter.

### Type-Safe Returns
```csharp
var temperature = await device.ExecuteAsync<float>("sensor.read_temp()");
```
Execute code and get strongly-typed return values with full compile-time safety.

### Attributes
- `[Setup]` - Runs once when the device is initialized
- `[Task]` - Marks methods that execute remote code
- `[Teardown]` - Runs when the device is disposed (cleanup)
- `[Thread]` - Runs code in a background thread on the device

## Common Issues

### Connection Failures
**Problem**: `DeviceConnectionException: Could not connect to device`

**Solutions**:
- Verify the correct port name
- Ensure MicroPython firmware is installed
- Check that no other applications are using the port
- Try unplugging and reconnecting the device

### Permission Errors (Linux/macOS)
**Problem**: Permission denied accessing `/dev/ttyUSB0`

**Solutions**:
```bash
# Add your user to the dialout group (Linux)
sudo usermod -a -G dialout $USER

# Or run with sudo (not recommended for production)
sudo dotnet run
```

### Import Errors
**Problem**: `ModuleNotFoundError: No module named 'machine'`

**Solution**: Ensure you're running MicroPython, not standard Python. The `machine` module is specific to MicroPython.

## Next Steps

Now that you have Belay.NET working, explore these topics:

- **[Device Communication](./device-communication)** - Learn about the Raw REPL protocol
- **[Attribute Programming](./attributes)** - Master the attribute-based programming model  
- **[Dependency Injection](./dependency-injection)** - Use Belay.NET in larger applications
- **[Examples](../examples/)** - See practical IoT applications
- **[Hardware Guides](../hardware/)** - Device-specific setup instructions

## Getting Help

- **[GitHub Discussions](https://github.com/belay-dotnet/belay/discussions)** - Ask questions
- **[Issue Tracker](https://github.com/belay-dotnet/belay/issues)** - Report bugs
- **[Stack Overflow](https://stackoverflow.com/questions/tagged/belay.net)** - Community help

Welcome to the world of seamless .NET to MicroPython integration! 🚀