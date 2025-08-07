# Device Communication

Belay.NET uses the **Raw REPL Protocol** to communicate with MicroPython devices. This protocol provides programmatic access to the Python interpreter running on your microcontroller, enabling seamless code execution and data exchange.

## Understanding Raw REPL

### What is Raw REPL?

Raw REPL (Read-Eval-Print Loop) is a binary protocol that allows programmatic control of a MicroPython interpreter. Unlike the interactive REPL you see in a terminal, Raw REPL is designed for automated communication between applications and devices.

### Protocol Flow

```mermaid
sequenceDiagram
    participant App as .NET Application
    participant Device as MicroPython Device
    
    App->>Device: Ctrl-A (0x01) - Enter Raw Mode
    Device->>App: "raw REPL; CTRL-B to exit"
    
    App->>Device: Python Code
    App->>Device: Ctrl-D (0x04) - Execute
    
    Device->>App: "OK" + Results
    Device->>App: Ctrl-D (0x04) - End of execution
    
    App->>Device: Ctrl-B (0x02) - Exit Raw Mode
    Device->>App: Normal REPL prompt
```

## Connection Types

Belay.NET supports multiple connection methods:

### Serial/USB Connection

The primary connection method for most development boards:

```csharp
// Basic serial connection
using var device = await Device.ConnectAsync("COM3");

// With custom settings
var connection = new SerialConnection("COM3") {
    BaudRate = 115200,
    ReadTimeoutMs = 5000,
    WriteTimeoutMs = 2000
};
using var device = new Device(connection);
await device.ConnectAsync();
```

**Supported Parameters:**
- **BaudRate**: Communication speed (default: 115200)
- **ReadTimeout**: Maximum wait time for device responses
- **WriteTimeout**: Maximum time for sending data
- **DataBits**: Number of data bits (default: 8)
- **StopBits**: Number of stop bits (default: 1)
- **Parity**: Parity checking (default: None)

### Subprocess Connection (Testing)

For hardware-independent testing using the MicroPython Unix port:

```csharp
// Connect to MicroPython unix port
using var device = await Device.ConnectSubprocessAsync("./micropython");

// With custom arguments
var connection = new SubprocessConnection("./micropython") {
    Arguments = new[] { "-X", "heapsize=1M" },
    WorkingDirectory = "/path/to/working/dir"
};
using var device = new Device(connection);
await device.ConnectAsync();
```

### WebREPL Connection (Future)

Wireless connection support is planned for v0.3.0:

```csharp
// Future WebREPL support
using var device = await Device.ConnectWebReplAsync("ws://192.168.1.100:8266", password: "mypassword");
```

## Code Execution

### Basic Execution

Execute Python code on the remote device:

```csharp
// Execute without return value
await device.ExecuteAsync("print('Hello from device!')");

// Execute with return value
var result = await device.ExecuteAsync<int>("2 + 3");
Console.WriteLine($"Result: {result}"); // Output: Result: 5
```

### Multi-line Code

Execute complex Python code blocks:

```csharp
await device.ExecuteAsync("""
    from machine import Pin, ADC
    import time
    
    # Initialize sensors
    led = Pin(25, Pin.OUT)
    sensor = ADC(Pin(26))
    
    # Take 10 readings
    readings = []
    for i in range(10):
        reading = sensor.read_u16()
        readings.append(reading)
        led.toggle()
        time.sleep_ms(100)
    
    average = sum(readings) / len(readings)
    print(f"Average reading: {average}")
    """);
```

### Type-Safe Returns

Belay.NET supports automatic type conversion for return values:

```csharp
// Basic types
var intValue = await device.ExecuteAsync<int>("42");
var floatValue = await device.ExecuteAsync<float>("3.14159");
var stringValue = await device.ExecuteAsync<string>("'Hello World'");
var boolValue = await device.ExecuteAsync<bool>("True");

// Collections
var list = await device.ExecuteAsync<List<int>>("[1, 2, 3, 4, 5]");
var dict = await device.ExecuteAsync<Dictionary<string, object>>("""
    {'name': 'Sensor1', 'value': 42, 'active': True}
    """);

// Custom objects (JSON serializable)
var sensorData = await device.ExecuteAsync<SensorReading>("""
    {'timestamp': 1640995200, 'temperature': 23.5, 'humidity': 65.2}
    """);

public record SensorReading(long Timestamp, float Temperature, float Humidity);
```

