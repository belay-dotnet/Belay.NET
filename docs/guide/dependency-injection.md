# Dependency Injection

Belay.NET provides first-class support for dependency injection using Microsoft.Extensions.DependencyInjection. This enables clean architecture, testability, and seamless integration with ASP.NET Core, Worker Services, and other .NET applications.

## Quick Setup

### Console Application

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Belay.Extensions;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddBelay(config => {
            config.Device.DefaultConnectionTimeoutMs = 5000;
            config.Communication.Serial.DefaultBaudRate = 115200;
        });
    })
    .Build();

var deviceFactory = host.Services.GetRequiredService<IDeviceFactory>();
using var device = deviceFactory.CreateSerialDevice("COM3");
await device.ConnectAsync();

var result = await device.ExecuteAsync<float>("sensor.read_temp()");
Console.WriteLine($"Temperature: {result}°C");
```

### ASP.NET Core

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBelay(config => {
    config.Device.DefaultConnectionTimeoutMs = 10000;
    config.Communication.Serial.MaxConcurrentConnections = 5;
});

builder.Services.AddControllers();
var app = builder.Build();

app.MapControllers();
app.Run();
```

```csharp
// Controllers/SensorController.cs
[ApiController]
[Route("api/[controller]")]
public class SensorController : ControllerBase
{
    private readonly IDeviceFactory _deviceFactory;
    private readonly ILogger<SensorController> _logger;

    public SensorController(IDeviceFactory deviceFactory, ILogger<SensorController> logger)
    {
        _deviceFactory = deviceFactory;
        _logger = logger;
    }

    [HttpGet("temperature")]
    public async Task<ActionResult<float>> GetTemperature()
    {
        try
        {
            using var device = _deviceFactory.CreateSerialDevice("COM3");
            await device.ConnectAsync();
            
            var temperature = await device.ExecuteAsync<float>("sensor.read_temp()");
            _logger.LogInformation("Temperature reading: {Temperature}°C", temperature);
            
            return Ok(temperature);
        }
        catch (DeviceConnectionException ex)
        {
            _logger.LogError(ex, "Failed to connect to temperature sensor");
            return StatusCode(503, "Sensor unavailable");
        }
    }
}
```

## Configuration

### Configuration Options

Belay.NET uses a hierarchical configuration system:

```csharp
services.AddBelay(config => {
    // Device configuration
    config.Device.DefaultConnectionTimeoutMs = 10000;
    config.Device.MaxRetryAttempts = 3;
    config.Device.EnableAutoReconnect = true;
    
    // Communication configuration
    config.Communication.Serial.DefaultBaudRate = 115200;
    config.Communication.Serial.MaxConcurrentConnections = 10;
    config.Communication.Serial.ConnectionPooling = true;
    
    // Session configuration  
    config.Session.MaxConcurrentSessions = 50;
    config.Session.SessionTimeoutMs = 300000; // 5 minutes
    config.Session.EnableSessionCaching = true;
    
    // Executor configuration
    config.Executor.EnableMethodCaching = true;
    config.Executor.CacheTimeoutMs = 60000;
    config.Executor.MaxConcurrentExecutions = 20;
    
    // Exception handling
    config.ExceptionHandling.EnableDetailedStackTraces = true;
    config.ExceptionHandling.LogDeviceErrors = true;
});
```

### Configuration from appsettings.json

```json
{
  "Belay": {
    "Device": {
      "DefaultConnectionTimeoutMs": 10000,
      "MaxRetryAttempts": 3,
      "EnableAutoReconnect": true
    },
    "Communication": {
      "Serial": {
        "DefaultBaudRate": 115200,
        "MaxConcurrentConnections": 10,
        "ConnectionPooling": true
      }
    },
    "Session": {
      "MaxConcurrentSessions": 50,
      "SessionTimeoutMs": 300000,
      "EnableSessionCaching": true
    }
  }
}
```

```csharp
// Load from configuration
services.AddBelay(builder.Configuration.GetSection("Belay"));

// Or combine with code configuration
services.AddBelay(builder.Configuration.GetSection("Belay"), config => {
    // Override specific settings
    config.Device.EnableAutoReconnect = false;
});
```

## Factory Patterns

### Device Factory

Create devices with consistent configuration:

```csharp
public class IoTService
{
    private readonly IDeviceFactory _deviceFactory;

    public IoTService(IDeviceFactory deviceFactory)
    {
        _deviceFactory = deviceFactory;
    }

    public async Task<float> ReadTemperatureAsync(string port)
    {
        using var device = _deviceFactory.CreateSerialDevice(port);
        await device.ConnectAsync();
        return await device.ExecuteAsync<float>("sensor.read_temp()");
    }

    public async Task<List<SensorReading>> ReadMultipleSensorsAsync(string[] ports)
    {
        var tasks = ports.Select(async port => {
            using var device = _deviceFactory.CreateSerialDevice(port);
            await device.ConnectAsync();
            var temp = await device.ExecuteAsync<float>("temp_sensor.read()");
            var humidity = await device.ExecuteAsync<float>("humidity_sensor.read()");
            return new SensorReading(port, temp, humidity);
        });

        return (await Task.WhenAll(tasks)).ToList();
    }
}

public record SensorReading(string Port, float Temperature, float Humidity);
```

