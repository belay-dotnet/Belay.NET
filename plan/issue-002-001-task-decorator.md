# Issue 002-001: Task Decorator Implementation

**Epic**: 002 - Attribute-Based Programming Model  
**Status**: Planned  
**Priority**: Critical  
**Estimated Effort**: 1 week  
**Assignee**: TBD  

## Summary

Implement the `[Task]` attribute and supporting infrastructure that enables type-safe remote method execution on MicroPython devices. This is the foundational component of the attribute-based programming model.

## Acceptance Criteria

### Functional Requirements
- [ ] `TaskAttribute` class with proper attribute targeting
- [ ] Method interception and code deployment pipeline
- [ ] Type-safe parameter marshaling to MicroPython
- [ ] Return value parsing and type conversion
- [ ] Code caching to avoid repeated deployment
- [ ] Integration with existing Device communication layer

### Technical Requirements
- [ ] Support generic return types: `Task<T>`, `Task<(T1, T2)>`, `Task`
- [ ] Handle primitive types: `int`, `float`, `string`, `bool`  
- [ ] Support collections: `List<T>`, `Dictionary<K,V>`, arrays
- [ ] Parameter validation and error handling
- [ ] Async/await pattern throughout the pipeline

### Quality Requirements
- [ ] Unit test coverage >90%
- [ ] Integration tests with real MicroPython device
- [ ] Performance: <20ms overhead vs direct `ExecuteAsync()`
- [ ] Memory efficiency: minimal object allocation per call

## Technical Design

### Core Components

#### TaskAttribute Class
```csharp
namespace Belay.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class TaskAttribute : Attribute
    {
        public bool CacheCode { get; set; } = true;
        public int TimeoutMs { get; set; } = 30000;
    }
}
```

#### Enhanced Device Base Class
```csharp
public abstract class Device : IDisposable
{
    private readonly Dictionary<string, DeployedMethod> _deployedMethods = new();
    private readonly IDeviceCommunication _communication;

    protected async Task<T> InvokeTaskAsync<T>(
        [CallerMemberName] string methodName = null,
        params object[] parameters)
    {
        var method = GetTaskMethod(methodName);
        var code = await DeployMethodAsync(method);
        var result = await ExecuteMethodAsync<T>(code, parameters);
        return result;
    }

    private TaskMethodInfo GetTaskMethod(string methodName)
    {
        var type = GetType();
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        
        if (method?.GetCustomAttribute<TaskAttribute>() == null)
        {
            throw new InvalidOperationException($"Method {methodName} is not decorated with [Task]");
        }

        return new TaskMethodInfo(method);
    }
}
```

#### Method Information and Code Generation
```csharp
internal class TaskMethodInfo
{
    public MethodInfo Method { get; }
    public TaskAttribute Attribute { get; }
    public Type ReturnType { get; }
    public ParameterInfo[] Parameters { get; }
    public string PythonCode { get; private set; }

    public TaskMethodInfo(MethodInfo method)
    {
        Method = method;
        Attribute = method.GetCustomAttribute<TaskAttribute>();
        ReturnType = GetTaskReturnType(method.ReturnType);
        Parameters = method.GetParameters();
        PythonCode = ExtractPythonCode(method);
    }

    private string ExtractPythonCode(MethodInfo method)
    {
        // Extract Python code from method body using Roslyn or reflection
        // This is a key implementation challenge
        throw new NotImplementedException();
    }
}
```

### Code Extraction Strategy

The most challenging aspect is extracting Python code from C# method bodies. Three approaches:

#### Option 1: String-based (Recommended for v1)
```csharp
[Task]
public async Task<float> ReadTemperatureAsync()
{
    return await InvokeTaskAsync<float>(@"
        reading = sensor.read_u16()
        voltage = reading * 3.3 / 65535
        temperature = 27 - (voltage - 0.706) / 0.001721
        return temperature
    ");
}
```

#### Option 2: Attribute-based
```csharp
[Task(@"
    reading = sensor.read_u16()
    voltage = reading * 3.3 / 65535  
    temperature = 27 - (voltage - 0.706) / 0.001721
    return temperature
")]
public async Task<float> ReadTemperatureAsync() { }
```

#### Option 3: Source generation (Future enhancement)
```csharp
[Task]
public async Task<float> ReadTemperatureAsync()
{
    // Source generator converts this to Python
    var reading = sensor.read_u16();
    var voltage = reading * 3.3f / 65535f;
    var temperature = 27 - (voltage - 0.706f) / 0.001721f;
    return temperature;
}
```

## Implementation Plan

### Phase 1: Basic Infrastructure (2-3 days)
- Create `TaskAttribute` class
- Implement method reflection and validation
- Basic parameter marshaling for primitive types
- Simple return value parsing