## Error Handling

Belay.NET provides comprehensive error handling with device error mapping:

### Exception Types

```csharp
try 
{
    await device.ExecuteAsync("1 / 0");  // Division by zero
}
catch (DeviceExecutionException ex)
{
    Console.WriteLine($"Python error: {ex.Message}");
    Console.WriteLine($"Error type: {ex.PythonExceptionType}"); // "ZeroDivisionError"
    Console.WriteLine($"Line number: {ex.LineNumber}");
    Console.WriteLine($"Stack trace: {ex.DeviceStackTrace}");
}
catch (DeviceConnectionException ex)
{
    Console.WriteLine($"Connection lost: {ex.Message}");
    // Attempt reconnection logic
}
catch (BelayTimeoutException ex)
{
    Console.WriteLine($"Operation timed out: {ex.Message}");
}
```

### Connection State Management

Monitor and handle connection state:

```csharp
device.ConnectionStateChanged += (sender, state) => {
    Console.WriteLine($"Connection state: {state}");
};

// Check connection before operations
if (device.IsConnected)
{
    await device.ExecuteAsync("print('Still connected!')");
}
else
{
    await device.ReconnectAsync();
}
```

## Performance Considerations

### Raw-Paste Mode

For large code blocks, Belay.NET automatically uses Raw-Paste mode for better performance:

```csharp
// Large code blocks automatically use Raw-Paste mode
await device.ExecuteAsync("""
    # This large code block will use Raw-Paste mode automatically
    import machine
    import time
    
    # ... many lines of code ...
    # The protocol handles flow control automatically
    """);
```

### Connection Pooling

For applications with multiple devices:

```csharp
// Using dependency injection (recommended)
services.AddBelay(config => {
    config.Communication.Serial.MaxConcurrentConnections = 10;
    config.Communication.Serial.ConnectionPooling = true;
});

// Manual connection management
var devicePool = new DevicePool();
using var device1 = await devicePool.GetDeviceAsync("COM3");
using var device2 = await devicePool.GetDeviceAsync("COM4");

// Parallel execution
var tasks = new[] {
    device1.ExecuteAsync<float>("sensor1.read()"),
    device2.ExecuteAsync<float>("sensor2.read()")
};
var results = await Task.WhenAll(tasks);
```

### Caching and Optimization

Belay.NET includes automatic optimizations:

- **Method Deployment Caching**: Repeated code execution is optimized
- **Connection Reuse**: Connections are maintained across operations
- **Protocol Optimization**: Automatic selection of optimal protocol mode

## Advanced Features

### Custom Communication Protocols

Extend Belay.NET with custom protocols:

```csharp
public class CustomConnection : IDeviceCommunication
{
    public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken)
    {
        // Custom protocol implementation
        return await MyCustomProtocol.ExecuteAsync(code);
    }
}

// Register custom connection
services.AddBelay(config => {
    config.Communication.RegisterCustomProtocol<CustomConnection>("custom");
});
```

### Protocol Debugging

Enable detailed protocol logging:

```csharp
services.AddBelay(config => {
    config.Communication.EnableProtocolLogging = true;
    config.Communication.LogLevel = LogLevel.Debug;
});

// Logs will show:
// [DEBUG] Belay.Communication: Sending: \x01
// [DEBUG] Belay.Communication: Received: raw REPL; CTRL-B to exit
// [DEBUG] Belay.Communication: Sending: print('hello')\x04
// [DEBUG] Belay.Communication: Received: hello\r\n\x04>
```

## Best Practices

### Connection Management
- Always use `using` statements for proper disposal
- Handle connection failures gracefully with retry logic
- Monitor connection state for long-running applications

### Code Execution
- Keep code blocks focused and small when possible
- Use type-safe return values for better debugging
- Handle device-specific Python exceptions appropriately

### Performance
- Reuse connections instead of creating new ones for each operation
- Use async/await patterns throughout your application
- Consider connection pooling for high-throughput scenarios

### Error Handling
- Always wrap device operations in try-catch blocks
- Log errors with sufficient context for debugging
- Implement appropriate retry strategies for transient failures

## Next Steps

- **[Attribute Programming](./attributes)** - Learn the declarative programming model
- **[Dependency Injection](./dependency-injection)** - Integrate with DI containers
- **[Examples](../examples/)** - See practical communication patterns
- **[Hardware Guides](../hardware/)** - Device-specific setup and optimization