### Communication Factory

Create different communication types:

```csharp
public class DeviceManager
{
    private readonly ICommunicatorFactory _communicatorFactory;
    private readonly ILogger<DeviceManager> _logger;

    public DeviceManager(ICommunicatorFactory communicatorFactory, ILogger<DeviceManager> logger)
    {
        _communicatorFactory = communicatorFactory;
        _logger = logger;
    }

    public async Task<Device> ConnectToDeviceAsync(string connectionString)
    {
        var communicator = connectionString switch
        {
            var s when s.StartsWith("COM") => _communicatorFactory.CreateSerial(s),
            var s when s.StartsWith("/dev/") => _communicatorFactory.CreateSerial(s),
            var s when s.StartsWith("subprocess:") => _communicatorFactory.CreateSubprocess(s[11..]),
            _ => throw new ArgumentException($"Unsupported connection string: {connectionString}")
        };

        var device = new Device(communicator);
        await device.ConnectAsync();
        
        _logger.LogInformation("Connected to device via {Connection}", connectionString);
        return device;
    }
}
```

## Custom Device Registration

### Register Custom Device Classes

```csharp
public interface ITemperatureSensor
{
    Task<float> ReadTemperatureAsync();
    Task<bool> CalibrateAsync();
}

public class TemperatureSensorDevice : Device, ITemperatureSensor
{
    public TemperatureSensorDevice(IDeviceCommunication communication, ILogger<TemperatureSensorDevice> logger)
        : base(communication, logger) { }

    [Setup]
    public async Task InitializeAsync()
    {
        await ExecuteAsync("from machine import ADC, Pin; sensor = ADC(Pin(26))");
    }

    [Task(Cache = true)]
    public async Task<bool> CalibrateAsync()
    {
        return await ExecuteAsync<bool>("sensor.calibrate()");
    }

    [Task]
    public async Task<float> ReadTemperatureAsync()
    {
        return await ExecuteAsync<float>("sensor.read_temp()");
    }
}

// Register custom device
services.AddBelay();
services.AddTransient<ITemperatureSensor, TemperatureSensorDevice>();

// Factory for custom devices
services.AddTransient<Func<string, ITemperatureSensor>>(serviceProvider => 
    port => {
        var communicator = serviceProvider.GetRequiredService<ICommunicatorFactory>()
            .CreateSerial(port);
        return new TemperatureSensorDevice(communicator, 
            serviceProvider.GetRequiredService<ILogger<TemperatureSensorDevice>>());
    });
```

### Use Custom Devices

```csharp
public class SensorService
{
    private readonly Func<string, ITemperatureSensor> _sensorFactory;

    public SensorService(Func<string, ITemperatureSensor> sensorFactory)
    {
        _sensorFactory = sensorFactory;
    }

    public async Task<List<float>> ReadAllTemperaturesAsync(string[] ports)
    {
        var tasks = ports.Select(async port => {
            using var sensor = _sensorFactory(port);
            await ((Device)sensor).ConnectAsync();
            return await sensor.ReadTemperatureAsync();
        });

        return (await Task.WhenAll(tasks)).ToList();
    }
}
```

## Health Checks

Belay.NET includes built-in health checks for monitoring device connectivity:

```csharp
services.AddBelay(config => {
    config.Device.EnableHealthChecks = true;
});

services.AddHealthChecks()
    .AddBelay() // Adds system health checks
    .AddBelayDevice("main-sensor", "COM3") // Specific device health check
    .AddBelayDevice("backup-sensor", "COM4");

// ASP.NET Core health check endpoint
app.MapHealthChecks("/health");
```

### Custom Health Checks

```csharp
public class CustomSensorHealthCheck : IHealthCheck
{
    private readonly IDeviceFactory _deviceFactory;
    private readonly string _port;

    public CustomSensorHealthCheck(IDeviceFactory deviceFactory, string port)
    {
        _deviceFactory = deviceFactory;
        _port = port;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var device = _deviceFactory.CreateSerialDevice(_port);
            await device.ConnectAsync(cancellationToken);
            
            // Test basic functionality
            var version = await device.ExecuteAsync<string>("sys.version");
            var freeMemory = await device.ExecuteAsync<int>("gc.mem_free()");
            
            var data = new Dictionary<string, object>
            {
                ["port"] = _port,
                ["version"] = version,
                ["free_memory"] = freeMemory
            };

            return freeMemory > 1000 
                ? HealthCheckResult.Healthy($"Sensor on {_port} is healthy", data)
                : HealthCheckResult.Degraded($"Low memory on {_port}", null, data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Sensor on {_port} is unavailable", ex);
        }
    }
}

// Register custom health check
services.AddHealthChecks()
    .Add(new HealthCheckRegistration(
        "custom-sensor",
        sp => new CustomSensorHealthCheck(
            sp.GetRequiredService<IDeviceFactory>(), 
            "COM3"),
        HealthStatus.Degraded,
        new[] { "sensors", "hardware" },
        TimeSpan.FromSeconds(30)));
```

