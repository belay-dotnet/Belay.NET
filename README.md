# Belay.NET

A C# library for seamless integration between .NET applications and MicroPython/CircuitPython devices. Belay.NET enables Windows applications to treat MicroPython devices as off-the-shelf hardware components, providing physical capabilities through a clean, strongly-typed API.

## 🚀 Project Status

**Current Phase**: Foundation Development (v0.1.0)  
**Development Stage**: Initial Implementation  
**Next Milestone**: Raw REPL Protocol Completion

### ✅ Completed
- Project structure and solution setup
- MicroPython submodule integration
- Docker-based development environment
- Raw REPL protocol foundation
- Basic test infrastructure
- Comprehensive planning documentation

### 🔄 In Progress
- Issue 001-001: Raw REPL Protocol Implementation
- Serial device communication layer
- Subprocess communication for testing

### 📋 Planned
- Device factory and connection management
- File synchronization system
- Attribute-based programming model
- Package management system

## 🛠 Development Setup

### Prerequisites
- Docker and Docker Compose
- Git with submodules support
- Linux/macOS development environment

### Quick Start

```bash
# Clone repository with submodules
git clone --recurse-submodules <repository-url>
cd belay_c#

# Start development environment
./dev.sh shell

# Build and test
./dev.sh build
./dev.sh test

# Build MicroPython unix port for testing
./dev.sh build-micropython
```

### Development Commands

| Command | Description |
|---------|-------------|
| `./dev.sh shell` | Start interactive development container |
| `./dev.sh build` | Build the solution |
| `./dev.sh test` | Run all tests |
| `./dev.sh dotnet <args>` | Run dotnet command in container |
| `./dev.sh build-micropython` | Build MicroPython unix port |

## 🏗 Architecture Overview

### Core Components

#### **Belay.Core**
- `IDeviceCommunication` - Device communication abstraction
- `RawReplProtocol` - MicroPython Raw REPL protocol implementation
- `Device` - High-level device management class

#### **Belay.Tests.Unit**
- Comprehensive unit tests with >90% coverage target
- Mock-based testing for protocol validation
- Cross-platform compatibility tests

### Communication Protocols

#### Raw REPL Protocol
- **Raw Mode**: Basic programmatic code execution (Ctrl-A → code → Ctrl-D)
- **Raw-Paste Mode**: Advanced flow-controlled code transmission
- **State Management**: Proper state transitions and error handling
- **Flow Control**: Window-based data transmission for large code blocks

#### Supported Connection Types
1. **Serial/USB** - Primary connection method for development boards
2. **Subprocess** - MicroPython unix port for testing without hardware
3. **WebREPL** - Wireless connections (planned for v0.2.0)

## 📖 Documentation

- **[Technical Specification](./belay_technical_specification.md)** - Complete analysis of original Python Belay
- **[Architecture Plan](./belay_csharp_architecture.md)** - Detailed C# implementation design
- **[Development Guide](./CLAUDE.md)** - Development practices and knowledge base
- **[Implementation Plans](./plan/)** - Structured epics, issues, and milestones

## 🧪 Testing Strategy

### Test Categories
- **Unit Tests**: Component isolation with comprehensive mocking
- **Integration Tests**: Real device communication validation
- **Subprocess Tests**: Hardware-independent testing via MicroPython unix port
- **Performance Tests**: Communication overhead and throughput validation

### Test Hardware
- Raspberry Pi Pico (MicroPython + CircuitPython)
- ESP32 development boards
- MicroPython unix port (software-based testing)

## 🎯 Usage Examples

### Basic Device Connection
```csharp
// Connect to device
var device = new Device("serial:COM3");
await device.ConnectAsync();

// Execute Python code
var result = await device.ExecuteAsync<int>("2 + 2");
Console.WriteLine($"Result: {result}"); // Result: 4

// Cleanup
await device.DisconnectAsync();
```

### Advanced Device Subclassing (Planned)
```csharp
public class MyIoTDevice : Device
{
    [Setup(AutoInit = true)]
    public async Task InitializeHardware() { /* Setup code */ }
    
    [Task]
    public async Task<bool> SetLedAsync(bool state) { /* LED control */ }
    
    [Task] 
    public async Task<double> ReadTemperatureAsync() { /* Sensor reading */ }
}
```

## 🗂 Project Structure

```
Belay.NET/
├── src/
│   ├── Belay.Core/                 # ✅ Core device communication
│   ├── Belay.Attributes/           # 📋 Method decoration attributes
│   ├── Belay.Proxy/               # 📋 Dynamic proxy objects
│   ├── Belay.Sync/                # 📋 File synchronization
│   ├── Belay.PackageManager/      # 📋 NuGet-style package management
│   ├── Belay.CLI/                 # 📋 Command-line interface
│   └── Belay.Extensions/          # 📋 DI and configuration extensions
├── tests/
│   ├── Belay.Tests.Unit/          # ✅ Unit tests
│   ├── Belay.Tests.Integration/   # 📋 Integration tests
│   └── Belay.Tests.Subprocess/    # 📋 Subprocess-based tests
├── plan/                          # ✅ Implementation planning
├── samples/                       # 📋 Usage examples
├── docs/                          # 📋 Documentation
└── micropython/                   # ✅ MicroPython reference submodule
```

Legend: ✅ Implemented | 🔄 In Progress | 📋 Planned

## 🤝 Contributing

This project follows structured development practices:

1. **Plan First**: All features require corresponding planning documents in `./plan/`
2. **Test Coverage**: Maintain >90% unit test coverage
3. **Documentation**: Update `CLAUDE.md` and planning docs during implementation
4. **Performance**: Profile communication protocols and optimize hot paths
5. **Cross-Platform**: Ensure compatibility across Windows, Linux, and macOS

## 📄 License

Apache 2.0 License - see LICENSE file for details.

## 🙏 Acknowledgments

- Original [Python Belay Library](https://github.com/BrianPugh/belay) by Brian Pugh
- [MicroPython Project](https://github.com/micropython/micropython) for the excellent embedded Python implementation
- .NET Foundation for the robust development platform

---

**Development Status**: This project is under active development. The foundation is being built with production-quality architecture and comprehensive testing. Contributions and feedback are welcome!