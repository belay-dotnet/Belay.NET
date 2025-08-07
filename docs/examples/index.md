# Examples

Welcome to the Belay.NET examples! These practical tutorials demonstrate real-world applications of MicroPython device integration with .NET.

## Basic Examples

Perfect for getting started and learning core concepts:

### [First Connection](./first-connection.md)
Learn the basics of connecting to and communicating with your first MicroPython device.

```csharp
using var device = await Device.ConnectAsync("COM3");
var version = await device.ExecuteAsync<string>("sys.version");
```

### [LED Control](./led-control.md) 
Control LEDs with type-safe remote execution and learn basic GPIO operations.

```csharp
[Task]
public async Task BlinkAsync(int times = 3) =>
    await ExecuteAsync($"for i in range({times}): led.toggle(); time.sleep(0.5)");
```

### [Sensor Reading](./sensor-reading.md)
Read sensor data with proper error handling and data validation.

```csharp
[Task]
public async Task<float> ReadTemperatureAsync() =>
    await ExecuteAsync<float>("(adc.read_u16() * 3.3 / 65536 - 0.5) * 100");
```

### [Error Handling](./error-handling.md)
Handle device errors gracefully with comprehensive exception management.

```csharp
try {
    await device.ExecuteAsync("1/0");
} catch (DeviceExecutionException ex) {
    Console.WriteLine($"Python error: {ex.PythonExceptionType}");
}
```

## Advanced Examples

Production-ready examples for enterprise applications:

### [ASP.NET Core Integration](./aspnet-core.md)
Build web APIs that control IoT devices with dependency injection and health checks.

```csharp
[HttpGet("temperature")]
public async Task<ActionResult<float>> GetTemperature()
{
    using var device = _deviceFactory.CreateSerialDevice("COM3");
    await device.ConnectAsync();
    return await device.ExecuteAsync<float>("sensor.read_temp()");
}
```

### [Background Services](./background-services.md)
Create Windows Services and Worker Services for continuous device monitoring.

```csharp
public class DeviceMonitorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await MonitorDevicesAsync();
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

### [Multiple Devices](./multiple-devices.md)
Manage multiple devices concurrently with connection pooling and coordination.

```csharp
var tasks = devices.Select(async device => {
    var reading = await device.ReadSensorAsync();
    return new { Device = device.Name, Reading = reading };
});
var results = await Task.WhenAll(tasks);
```

### [Custom Attributes](./custom-attributes.md)
Create domain-specific attributes for specialized device operations.

```csharp
[SensorReading(SensorType.Temperature, Units.Celsius)]
public async Task<float> ReadTemperatureAsync() =>
    await ExecuteAsync<float>("temp_sensor.read()");
