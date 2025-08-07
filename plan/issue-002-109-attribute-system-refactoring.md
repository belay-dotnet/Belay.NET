# Issue 002-109: Attribute System Refactoring

**Status**: Not Started  
**Priority**: HIGH  
**Estimated Effort**: 3 days  
**Epic**: 002.1 (Architectural Improvements)  
**Dependencies**: Issue 002-101 (Executor Framework Implementation)

## Problem Statement

The current samples/EnvironmentMonitor demonstrates manual device communication instead of the attribute-driven execution model that is the core value proposition of Belay.NET. The attribute system needs to be refactored to properly demonstrate method interception, automatic attribute processing, and the seamless device integration promised by the architecture. This refactoring will serve as the primary example of the attribute-based programming model.

## Technical Requirements

### Current State Analysis

The current EnvironmentMonitor sample shows manual device communication:
```csharp
// Current manual approach
await device.ExecuteAsync(@"
import machine
import time
sensor = machine.ADC(machine.Pin(26))
");

var temperature = await device.ExecuteAsync<float>(@"
reading = sensor.read_u16()
voltage = reading * 3.3 / 65535
temperature = 27 - (voltage - 0.706) / 0.001721
return temperature
");
```

### Target Attribute-Driven Model

The refactored version should demonstrate the attribute-based programming model:

```csharp
// Target attribute-driven approach
public class EnvironmentMonitor : Device
{
    public EnvironmentMonitor(string connectionString) : base(connectionString) { }
    
    [Setup]
    private async Task InitializeSensor()
    {
        // Automatic setup execution on device connection
        await ExecuteAsync(@"
import machine
import time
sensor = machine.ADC(machine.Pin(26))
calibration_offset = 27
calibration_factor = 0.001721
reference_voltage = 3.3
");
    }
    
    [Task]
    public async Task<float> ReadTemperatureAsync()
    {
        // Method interception with automatic deployment and caching
        return await ExecuteAsync<float>(@"
reading = sensor.read_u16()
voltage = reading * reference_voltage / 65535
temperature = calibration_offset - (voltage - 0.706) / calibration_factor
return temperature
");
    }
    
    [Task] 
    public async Task<float> ReadHumidityAsync()
    {
        // Additional sensor method (simulated)
        return await ExecuteAsync<float>(@"
# Simulated humidity reading
humidity = 65.0 + (sensor.read_u16() % 2000) / 100.0
return humidity
");
    }
    
    [Thread]
    public async Task StartContinuousLoggingAsync(int intervalMs = 5000)
    {
        // Background thread execution on device
        await ExecuteAsync($@"
import _thread

def continuous_logging():
    while True:
        temp = read_temperature()
        humidity = read_humidity()
        print(f'{{time.ticks_ms()}}: Temp={{temp:.1f}}C, Humidity={{humidity:.1f}}%')
        time.sleep_ms({intervalMs})

_thread.start_new_thread(continuous_logging, ())
print('Started continuous logging thread')
");
    }
    
    [Teardown]
    private async Task CleanupSensor()
    {
        // Automatic cleanup on device disconnection
        await ExecuteAsync(@"
if 'sensor' in globals():
    sensor.deinit()
print('Environment sensor cleaned up')
");
    }
}
```

### Method Interception Infrastructure

```csharp
// Method interception for attribute processing
public interface IMethodInterceptor
{
    Task<T> InterceptAsync<T>(MethodInfo method, object[] args, Func<Task<T>> proceed);
    bool CanIntercept(MethodInfo method);
}

internal class AttributeMethodInterceptor : IMethodInterceptor
{
    private readonly ITaskExecutor _taskExecutor;
    private readonly ISetupExecutor _setupExecutor;
    private readonly IThreadExecutor _threadExecutor;
    private readonly ITeardownExecutor _teardownExecutor;
    private readonly ILogger<AttributeMethodInterceptor> _logger;
    
    public async Task<T> InterceptAsync<T>(MethodInfo method, object[] args, Func<Task<T>> proceed)
    {
        var attributes = method.GetCustomAttributes<BelayMethodAttribute>(true).ToList();
        
        if (!attributes.Any())
        {
            return await proceed();
        }
        
        // Process each attribute type
        foreach (var attribute in attributes)
        {
            switch (attribute)
            {
                case TaskAttribute taskAttr:
                    return await _taskExecutor.InvokeMethodAsync<T>(method, args);
                    
                case SetupAttribute setupAttr:
                    await _setupExecutor.InitializeContextAsync();
                    break;
                    
                case ThreadAttribute threadAttr:
                    await _threadExecutor.StartBackgroundMethodAsync(method, args);
                    break;
                    
                case TeardownAttribute teardownAttr:
                    await _teardownExecutor.RegisterCleanupAction(() => (Task)proceed());
                    break;
            }
        }
        
        return default(T);
    }
    
    public bool CanIntercept(MethodInfo method)
    {
        return method.GetCustomAttributes<BelayMethodAttribute>(true).Any();
    }
}

// Enhanced base attribute class
public abstract class BelayMethodAttribute : Attribute
{
    public string Description { get; set; }
    public int Priority { get; set; } = 0;
    public bool CacheResult { get; set; } = true;
    public TimeSpan? Timeout { get; set; }
}

// Enhanced attribute implementations
[AttributeUsage(AttributeTargets.Method)]
public class TaskAttribute : BelayMethodAttribute
{
    public bool ReturnResult { get; set; } = true;
    public string[] RequiredGlobals { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Method)]
public class SetupAttribute : BelayMethodAttribute
{
    public bool RunOnce { get; set; } = true;
    public string[] Dependencies { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Method)]
public class ThreadAttribute : BelayMethodAttribute
{
    public string ThreadName { get; set; }
    public bool Daemon { get; set; } = true;
    public int StackSize { get; set; } = 4096;
}

[AttributeUsage(AttributeTargets.Method)]
public class TeardownAttribute : BelayMethodAttribute
{
    public bool RunOnError { get; set; } = true;
    public int Order { get; set; } = 0;
}
```

