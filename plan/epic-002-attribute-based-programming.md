# Epic 002: Attribute-Based Programming Model

**Status**: Planning  
**Priority**: High  
**Estimated Effort**: 4-5 weeks  
**Dependencies**: Epic 001 (Device Communication Foundation)

## Summary

Implement the decorator-based programming model that enables seamless integration between .NET host applications and MicroPython devices. This epic delivers the core value proposition of Belay.NET - treating MicroPython devices as off-the-shelf hardware components through strongly-typed method decorations.

## Business Value

- Enables intuitive device programming through familiar C# patterns
- Provides strongly-typed remote method execution with compile-time safety
- Eliminates boilerplate device communication code for developers
- Supports advanced patterns like background threads and cleanup handlers
- Creates foundation for device subclassing and specialized hardware abstractions

## Success Criteria

- [ ] Implement `[Task]`, `[Setup]`, `[Thread]`, `[Teardown]` attributes
- [ ] Support device subclassing with automatic method deployment
- [ ] Achieve compile-time type safety for remote method calls
- [ ] Enable background thread execution on MicroPython devices
- [ ] Provide clean teardown and resource management
- [ ] Maintain >95% reliability for decorated method execution

## Technical Scope

### In Scope
- Attribute-based method decoration system
- Device proxy object generation and management
- Code deployment and caching strategies
- Background thread management for MicroPython
- Global context setup and teardown handling
- Type-safe parameter marshaling and return value handling
- Device subclass pattern implementation

### Out of Scope  
- File synchronization features (Epic 003)
- Package management (Epic 004)
- WebREPL communication (deferred from Epic 001)
- Advanced code minification and optimization
- Multi-device coordination and orchestration

## Architecture Impact

### New Components
- `Belay.Attributes` assembly with decoration attributes
- `Belay.Proxy` assembly with dynamic proxy generation
- Enhanced `Device` base class supporting subclassing
- Code deployment and caching system
- Thread management for background execution

### Key Interfaces
```csharp
// Method decoration attributes
[AttributeUsage(AttributeTargets.Method)]
public class TaskAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class SetupAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class ThreadAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class TeardownAttribute : Attribute { }

// Device subclassing support
public abstract class Device : IDisposable
{
    protected abstract Task InitializeAsync();
    public async Task<T> InvokeTaskAsync<T>(string methodName, params object[] args);
    public async Task StartThreadAsync(string methodName, params object[] args);
}
```

## Breaking Down into Issues

### Core Implementation Issues
- **Issue 002-001**: Task Decorator Implementation
- **Issue 002-002**: Setup Decorator and Global Context Management  
- **Issue 002-003**: Thread Decorator and Background Execution
- **Issue 002-004**: Teardown Decorator and Cleanup Management
- **Issue 002-005**: Device Subclassing and Proxy System

### Integration Issues
- **Issue 002-006**: Type System and Parameter Marshaling
- **Issue 002-007**: Code Deployment and Caching Strategy
- **Issue 002-008**: Error Propagation and Exception Mapping
- **Issue 002-009**: Threading Model and Synchronization
- **Issue 002-010**: Comprehensive Integration Testing

## Example Usage Patterns

### Basic Device Subclass
```csharp
public class TemperatureSensor : Device
{
    public TemperatureSensor(string connectionString) 
        : base(connectionString) { }

    [Setup]
    private async Task InitializeSensor()
    {
        // Runs once on device connection
        await ExecuteAsync(@"
import machine
import time
sensor = machine.ADC(machine.Pin(26))
");
    }

    [Task]
    public async Task<float> ReadTemperatureAsync()
    {
        // Type-safe remote method execution
        return await ExecuteAsync<float>(@"
reading = sensor.read_u16()
voltage = reading * 3.3 / 65535
temperature = 27 - (voltage - 0.706) / 0.001721
return temperature
");
    }

    [Thread]
    public async Task StartContinuousLoggingAsync(int intervalMs)
    {
        // Background thread on device
        await ExecuteAsync($@"
import _thread
def log_temperature():
    while True:
        temp = read_temperature_internal()
        print(f'Temperature: {{temp}}C')
        time.sleep_ms({intervalMs})
_thread.start_new_thread(log_temperature, ())
");
    }

    [Teardown]
    private async Task CleanupSensor()
    {
        // Cleanup when device disconnects
        await ExecuteAsync("sensor.deinit()");
    }
}

// Usage
var sensor = new TemperatureSensor("serial:COM3");
await sensor.ConnectAsync();

float temp = await sensor.ReadTemperatureAsync();
await sensor.StartContinuousLoggingAsync(1000);

await sensor.DisconnectAsync(); // Automatic teardown
```