```

## Real-World Projects

Complete applications demonstrating end-to-end IoT solutions:

### Smart Home Controller
A complete home automation system with:
- Temperature and humidity monitoring
- Smart lighting control
- Security sensor integration
- Web dashboard with real-time updates

### Industrial Data Logger
Production monitoring system featuring:
- Multi-sensor data collection
- Database storage with Entity Framework
- Real-time alerts and notifications
- Historical data analysis

### Weather Station
Environmental monitoring solution with:
- Multiple sensor types (temperature, humidity, pressure, wind)
- Data visualization with charts
- Weather prediction algorithms
- Mobile-responsive web interface

### Greenhouse Automation
Automated greenhouse management including:
- Climate control with feedback loops
- Automated watering system
- Growth monitoring with cameras
- Mobile notifications and control

## Integration Examples

Learn how to integrate Belay.NET with popular .NET technologies:

### Entity Framework Integration
Store device data in databases with proper modeling:

```csharp
public class SensorReading
{
    public int Id { get; set; }
    public string DeviceId { get; set; }
    public float Temperature { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SensorContext : DbContext
{
    public DbSet<SensorReading> SensorReadings { get; set; }
}
```

### SignalR Real-Time Updates
Push device data to web clients in real-time:

```csharp
public class DeviceHub : Hub
{
    public async Task JoinDeviceGroup(string deviceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, deviceId);
    }
}

// Send updates to all connected clients
await _hubContext.Clients.Group(deviceId)
    .SendAsync("SensorUpdate", new { Temperature = reading });
```

### Blazor Server Integration
Create interactive web UIs for device control:

```razor
@page "/devices"
@using Belay.Core
@inject IDeviceFactory DeviceFactory

<h3>Device Dashboard</h3>

<div class="card-deck">
    @foreach (var device in _devices)
    {
        <div class="card">
            <div class="card-body">
                <h5 class="card-title">@device.Name</h5>
                <p class="card-text">Temperature: @device.Temperature°C</p>
                <button class="btn btn-primary" @onclick="() => RefreshDevice(device)">
                    Refresh
                </button>
            </div>
        </div>
    }
</div>
```

## Testing Examples

Learn how to test your Belay.NET applications effectively:

### Unit Testing with Mocks
```csharp
[TestMethod]
public async Task SensorService_ReadTemperature_ReturnsValidReading()
{
    // Arrange
    var mockDevice = new Mock<IDevice>();
    mockDevice.Setup(d => d.ExecuteAsync<float>(It.IsAny<string>(), default))
              .ReturnsAsync(23.5f);
    
    var service = new SensorService(mockDevice.Object);
    
    // Act
    var temperature = await service.ReadTemperatureAsync();
    
    // Assert
    Assert.AreEqual(23.5f, temperature);
}
```

### Integration Testing
```csharp
[TestMethod]
[TestCategory("Integration")]
public async Task Device_RealHardware_CanReadSensor()
{
    if (!HardwareAvailable()) return;
    
    using var device = await Device.ConnectAsync("COM3");
    var reading = await device.ExecuteAsync<float>("sensor.read()");
    
    Assert.IsTrue(reading > 0);
}
```

## Performance Examples

Optimize your applications for high-throughput scenarios:

### Connection Pooling
```csharp
public class DevicePool : IDisposable
{
    private readonly ConcurrentQueue<Device> _availableDevices = new();
    
    public async Task<Device> GetDeviceAsync()
    {
        if (_availableDevices.TryDequeue(out var device))
        {
            return device;
        }
        
        return await Device.ConnectAsync(GetNextAvailablePort());
    }
    
    public void ReturnDevice(Device device)
    {
        _availableDevices.Enqueue(device);
    }
}
```

### Batch Operations
```csharp
[Task]
public async Task<Dictionary<string, float>> ReadAllSensorsAsync()
{
    return await ExecuteAsync<Dictionary<string, float>>("""
        {
            'temperature': temp_sensor.read(),
            'humidity': humidity_sensor.read(),
            'pressure': pressure_sensor.read()
        }
        """);
}
```

## Getting Started

1. **Pick an Example**: Start with [First Connection](./first-connection.md) if you're new to Belay.NET
2. **Set Up Hardware**: Follow our [Hardware Guides](../hardware/) for device setup
3. **Run the Code**: Each example includes complete, runnable code
4. **Experiment**: Modify the examples to match your specific hardware setup
5. **Scale Up**: Move to advanced examples as you become more comfortable

## Hardware Requirements

Most examples work with common development boards:

- **Raspberry Pi Pico**: Perfect for beginners, runs MicroPython out of the box
- **ESP32**: Great for WiFi-enabled projects and advanced features
- **PyBoard**: Original MicroPython board with rich peripheral support

See our [Hardware Compatibility Matrix](../hardware/compatibility.md) for complete device support.

## Contributing Examples

Have a great Belay.NET project? We'd love to include it! See our [Contributing Guide](../contributing.md) for how to submit examples.

## Need Help?

- **[GitHub Discussions](https://github.com/belay-dotnet/belay/discussions)** - Ask questions about examples
- **[Issue Tracker](https://github.com/belay-dotnet/belay/issues)** - Report problems with examples
- **[Stack Overflow](https://stackoverflow.com/questions/tagged/belay.net)** - Get help from the community