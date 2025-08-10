<div align="center">
  <img src="belay_net_logo.svg" alt="Belay.NET Logo" width="200" height="200">
  
# Belay.NET

**Control MicroPython devices from .NET with zero friction.**

[📖 Documentation](https://belay-dotnet.github.io) | [🚀 Quick Start](#quick-start) | [💡 Examples](#examples) | [📋 API Reference](https://belay-dotnet.github.io/api)

---

## What is Belay.NET?

Belay.NET transforms MicroPython devices into native .NET components. Write C# code that seamlessly executes on microcontrollers, with full IntelliSense, type safety, and async/await support. No more context switching between languages—control sensors, actuators, and IoT devices directly from your .NET applications.

## ✨ Features

- 🚀 **Zero Configuration** - Connect and control devices in seconds
- 🎯 **Type-Safe** - Full IntelliSense and compile-time safety for remote code
- ⚡ **Async First** - Modern async/await patterns throughout the API
- 🏷️ **Attribute-Based** - Decorate methods to run seamlessly on devices
- 📦 **Dependency Injection** - First-class DI support with Microsoft.Extensions
- 🔧 **File Synchronization** - Keep device files in sync with your project
- 🌍 **Cross-Platform** - Windows, Linux, macOS support
- 🔍 **Health Monitoring** - Built-in health checks and diagnostics

## Quick Start

Get up and running in 30 seconds:

### 1. Install the Package
```bash
dotnet add package Belay.NET
```

### 2. Connect and Execute
```csharp
using Belay.Core;

// Connect to your MicroPython device
using var device = await Device.ConnectAsync("COM3"); // or "/dev/ttyUSB0" on Linux

// Execute Python code remotely with full type safety
var temperature = await device.ExecuteAsync<float>("machine.ADC(4).read_u16() * 3.3 / 65536");
Console.WriteLine($"Temperature: {temperature}°C");
```

### 3. Use Attribute-Based Programming
```csharp
public class SmartSensor : Device
{
    [Task]
    public async Task<float> ReadTemperatureAsync() =>
        await ExecuteAsync<float>("sensor.read_temp()");
        
    [Task(Cache = true, TimeoutMs = 5000)]
    public async Task<string> GetDeviceInfoAsync() =>
        await ExecuteAsync<string>("sys.version");
        
    [Setup]
    public async Task InitializeSensorAsync() =>
        await ExecuteAsync("sensor.init(pin=4)");
}

// Use your device like any other .NET class
var sensor = new SmartSensor();
await sensor.ConnectAsync("COM3");
await sensor.InitializeSensorAsync();

var temp = await sensor.ReadTemperatureAsync();
Console.WriteLine($"Current temperature: {temp}°C");
```

## Supported Hardware

| Device | Connection | Status | Guide |
|--------|------------|--------|-------|
| Raspberry Pi Pico | USB Serial | ✅ Fully Supported | [→ Guide](https://belay-dotnet.github.io/hardware/raspberry-pi-pico) |
| ESP32 | USB Serial / WiFi | ✅ Fully Supported | [→ Guide](https://belay-dotnet.github.io/hardware/esp32) |
| PyBoard | USB Serial | ✅ Fully Supported | [→ Guide](https://belay-dotnet.github.io/hardware/pyboard) |
| CircuitPython Devices | USB Serial | 🧪 Beta Support | [→ Guide](https://belay-dotnet.github.io/hardware/circuitpython) |

[View full compatibility matrix →](https://belay-dotnet.github.io/hardware/compatibility)

## Examples

### Basic Device Control
```csharp
using var device = await Device.ConnectAsync("COM3");

// Control built-in LED
await device.ExecuteAsync("from machine import Pin; led = Pin(25, Pin.OUT)");
await device.ExecuteAsync("led.on()");
await Task.Delay(1000);
await device.ExecuteAsync("led.off()");
```

### Dependency Injection
```csharp
// Startup.cs or Program.cs
services.AddBelay(config => {
    config.Device.DefaultConnectionTimeoutMs = 5000;
    config.Communication.Serial.DefaultBaudRate = 115200;
});

// Use in controllers or services
public class IoTController : ControllerBase
{
    private readonly IDeviceFactory _deviceFactory;
    
    public IoTController(IDeviceFactory deviceFactory) =>
        _deviceFactory = deviceFactory;
        
    [HttpGet("temperature")]
    public async Task<float> GetTemperature()
    {
        using var device = _deviceFactory.CreateSerialDevice("COM3");
        await device.ConnectAsync();
        return await device.ExecuteAsync<float>("sensor.read_temp()");
    }
}
```

### File Synchronization
```csharp
// Keep device files synchronized with your project
await device.Sync.PushFileAsync("./sensors/temperature.py", "/lib/temperature.py");
await device.ExecuteAsync("import temperature; temp = temperature.read()");
```

[Browse more examples →](https://belay-dotnet.github.io/examples)

## Learn More

- 📖 **[Documentation](https://belay-dotnet.github.io)** - Complete guides and tutorials
- 🏁 **[Getting Started](https://belay-dotnet.github.io/guide/getting-started)** - Detailed setup guide
- 📋 **[API Reference](https://belay-dotnet.github.io/api)** - Complete API documentation
- 🔧 **[Hardware Guides](https://belay-dotnet.github.io/hardware)** - Device-specific setup instructions
- 🍳 **[Recipes](https://belay-dotnet.github.io/recipes)** - Real-world application examples

## Community & Support

- 💬 **[GitHub Discussions](https://github.com/belay-dotnet/Belay.NET/discussions)** - Ask questions and share ideas
- 🐛 **[Issue Tracker](https://github.com/belay-dotnet/Belay.NET/issues)** - Report bugs and request features
- 📚 **[Stack Overflow](https://stackoverflow.com/questions/tagged/belay.net)** - Get help from the community

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Original [Python Belay Library](https://github.com/BrianPugh/belay) by Brian Pugh
- [MicroPython Project](https://micropython.org/) for the excellent embedded Python implementation
- The .NET Foundation for providing the development platform

---

<details>
<summary><strong>📚 For Contributors & Developers</strong></summary>

## Project Status

**Current Version**: v0.2.0  
**Development Stage**: Architectural Improvements  
**Next Milestone**: Method Deployment Caching Infrastructure

### ✅ Completed Features
- Raw REPL Protocol implementation
- Device communication layer (Serial, Subprocess)
- Attribute-based programming model ([Task], [Setup], [Teardown], [Thread])
- Session management system
- Comprehensive exception handling
- Dependency injection infrastructure
- Health checks and monitoring
- Configuration management

### 🔄 In Progress
- Method deployment caching
- Cross-component integration layer
- Performance monitoring infrastructure

### 📋 Planned Features
- WebREPL support for wireless connections
- File synchronization system
- Package management (NuGet-style for MicroPython)
- Advanced logging and telemetry
- Visual Studio Code extension

## Development Setup

### Prerequisites
- .NET 8.0 SDK or later
- Git with submodules support
- MicroPython device or unix port for testing

### Quick Setup
```bash
# Clone with submodules
git clone --recurse-submodules https://github.com/belay-dotnet/Belay.NET
cd belay

# Setup development environment (includes git hooks)
make setup

# Or manually install git hooks for documentation validation
./scripts/install-hooks.sh

# Build the solution
dotnet build

# Run tests
dotnet test

# Build MicroPython unix port for testing (Linux/macOS)
cd micropython/ports/unix
make submodules
make
```

### Architecture Overview

```
src/
├── Belay.Core/                 # Core device communication and protocols
├── Belay.Attributes/           # Method decoration attributes  
├── Belay.Extensions/           # Dependency injection and configuration
├── Belay.Proxy/               # Dynamic proxy objects (planned)
├── Belay.Sync/                # File synchronization (planned)
├── Belay.PackageManager/      # Package management (planned)
└── Belay.CLI/                 # Command-line tools (planned)
```

### Communication Architecture

#### Raw REPL Protocol
- **Raw Mode**: Basic programmatic code execution (Ctrl-A → code → Ctrl-D)
- **Raw-Paste Mode**: Advanced flow-controlled code transmission for large blocks
- **State Management**: Proper state transitions and error handling
- **Flow Control**: Window-based data transmission prevents buffer overflows

#### Supported Connection Types
1. **Serial/USB**: Primary connection method for development boards
2. **Subprocess**: MicroPython unix port for hardware-independent testing
3. **WebREPL**: Wireless connections (planned for v0.3.0)

### Contributing Guidelines

1. **Plan First**: All features require corresponding planning documents in `./plan/`
2. **Test Coverage**: Maintain >80% code coverage with comprehensive tests
3. **Documentation**: Update documentation and examples with new features
4. **Performance**: Profile communication protocols and optimize critical paths
5. **Cross-Platform**: Ensure compatibility across Windows, Linux, and macOS

### Testing Strategy

#### Test Categories
- **Unit Tests**: Component isolation with comprehensive mocking
- **Integration Tests**: Real device communication validation  
- **Subprocess Tests**: Hardware-independent testing via MicroPython unix port
- **Performance Tests**: Communication overhead and throughput validation

#### Test Hardware
- Raspberry Pi Pico (MicroPython + CircuitPython)
- ESP32 development boards (various models)
- MicroPython unix port (software-based testing)

### Development Workflow

This project uses a structured agent-based development process:

1. **Sprint Planning**: Architecture agent determines next priorities
2. **Implementation**: Feature development with comprehensive testing
3. **Quality Assurance**: Build-test-commit workflow ensures code quality
4. **Code Review**: Principal code reviewer validates all changes
5. **Integration**: Cross-component integration testing

For detailed development practices, see [CLAUDE.md](CLAUDE.md).

</details>