## Testing with Dependency Injection

### Mock Services for Testing

```csharp
public class MockDeviceFactory : IDeviceFactory
{
    public Device CreateSerialDevice(string portName, SerialConnectionSettings? settings = null)
    {
        var mockCommunicator = new Mock<IDeviceCommunication>();
        
        // Setup mock behavior
        mockCommunicator
            .Setup(c => c.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("mock_response");

        return new Device(mockCommunicator.Object);
    }

    public Device CreateSubprocessDevice(string executablePath, string[]? arguments = null)
    {
        throw new NotImplementedException("Not needed for this test");
    }
}

// Test setup
[TestMethod]
public async Task SensorService_ReadTemperature_ReturnsExpectedValue()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddTransient<IDeviceFactory, MockDeviceFactory>();
    services.AddTransient<SensorService>();
    
    var serviceProvider = services.BuildServiceProvider();
    var sensorService = serviceProvider.GetRequiredService<SensorService>();

    // Act
    var temperature = await sensorService.ReadTemperatureAsync("COM3");

    // Assert
    Assert.AreEqual(23.5f, temperature);
}
```

### Integration Testing

```csharp
public class IntegrationTestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }

    public IntegrationTestFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBelay(config => {
            config.Device.DefaultConnectionTimeoutMs = 5000;
        });

        ServiceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        (ServiceProvider as IDisposable)?.Dispose();
    }
}

[TestClass]
public class DeviceIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public DeviceIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task DeviceFactory_CreateSerialDevice_CanConnect()
    {
        // Skip if no hardware available
        if (!HardwareDetection.IsAvailable()) 
            return;

        var factory = _fixture.ServiceProvider.GetRequiredService<IDeviceFactory>();
        
        using var device = factory.CreateSerialDevice("COM3");
        await device.ConnectAsync();
        
        var result = await device.ExecuteAsync<string>("'Hello World'");
        Assert.AreEqual("Hello World", result);
    }
}
```

## Advanced Configuration

### Multiple Device Configurations

```csharp
services.AddBelay("primary", config => {
    config.Device.DefaultConnectionTimeoutMs = 5000;
    config.Communication.Serial.DefaultBaudRate = 115200;
});

services.AddBelay("secondary", config => {
    config.Device.DefaultConnectionTimeoutMs = 10000;
    config.Communication.Serial.DefaultBaudRate = 9600;
});

// Inject named configurations
public class MultiDeviceService
{
    private readonly IDeviceFactory _primaryFactory;
    private readonly IDeviceFactory _secondaryFactory;

    public MultiDeviceService(
        [FromKeyedServices("primary")] IDeviceFactory primaryFactory,
        [FromKeyedServices("secondary")] IDeviceFactory secondaryFactory)
    {
        _primaryFactory = primaryFactory;
        _secondaryFactory = secondaryFactory;
    }
}
```

### Environment-Specific Configuration

```csharp
services.AddBelay(config => {
    if (Environment.IsDevelopment())
    {
        config.Communication.EnableDebugLogging = true;
        config.Device.DefaultConnectionTimeoutMs = 30000; // Longer for debugging
    }
    else if (Environment.IsProduction())
    {
        config.Communication.Serial.ConnectionPooling = true;
        config.Session.EnableSessionCaching = true;
        config.Device.EnableAutoReconnect = true;
    }
});
```

## Best Practices

### Service Lifetime Management

```csharp
// Correct: Register factories as Singleton (they're stateless)
services.AddSingleton<IDeviceFactory, DeviceFactory>();

// Correct: Register custom devices as Transient (they have state)
services.AddTransient<ITemperatureSensor, TemperatureSensorDevice>();

// Correct: Register session managers as Scoped for web apps
services.AddScoped<IDeviceSessionManager, DeviceSessionManager>();
```

### Resource Management

```csharp
public class ResourceManagedService : IDisposable
{
    private readonly List<Device> _devices = new();
    private readonly IDeviceFactory _deviceFactory;

    public ResourceManagedService(IDeviceFactory deviceFactory)
    {
        _deviceFactory = deviceFactory;
    }

    public async Task<Device> GetDeviceAsync(string port)
    {
        var device = _deviceFactory.CreateSerialDevice(port);
        await device.ConnectAsync();
        _devices.Add(device);
        return device;
    }

    public void Dispose()
    {
        foreach (var device in _devices)
        {
            device?.Dispose();
        }
        _devices.Clear();
    }
}

// Register with proper lifetime
services.AddScoped<ResourceManagedService>();
```

### Configuration Validation

```csharp
services.AddBelay(config => {
    config.Device.DefaultConnectionTimeoutMs = 5000;
})
.Validate(config => config.Device.DefaultConnectionTimeoutMs > 0, "Timeout must be positive")
.Validate(config => config.Communication.Serial.DefaultBaudRate >= 9600, "Baud rate too low");
```

## Next Steps

- **[Testing](./testing)** - Comprehensive testing strategies
- **[Configuration](./configuration)** - Advanced configuration management  
- **[Examples](../examples/)** - Real-world DI integration examples
- **[Health Monitoring](./health-monitoring)** - System health and monitoring