### Phase 2: Code Deployment (2-3 days)
- Code extraction from method bodies
- Caching mechanism for deployed code
- Integration with `IDeviceCommunication`
- Error handling and validation

### Phase 3: Type System (1-2 days)
- Support for complex return types (tuples, collections)
- Parameter validation and conversion
- Generic type handling

### Phase 4: Testing and Polish (1-2 days)
- Comprehensive unit test suite
- Integration testing with real devices
- Performance optimization
- Documentation and examples

## Code Examples

### Basic Usage
```csharp
public class TemperatureSensor : Device
{
    [Task]
    public async Task<float> ReadTemperatureAsync()
    {
        return await InvokeTaskAsync<float>(@"
            import machine
            sensor = machine.ADC(machine.Pin(26))
            reading = sensor.read_u16()
            voltage = reading * 3.3 / 65535
            temperature = 27 - (voltage - 0.706) / 0.001721
            return temperature
        ");
    }

    [Task]
    public async Task<(float temp, float humidity)> ReadSensorDataAsync()
    {
        return await InvokeTaskAsync<(float, float)>(@"
            temp = read_temperature()
            humidity = read_humidity() 
            return (temp, humidity)
        ");
    }
}

// Usage
var sensor = new TemperatureSensor("serial:COM3");
await sensor.ConnectAsync();

float temperature = await sensor.ReadTemperatureAsync();
var (temp, humidity) = await sensor.ReadSensorDataAsync();
```

### With Parameters
```csharp
public class ServoController : Device  
{
    [Task]
    public async Task SetAngleAsync(int angle)
    {
        await InvokeTaskAsync($@"
            import machine
            servo = machine.PWM(machine.Pin(0))
            servo.freq(50)
            duty = int(40 + (angle / 180) * 115)
            servo.duty_u16(duty * 655)
        ", angle);
    }

    [Task]
    public async Task<int> GetCurrentAngleAsync()
    {
        return await InvokeTaskAsync<int>(@"
            # Read current servo position
            return servo.current_angle
        ");
    }
}
```

## Testing Strategy

### Unit Tests
- `TaskAttribute` metadata validation
- Method reflection and discovery
- Parameter marshaling for all supported types
- Return value parsing and conversion
- Code caching behavior
- Error condition handling

### Integration Tests  
- End-to-end method execution on real devices
- Performance benchmarking vs direct execution
- Memory usage validation
- Error propagation from device to host
- Long-running stability testing

### Test Cases
```csharp
[TestClass]
public class TaskDecoratorTests
{
    [TestMethod]
    public async Task TaskMethod_WithPrimitiveReturnType_ReturnsCorrectValue()
    {
        var device = new TestDevice("subprocess");
        await device.ConnectAsync();
        
        var result = await device.TestMethod();
        
        Assert.AreEqual(42, result);
    }

    [TestMethod]
    public async Task TaskMethod_WithParameters_PassesCorrectly()
    {
        var device = new TestDevice("subprocess");
        await device.ConnectAsync();
        
        var result = await device.AddNumbers(5, 7);
        
        Assert.AreEqual(12, result);
    }

    [TestMethod]
    public async Task TaskMethod_WithTupleReturn_DeserializesCorrectly()
    {
        var device = new TestDevice("subprocess");
        await device.ConnectAsync();
        
        var (x, y) = await device.GetCoordinates();
        
        Assert.AreEqual(10.5f, x);
        Assert.AreEqual(20.3f, y);
    }
}
```

## Dependencies

### Internal Dependencies
- `Belay.Core` - `IDeviceCommunication` interface
- Enhanced `Device` base class from Epic 001
- Error handling infrastructure

### External Dependencies
- `System.Reflection` - Method metadata and attribute processing
- `System.Runtime.CompilerServices` - `[CallerMemberName]` support
- `Newtonsoft.Json` or `System.Text.Json` - Response parsing

## Risks and Mitigation

### High Risk
- **Code extraction complexity**: Different approaches have trade-offs
  - *Mitigation*: Start with string-based approach, plan for future enhancements
- **Type system complexity**: Supporting all .NET types in Python
  - *Mitigation*: Start with primitive types, expand incrementally

### Medium Risk  
- **Performance overhead**: Method interception and code deployment
  - *Mitigation*: Implement caching, profile early and optimize
- **Error handling**: Complex stack traces across boundaries
  - *Mitigation*: Systematic error mapping, preserve context

## Success Metrics

- [ ] All acceptance criteria met
- [ ] Unit test coverage >90%
- [ ] Integration tests passing on Raspberry Pi Pico
- [ ] Performance overhead <20ms vs direct execution
- [ ] Developer feedback positive on API usability

## Future Enhancements

- Source generation for C# to Python conversion
- Advanced parameter types (custom objects, delegates)
- Async enumerable support for streaming results
- Code minification and optimization
- IntelliSense support for embedded Python code