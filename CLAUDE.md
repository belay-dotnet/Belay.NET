# CLAUDE.md - Belay.NET Development Guide

This file provides guidance to Claude Code when working on the Belay.NET project - a C# library for seamless integration between .NET applications and MicroPython/CircuitPython devices.

## Project Overview

**Belay.NET** is a C# port of the Python Belay library that enables Windows applications to treat MicroPython devices as off-the-shelf hardware components. The library provides a clean, strongly-typed API for device communication, file synchronization, and remote code execution.

## Key Knowledge Areas

### Raw REPL Protocol Understanding
- **Raw Mode**: Entered via Ctrl-A (0x01), allows programmatic code execution
- **Raw-Paste Mode**: More advanced protocol with flow control for large code transfers
- **Protocol Sequence**: `\x05A\x01` initialization, window-size management, flow control
- **Exit Sequences**: Ctrl-B (0x02) exits raw mode, Ctrl-D (0x04) executes code
- **Implementation**: Must handle flow control bytes `\x01` (increase window) and `\x04` (end data)

### MicroPython Submodule
- Include micropython git submodule for reference and testing
- Unix port can be built for local testing: `./ports/unix/build-standard/micropython`
- Reference documentation: `./docs/reference/repl.rst`
- Used for subprocess-based testing without physical hardware
- **Important**: When working within the micropython submodule directory, reference `./CLAUDE_micropython.md` for MicroPython-specific development guidance

### Architecture Principles
- **Async-First**: All device communication uses Task-based async patterns
- **Strong Typing**: Generic return types with compile-time safety
- **DI Ready**: Full dependency injection support with IServiceCollection extensions
- **Cross-Platform**: .NET 6+ for Windows/Linux/macOS compatibility
- **Enterprise Ready**: Structured logging, configuration, health checks

## Project Structure

```
Belay.NET/
├── src/
│   ├── Belay.Core/                    # Core device communication and management
│   ├── Belay.Attributes/              # Attributes for method decoration
│   ├── Belay.Proxy/                   # Dynamic proxy object system
│   ├── Belay.Sync/                    # File synchronization system
│   ├── Belay.PackageManager/          # NuGet-style package management
│   ├── Belay.CLI/                     # Command-line interface
│   └── Belay.Extensions/              # Extensions for DI and configuration
├── plan/                              # Implementation planning documents
├── samples/                           # Usage examples and demos
├── tests/                            # Unit and integration tests
├── docs/                             # Documentation
└── micropython/                      # Submodule for reference and testing
```

## Implementation Planning

All implementation work is planned using structured documents in the `./plan/` folder:

- **`epic-*.md`**: High-level feature epics with business requirements
- **`issue-*.md`**: Detailed implementation issues with technical specifications  
- **`milestone-*.md`**: Release milestones and delivery planning

### Current Planning Status
- ✅ Technical specification completed
- ✅ Architecture design completed  
- ✅ Core foundation implementation completed
- ✅ Unit test coverage achieved
- 🔄 Hardware validation testing phase
- ⏳ Advanced features development pending

## Development Commands

### Testing with MicroPython Unix Port
```bash
# Build micropython unix port for testing
cd micropython/ports/unix
make submodules
make

# Run tests against unix port
dotnet test --filter "Category=UnixPort"
```

### Hardware Testing
```bash
# Run all hardware validation tests (requires physical devices)
dotnet test --filter "Category=Hardware" --logger console

# Run hardware tests for specific device type
dotnet test --filter "Category=Hardware&Category=RaspberryPi" --logger console

# Run performance benchmarks
dotnet test --filter "Category=Performance" --logger console
```

### Code Quality
```bash
# Format code
dotnet format

# Run analyzers
dotnet build --verbosity normal

# Run security analysis
dotnet list package --vulnerable
```

### Documentation
```bash
# Generate API docs
dotnet build -c Release
docfx docs/docfx.json
```

## Communication Protocols

### Raw REPL Implementation Requirements
1. **State Management**: Track Normal/Raw/RawPaste states
2. **Flow Control**: Handle window size and control bytes properly
3. **Error Handling**: Map device errors to host exceptions with line numbers
4. **Timeouts**: Implement appropriate timeouts for each protocol phase
5. **Reconnection**: Support device reconnection with state restoration

### Connection Types Priority
1. **Serial/USB**: Primary connection method for development boards
2. **Subprocess**: Essential for testing without physical hardware
3. **WebREPL**: Secondary for wireless development (MicroPython only)

## Code Patterns and Conventions

### Async Patterns
```csharp
// Always use ConfigureAwait(false) in library code
await SomeAsync().ConfigureAwait(false);

// Support cancellation tokens throughout
public async Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default)
```

### Error Handling
```csharp
// Custom exceptions for different error types
throw new DeviceConnectionException("Failed to connect to device", innerException);
throw new DeviceExecutionException("Code execution failed on device", deviceStackTrace);
```

### Logging
```csharp
// Use structured logging with Microsoft.Extensions.Logging
_logger.LogDebug("Executing code on device: {Code}", code);
_logger.LogError(ex, "Device communication failed for port {Port}", portName);
```

## Testing Strategy

### Test Categories
- **Unit Tests**: Component isolation with mocks
- **Integration Tests**: Real device communication (requires hardware)
- **Unix Port Tests**: Testing against micropython subprocess
- **Performance Tests**: Throughput and latency measurements

### Test Hardware Requirements
- Raspberry Pi Pico (MicroPython + CircuitPython)
- ESP32 development board
- STM32-based pyboard (optional)

## Release Planning

### Phase 1: Core Foundation
- Device communication layer with raw REPL support
- Basic task execution and attribute system
- Subprocess communication for testing
- Unit test coverage

### Phase 2: Advanced Features  
- File synchronization system
- Proxy object implementation
- Package management foundation
- Integration test suite

### Phase 3: Enterprise Features
- ASP.NET Core integration
- WPF/WinUI examples
- Performance optimization
- Documentation and samples

## Contributing Guidelines

1. **Plan First**: All features must have corresponding planning documents
2. **Test Coverage**: Maintain >80% code coverage
3. **Documentation**: Update CLAUDE.md and planning docs during implementation
4. **Performance**: Profile communication protocols and optimize hot paths
5. **Compatibility**: Ensure cross-platform functionality

## Development Guidance

### Hardware Testing Workflow
After completing the foundation implementation with all unit tests passing, the project transitions to hardware validation:

1. **Hardware Setup**: Install required firmware on test devices (see [Hardware Testing Guide](./docs/hardware-testing-guide.md))
2. **Platform Testing**: Validate implementation on Windows, Linux, and macOS with real hardware
3. **Protocol Validation**: Test Raw REPL protocol implementation with MicroPython and CircuitPython devices
4. **Performance Benchmarking**: Measure communication latency and throughput
5. **Reliability Testing**: Validate error handling, reconnection logic, and edge cases
6. **Integration Validation**: Ensure all implemented features work correctly with physical devices

### Test Failure Resolution
- Whenever there are test failures, ensure they are fixed by improving the core implementation and/or the test implementation the best meet the functional needs of the project, check with the planning subagent for clarification if needed.

## References

- [MicroPython REPL Documentation](https://docs.micropython.org/en/latest/reference/repl.html)
- [Original Python Belay Library](https://github.com/BrianPugh/belay)
- [Technical Specification](./belay_technical_specification.md)
- [Architecture Plan](./belay_csharp_architecture.md)
- [Hardware Testing Guide](./docs/hardware-testing-guide.md)