### Enhanced Device Base Class

```csharp
// Enhanced Device class with attribute processing
public abstract partial class Device : IDevice
{
    private readonly IMethodInterceptor _interceptor;
    private readonly Dictionary<string, MethodInfo> _setupMethods = new();
    private readonly Dictionary<string, MethodInfo> _teardownMethods = new();
    private readonly HashSet<string> _initializedSetups = new();
    
    protected Device(
        IDeviceCommunicator communicator,
        IServiceProvider serviceProvider,
        IMethodInterceptor interceptor,
        /* other dependencies */) : base(communicator, serviceProvider, /* other deps */)
    {
        _interceptor = interceptor;
        DiscoverAttributedMethods();
    }
    
    public override async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await base.ConnectAsync(cancellationToken);
        
        // Execute setup methods in priority order
        await ExecuteSetupMethodsAsync(cancellationToken);
    }
    
    public override async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        // Execute teardown methods in reverse priority order
        await ExecuteTeardownMethodsAsync(cancellationToken);
        
        await base.DisconnectAsync(cancellationToken);
    }
    
    private void DiscoverAttributedMethods()
    {
        var methods = GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        foreach (var method in methods)
        {
            var setupAttr = method.GetCustomAttribute<SetupAttribute>();
            if (setupAttr != null)
            {
                _setupMethods[method.Name] = method;
            }
            
            var teardownAttr = method.GetCustomAttribute<TeardownAttribute>();
            if (teardownAttr != null)
            {
                _teardownMethods[method.Name] = method;
            }
        }
    }
    
    private async Task ExecuteSetupMethodsAsync(CancellationToken cancellationToken)
    {
        var orderedSetups = _setupMethods.Values
            .OrderBy(m => m.GetCustomAttribute<SetupAttribute>().Priority)
            .ToList();
        
        foreach (var method in orderedSetups)
        {
            var setupAttr = method.GetCustomAttribute<SetupAttribute>();
            
            if (setupAttr.RunOnce && _initializedSetups.Contains(method.Name))
                continue;
                
            try
            {
                _logger.LogDebug("Executing setup method: {MethodName}", method.Name);
                
                var result = method.Invoke(this, Array.Empty<object>());
                if (result is Task task)
                {
                    await task;
                }
                
                _initializedSetups.Add(method.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Setup method {MethodName} failed", method.Name);
                throw new DeviceExecutionException($"Setup method {method.Name} failed: {ex.Message}", null, null);
            }
        }
    }
    
    private async Task ExecuteTeardownMethodsAsync(CancellationToken cancellationToken)
    {
        var orderedTeardowns = _teardownMethods.Values
            .OrderByDescending(m => m.GetCustomAttribute<TeardownAttribute>().Order)
            .ToList();
        
        foreach (var method in orderedTeardowns)
        {
            try
            {
                _logger.LogDebug("Executing teardown method: {MethodName}", method.Name);
                
                var result = method.Invoke(this, Array.Empty<object>());
                if (result is Task task)
                {
                    await task;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Teardown method {MethodName} failed", method.Name);
                // Continue with other teardown methods even if one fails
            }
        }
    }
}
```

### Usage Examples and Documentation

