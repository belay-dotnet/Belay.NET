# Getting Started with Belay.NET

This guide will help you get up and running with Belay.NET quickly.

## Installation

### NuGet Package Manager

```bash
dotnet add package Belay.Core
dotnet add package Belay.Attributes
```

### Package Manager Console (Visual Studio)

```powershell
Install-Package Belay.Core
Install-Package Belay.Attributes
```

## Prerequisites

- **.NET 8.0** or later
- **MicroPython device** (ESP32, Raspberry Pi Pico, etc.)
- **USB cable** for serial communication

## Your First Device

Let's create a simple LED controller to demonstrate the Belay.NET programming model:

### 1. Define the Device Class

```csharp
using Belay.Attributes;
using Belay.Core;

public class SimpleLED : IDisposable
{
    private readonly Device _device;

    public SimpleLED(string connectionString)
    {
        _device = Device.FromConnectionString(connectionString);
    }

    [Setup]
    private async Task InitializeLEDAsync()
    {
        await _device.ExecuteAsync(@"
            import machine
            led = machine.Pin(2, machine.Pin.OUT)
            led_state = False
        ");
    }

    [Task]
    public async Task TurnOnAsync()
    {
        await _device.ExecuteAsync(@"
            led.on()
            led_state = True
            print('LED turned on')
        ");
    }

    [Task] 
    public async Task TurnOffAsync()
    {
        await _device.ExecuteAsync(@"
            led.off()
            led_state = False  
            print('LED turned off')
        ");
    }

    [Task]
    public async Task<bool> GetStateAsync()
    {
        return await _device.ExecuteAsync<bool>("led_state");
    }

    [Teardown]
    private async Task CleanupAsync()
    {
        await _device.ExecuteAsync("led.off()");
    }

    public async Task ConnectAsync() => await _device.ConnectAsync();
    public async Task DisconnectAsync() => await _device.DisconnectAsync();
    public void Dispose() => _device?.Dispose();
}
```

### 2. Use the Device

```csharp
// Connect to device (adjust port for your system)
var led = new SimpleLED("serial:COM3");  // Windows
// var led = new SimpleLED("serial:/dev/ttyACM0");  // Linux  
// var led = new SimpleLED("serial:/dev/cu.usbmodem*");  // macOS

try
{
    await led.ConnectAsync();
    
    // Control the LED
    await led.TurnOnAsync();
    await Task.Delay(1000);
    
    bool isOn = await led.GetStateAsync();
    Console.WriteLine($"LED is {(isOn ? "on" : "off")}");
    
    await led.TurnOffAsync();
}
finally
{
    await led.DisconnectAsync();
    led.Dispose();
}
```

## Understanding the Attributes

### `[Setup]`
- Runs once when the device connects
- Use for hardware initialization and global setup
- Multiple setup methods run in order

### `[Task]`  
- Defines methods that execute on the device
- Can return typed values
- Supports both sync and async execution patterns

### `[Teardown]`
- Runs during disconnection
- Use for cleanup and safe shutdown
- Always executes, even if errors occur

## Device Discovery

Belay.NET can automatically discover connected MicroPython devices:

```csharp
// Find all MicroPython devices
var devices = await Device.DiscoverDevicesAsync();
foreach (var device in devices)
{
    Console.WriteLine($"Found device: {device.PortName} - {device.Description}");
}

// Connect to first available device
var firstDevice = await Device.DiscoverFirstAsync();
if (firstDevice != null)
{
    await firstDevice.ConnectAsync();
    // Use device...
}
```

## Connection Strings

Belay.NET supports multiple connection types:

```csharp
// Serial connections
"serial:COM3"                           // Windows  
"serial:/dev/ttyACM0"                   // Linux
"serial:/dev/cu.usbmodem143201"         // macOS
"serial:COM3?baudrate=115200"           // With custom baud rate

// Subprocess (for testing)
"subprocess:micropython"                // Local MicroPython subprocess
"subprocess:/path/to/micropython"      // Custom MicroPython path
```

## Error Handling

```csharp
try
{
    var device = Device.FromConnectionString("serial:COM3");
    await device.ConnectAsync();
    
    var result = await device.ExecuteAsync<int>("2 + 2");
    Console.WriteLine($"Result: {result}");
}
catch (DeviceConnectionException ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
}
catch (DeviceExecutionException ex)
{
    Console.WriteLine($"Execution failed: {ex.Message}");
    Console.WriteLine($"Device traceback: {ex.DeviceTraceback}");
}
```

## Next Steps

- **[Device Programming Guide](device-programming.md)** - Learn advanced device patterns
- **[Attributes Reference](attributes-reference.md)** - Complete attribute documentation  
- **[Hardware Testing](hardware-testing.md)** - Set up and test with real devices
- **[API Reference](../api/)** - Explore the complete API

## Common Issues

### Device Not Found
- Ensure the MicroPython device is connected via USB
- Check that the correct serial port is specified
- Verify device drivers are installed (Windows)

### Permission Denied (Linux/macOS)
```bash
# Add user to dialout group (Linux)
sudo usermod -a -G dialout $USER
# Logout and login again

# Or set port permissions temporarily  
sudo chmod 666 /dev/ttyACM0
```

### Connection Timeout
- Verify the device is running MicroPython (not bootloader mode)
- Try a different baud rate: `"serial:COM3?baudrate=9600"`  
- Check USB cable supports data transfer (not power-only)

### Import Errors
Ensure you have the latest NuGet packages:
```bash
dotnet add package Belay.Core --version latest
dotnet add package Belay.Attributes --version latest
```