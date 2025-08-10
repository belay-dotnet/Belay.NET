# Belay.NET v0.3.0-alpha Release Notes

**Release Date**: January 15, 2025  
**Status**: Alpha Release - Early Access  

## 🎉 Welcome to Belay.NET!

Belay.NET v0.3.0-alpha marks the first public release of our C# port of the Python Belay library, enabling seamless integration between .NET applications and MicroPython/CircuitPython devices.

## ✨ What's New in v0.3.0-alpha

### Core Features

#### 🏷️ **[Task] Attribute System**
Transform your MicroPython device into an extension of your .NET application with our attribute-based programming model:

```csharp
[Task]
public static int ReadTemperature() => """
    import machine
    adc = machine.ADC(4)  # Temperature sensor on Pico
    reading = adc.read_u16() * 3.3 / 65536
    return int(27 - (reading - 0.706) / 0.001721)
    """;

// Use as a regular C# method
int temperature = await device.ReadTemperature();
```

#### 📁 **File Synchronization**
Seamlessly sync files between your development environment and MicroPython devices:

```csharp
await device.Sync.SendFileAsync("config.json", "/config.json");
string content = await device.Sync.ReceiveFileAsync("/logs/sensor.txt");
```

#### 🔗 **Multi-Platform Hardware Support**
Full compatibility across major MicroPython platforms:

| Platform | Connection | Task Attributes | File Transfer | Status |
|----------|------------|-----------------|---------------|---------|
| **ESP32** | ✅ | ✅ | ✅ | ✅ SUPPORTED |
| **Raspberry Pi Pico** | ✅ | ✅ | ✅ | ✅ SUPPORTED |
| **MicroPython Unix Port** | ✅ | ✅ | ✅ | ✅ SUPPORTED |

#### 🧠 **Adaptive Protocol**
Intelligent Raw REPL protocol that auto-detects device capabilities:
- Automatic flow control management
- Device-specific optimization
- USB-CDC and USB-to-serial compatibility
- Robust error handling and recovery

### Developer Experience

#### 🛠️ **Simple Setup**
```csharp
// Connect to any MicroPython device
using var device = Device.FromConnectionString("serial:/dev/ttyACM0");
await device.ConnectAsync();

// Execute Python code directly
var result = await device.ExecuteAsync<string>("import sys; sys.platform");
```

#### 🏗️ **Dependency Injection Ready**
```csharp
services.AddBelay(options => {
    options.DefaultTimeout = TimeSpan.FromSeconds(30);
    options.EnableVerboseLogging = true;
});
```

#### 📊 **Comprehensive Logging**
Built-in structured logging with Microsoft.Extensions.Logging integration for full observability.

## 🚀 Getting Started

### Installation
```bash
# Install from NuGet
dotnet add package Belay.NET --version 0.3.0-alpha

# Or via Package Manager
Install-Package Belay.NET -Version 0.3.0-alpha
```

### Quick Start Example
```csharp
using Belay.Core;

// Connect to your MicroPython device
using var device = Device.FromConnectionString("serial:COM3");
await device.ConnectAsync();

// Execute Python code and get typed results
var platform = await device.ExecuteAsync<string>("import sys; sys.platform");
Console.WriteLine($"Device platform: {platform}");

// Use file synchronization
await device.Sync.SendFileAsync("app.py", "/main.py");
```

## 🔧 Technical Highlights

### Protocol Improvements
- **Fixed Raspberry Pi Pico Compatibility**: Resolved Raw REPL response parsing issues
- **Enhanced Error Handling**: Comprehensive exception mapping and recovery
- **Performance Optimizations**: Reduced latency for device communications

### Code Quality
- **100+ Unit Tests**: Comprehensive test coverage for critical components
- **Principal Code Review**: Underwent rigorous technical review process
- **Protocol Validation**: Hardware-tested against multiple device types

### Architecture
- **Async-First Design**: Full Task-based async patterns throughout
- **Cross-Platform**: .NET 6+ compatibility for Windows, Linux, and macOS
- **Extensible**: Plugin architecture for custom device types and protocols

## 📖 Documentation & Examples

- **Full Documentation**: Available at [https://belay-dotnet.github.io](https://belay-dotnet.github.io)
- **Working Examples**: See `samples/` directory for complete implementations
- **Hardware Guides**: Platform-specific setup instructions
- **API Reference**: Complete API documentation with examples

## ⚠️ Alpha Release Notes

This is an **alpha release** intended for:
- ✅ Early adopters and experimenters
- ✅ Feedback collection and community input
- ✅ Hardware compatibility validation
- ✅ API design validation

**Not recommended for**:
- ❌ Production applications
- ❌ Mission-critical systems
- ❌ Commercial deployments

## 🐛 Known Limitations

1. **Unit Test Coverage**: Some unit tests have nullable reference warnings (non-functional)
2. **Performance**: Method caching not yet implemented (methods re-deploy on each call)
3. **Advanced Features**: Some planned features (proxy objects, advanced sync) not yet available

## 🛣️ Roadmap

### v0.3.1-beta (Next Release)
- **Executor Framework**: Enhanced attribute system with proper interception
- **Session Management**: Improved resource management and multi-device support
- **Method Caching**: Performance optimization for repeated operations
- **Production Readiness**: Enhanced error handling and reliability

### v0.4.0+
- **File Synchronization**: Bidirectional sync and package deployment
- **Proxy Objects**: Advanced compile-time code generation
- **Enterprise Features**: Advanced logging, metrics, and monitoring

## 🤝 Community & Feedback

We're excited to get your feedback on this alpha release!

- **GitHub Issues**: [Report bugs and request features](https://github.com/belay-dotnet/Belay.NET/issues)
- **Discussions**: [Join community discussions](https://github.com/belay-dotnet/Belay.NET/discussions)
- **Documentation**: [Contribute to docs](https://github.com/belay-dotnet/belay-dotnet.github.io)

## 🙏 Acknowledgments

This release represents significant community effort:
- Hardware validation across multiple platforms
- Comprehensive protocol analysis and debugging
- Documentation improvements and examples
- Code quality improvements through review

## 📄 License

Belay.NET is released under the MIT License. See [LICENSE](LICENSE) for details.

---

**Happy coding with Belay.NET!** 🚀

*Transform your MicroPython devices into seamless extensions of your .NET applications.*