```csharp
// Complete usage example
public class Program
{
    public static async Task Main(string[] args)
    {
        // Configure services with Belay.NET
        var services = new ServiceCollection()
            .AddBelayCore(config =>
            {
                config.DefaultConnectionString = "serial:COM3";
                config.Cache.EnablePersistence = true;
            })
            .AddBelayLogging()
            .BuildServiceProvider();
        
        var deviceFactory = services.GetRequiredService<IDeviceFactory>();
        
        // Create device using factory with dependency injection
        var monitor = await deviceFactory.CreateDeviceAsync<EnvironmentMonitor>("serial:COM3");
        
        try
        {
            // Use attribute-driven methods
            var temperature = await monitor.ReadTemperatureAsync();
            var humidity = await monitor.ReadHumidityAsync();
            
            Console.WriteLine($"Temperature: {temperature:F1}°C");
            Console.WriteLine($"Humidity: {humidity:F1}%");
            
            // Start background logging
            await monitor.StartContinuousLoggingAsync(2000);
            
            // Let it run for a while
            await Task.Delay(TimeSpan.FromSeconds(30));
        }
        finally
        {
            // Automatic teardown execution
            await monitor.DisconnectAsync();
        }
    }
}

// Advanced usage with custom device types
public class WeatherStation : EnvironmentMonitor
{
    public WeatherStation(string connectionString) : base(connectionString) { }
    
    [Setup]
    private async Task InitializeWeatherSensors()
    {
        await ExecuteAsync(@"
import machine
barometer = machine.I2C(0, scl=machine.Pin(1), sda=machine.Pin(0))
wind_sensor = machine.ADC(machine.Pin(27))
");
    }
    
    [Task]
    public async Task<float> ReadBarometricPressureAsync()
    {
        return await ExecuteAsync<float>(@"
# Simulated barometric pressure reading
pressure = 1013.25 + (barometer.scan()[0] if barometer.scan() else 0) % 100
return pressure
");
    }
    
    [Task] 
    public async Task<WeatherData> GetCompleteWeatherDataAsync()
    {
        // Demonstrate complex return types
        return await ExecuteAsync<WeatherData>(@"
temp = read_temperature()
humidity = read_humidity()
pressure = read_barometric_pressure()

return {
    'temperature': temp,
    'humidity': humidity,
    'pressure': pressure,
    'timestamp': time.time()
}
");
    }
}

public record WeatherData(float Temperature, float Humidity, float Pressure, long Timestamp);
```

## Implementation Strategy

### Phase 1: Method Interception Infrastructure (Day 1)
1. Implement method interceptor interfaces and base classes
2. Create enhanced attribute classes with additional properties
3. Add method discovery and registration system
4. Integrate with existing executor framework

### Phase 2: Enhanced Device Base Class (Day 2)
1. Refactor Device class to support attribute processing
2. Add automatic setup and teardown method execution
3. Implement method interception integration
4. Add comprehensive error handling and logging

### Phase 3: Sample Refactoring and Testing (Day 3)
1. Refactor EnvironmentMonitor sample to use attributes
2. Create additional sample classes demonstrating advanced features
3. Add comprehensive integration tests
4. Create documentation and usage examples

## Definition of Done

### Functional Requirements
- [ ] EnvironmentMonitor sample refactored to use attribute-driven model
- [ ] Method interception working for all attribute types
- [ ] Automatic setup and teardown execution operational
- [ ] Enhanced Device base class supporting attribute processing
- [ ] Advanced usage examples and documentation complete

### Technical Requirements
- [ ] Method interception infrastructure implemented
- [ ] Attribute processing integrated with executor framework
- [ ] Setup and teardown method discovery and execution working
- [ ] Enhanced attribute classes with additional properties
- [ ] Integration with existing architecture components

### Quality Requirements
- [ ] Comprehensive unit test coverage for attribute processing
- [ ] Integration tests with refactored samples
- [ ] Performance benchmarks for method interception
- [ ] Documentation covering attribute-driven programming model
- [ ] Clear migration guide from manual to attribute-driven approach

## Dependencies

### Prerequisite Issues
- Issue 002-101: Executor Framework Implementation (method interception)

### Dependent Issues
- All main Epic 002 issues demonstrate the completed attribute system
- Future samples and documentation efforts

## Risk Assessment

### High Risk Items
- **Method Interception Complexity**: Complex method interception may introduce bugs
  - *Mitigation*: Comprehensive testing, clear interception boundaries
- **Attribute Processing Performance**: Attribute processing overhead on method calls
  - *Mitigation*: Performance benchmarks, caching strategies

### Medium Risk Items
- **Setup/Teardown Ordering**: Complex dependencies between setup and teardown methods
  - *Mitigation*: Clear ordering rules, dependency validation
- **Sample Complexity**: Refactored samples may be too complex for beginners
  - *Mitigation*: Progressive examples, clear documentation

## Testing Requirements

### Unit Testing
- Method interception functionality
- Attribute processing logic
- Setup and teardown method execution
- Enhanced Device class behavior
- Error handling in attribute processing

### Integration Testing
- End-to-end attribute-driven workflows
- Refactored sample execution
- Complex device hierarchies
- Performance impact assessment

### Sample Testing
- EnvironmentMonitor attribute-driven execution
- Advanced sample scenarios
- Error handling and recovery
- Cross-platform compatibility

## Acceptance Criteria

1. **Sample Refactoring**: EnvironmentMonitor uses attribute-driven model exclusively
2. **Method Interception**: All attribute types work with method interception
3. **Setup/Teardown**: Automatic execution of setup and teardown methods
4. **Enhanced Attributes**: Attribute classes support additional configuration properties
5. **Performance**: <5ms overhead for attribute processing per method call
6. **Integration**: Seamless integration with executor framework and existing architecture
7. **Documentation**: Complete examples and migration guide available

This issue refactors the attribute system to properly demonstrate the attribute-based programming model that is the core value proposition of Belay.NET, providing clear examples and comprehensive implementation of method interception and automatic attribute processing.