### Advanced Device Integration
```csharp
public class RobotController : Device
{
    [Setup]
    private async Task InitializeRobot() { /* ... */ }

    [Task]
    public async Task<(float x, float y)> GetPositionAsync()
    {
        // Tuple return types supported
        return await ExecuteAsync<(float, float)>(@"
return (robot.x, robot.y)
");
    }

    [Task]
    public async Task MoveToAsync(float x, float y, float speed = 1.0f)
    {
        // Optional parameters with defaults
        await ExecuteAsync($@"
robot.move_to({x}, {y}, {speed})
");
    }

    [Thread]
    public async Task StartObstacleAvoidanceAsync()
    {
        // Complex background behavior
        await ExecuteAsync(@"
def avoid_obstacles():
    while robot.enabled:
        if sensor.detect_obstacle():
            robot.backup()
            robot.turn(45)
        time.sleep_ms(100)
_thread.start_new_thread(avoid_obstacles, ())
");
    }
}
```

## Risk Assessment

### High Risk Items
- **Proxy Generation Complexity**: Dynamic proxy creation may have performance implications
  - *Mitigation*: Use compile-time code generation where possible, profile thoroughly
- **Thread Management**: Background thread lifecycle on MicroPython devices
  - *Mitigation*: Implement robust thread tracking and cleanup, extensive testing
- **Type Safety**: Maintaining compile-time safety for remote execution
  - *Mitigation*: Strong attribute validation, comprehensive type mapping system

### Medium Risk Items  
- **Code Deployment Performance**: Repeated code deployment may impact performance
  - *Mitigation*: Implement intelligent caching, measure deployment overhead
- **Error Handling**: Complex exception propagation from device to host
  - *Mitigation*: Systematic error mapping, preserve stack traces where possible

## Testing Strategy

### Unit Testing
- Attribute processing and validation
- Proxy object generation and behavior
- Code deployment and caching logic
- Type marshaling and conversion
- Thread management state machines

### Integration Testing
- End-to-end decorated method execution
- Device subclass lifecycle management
- Background thread behavior validation
- Error propagation and handling
- Performance benchmarking for method calls

### Test Hardware Requirements
- Raspberry Pi Pico (MicroPython threading support)
- ESP32 (CircuitPython compatibility testing)
- Long-running stability tests with background threads

## Implementation Timeline

### Week 1: Attribute Foundation
- Implement core attribute classes
- Basic attribute processing pipeline
- Simple task method execution
- Unit test framework for attributes

### Week 2: Device Subclassing
- Enhanced Device base class
- Proxy object generation
- Code deployment and caching
- Setup/Teardown lifecycle management

### Week 3: Advanced Features
- Thread decorator implementation
- Background thread management
- Type system enhancements
- Error propagation improvements

### Week 4: Integration & Polish
- End-to-end integration testing
- Performance optimization
- Documentation and examples
- Cross-platform validation

## Performance Targets

### Method Execution
- **Decorated method overhead**: <20ms additional latency vs direct execution
- **Code deployment time**: <100ms for typical method (with caching)
- **Memory usage**: <2MB additional overhead for proxy objects

### Threading
- **Thread startup time**: <50ms for background thread creation
- **Thread management overhead**: <5% CPU impact on device
- **Concurrent thread limit**: Support 4+ background threads on Pico

## Acceptance Criteria

### Functional Criteria
1. **Method Decoration**: All four attribute types work correctly
2. **Type Safety**: Compile-time validation for method signatures  
3. **Device Subclassing**: Clean inheritance pattern with automatic setup
4. **Background Threads**: Reliable thread execution and management
5. **Resource Management**: Proper cleanup on disconnection

### Non-Functional Criteria
1. **Performance**: Method call overhead <20ms
2. **Reliability**: >95% success rate for decorated method calls
3. **Memory Efficiency**: <10MB total memory usage for typical scenarios
4. **Developer Experience**: IntelliSense support, clear error messages

## Definition of Done

- [ ] All attribute types implemented and tested
- [ ] Device subclassing pattern working end-to-end
- [ ] Type-safe parameter marshaling complete
- [ ] Background thread management operational
- [ ] Comprehensive test suite covering all scenarios
- [ ] Performance benchmarks meet targets
- [ ] Documentation and examples complete
- [ ] Cross-platform compatibility verified

## Next Steps

Upon completion of this epic:
1. Epic 003: File Synchronization System
2. Epic 004: Package Management Foundation  
3. Epic 005: Advanced Communication Features

This epic transforms Belay.NET from a low-level communication library into a high-level device programming framework, delivering the core value proposition that differentiates it from generic serial communication tools.