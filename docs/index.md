# Belay.NET Documentation

Welcome to the **Belay.NET** documentation. Belay.NET is a powerful C# library that enables seamless integration between .NET applications and MicroPython/CircuitPython devices.

## What is Belay.NET?

Belay.NET allows you to treat MicroPython devices as off-the-shelf hardware components by providing:

- **Attribute-based programming model** for device-specific classes
- **Type-safe remote method execution** with compile-time safety
- **Background thread management** for continuous operations
- **Automatic code deployment and caching** for optimal performance
- **Cross-platform compatibility** (Windows, Linux, macOS)

## Quick Start

```csharp
// Define a device-specific class
public class TemperatureSensor : Device
{
    [Setup]
    private async Task InitializeAsync()
    {
        await ExecuteAsync("import machine; sensor = machine.ADC(26)");
    }

    [Task]
    public async Task<float> ReadTemperatureAsync()
    {
        return await ExecuteAsync<float>(@"
            reading = sensor.read_u16()
            voltage = reading * 3.3 / 65535
            temperature = 27 - (voltage - 0.706) / 0.001721
            temperature
        ");
    }
}

// Use the device
var sensor = new TemperatureSensor("serial:COM3");
await sensor.ConnectAsync();
float temperature = await sensor.ReadTemperatureAsync();
Console.WriteLine($"Temperature: {temperature}°C");
```

## Core Features

### 🎯 [Attribute-Based Programming](articles/attributes-reference.md)
Use `[Task]`, `[Setup]`, `[Thread]`, and `[Teardown]` attributes to create sophisticated device interactions with minimal boilerplate.

### 🔧 [Device Subclassing](articles/device-programming.md)  
Create reusable, strongly-typed device classes that encapsulate hardware-specific logic and provide clean APIs.

### 🧵 [Background Threads](articles/attributes-reference.md#thread-attribute)
Run continuous monitoring, data collection, or reactive behavior directly on MicroPython devices.

### ⚡ [Hardware Validated](articles/hardware-testing.md)
Tested and validated with real MicroPython devices including ESP32, Raspberry Pi Pico, and more.

## Getting Started

1. **[Installation](articles/getting-started.md#installation)** - Install Belay.NET via NuGet
2. **[First Device](articles/getting-started.md#your-first-device)** - Create your first device class  
3. **[Hardware Setup](articles/hardware-testing.md)** - Prepare MicroPython devices
4. **[API Reference](api/)** - Explore the complete API

## Hardware Compatibility

Belay.NET works with any device running:

- **MicroPython** v1.20+ (Recommended)
- **CircuitPython** v8.0+ (Partial support)

### Tested Platforms
- ✅ ESP32/ESP32-S3/ESP32-C6 
- ✅ Raspberry Pi Pico/Pico W
- ✅ PyBoard (STM32-based)
- ✅ Unix port (for testing)

## Support and Community

- **Documentation**: [belay-dotnet.github.io](https://belay-dotnet.github.io)
- **Source Code**: [github.com/belay-dotnet/belay](https://github.com/belay-dotnet/belay)  
- **Issues**: [GitHub Issues](https://github.com/belay-dotnet/belay/issues)
- **Discussions**: [GitHub Discussions](https://github.com/belay-dotnet/belay/discussions)

## License

Belay.NET is licensed under the [Apache License 2.0](https://github.com/belay-dotnet/belay/blob/main/LICENSE).