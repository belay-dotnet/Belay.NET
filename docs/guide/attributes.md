# Attribute Programming

Belay.NET's attribute-based programming model is its most powerful feature. By decorating C# methods with attributes, you can seamlessly execute code on MicroPython devices with full type safety and IntelliSense support.

## Core Attributes

### `[Task]` - Remote Execution

The `[Task]` attribute marks methods for remote execution on the device:

```csharp
public class TemperatureSensor : Device
{
    [Task]
    public async Task<float> ReadTemperatureAsync()
    {
        return await ExecuteAsync<float>("sensor.read_temp()");
    }

    [Task(Cache = true)]  
    public async Task<string> GetDeviceInfoAsync()
    {
        return await ExecuteAsync<string>("sys.version");
    }

    [Task(TimeoutMs = 10000)]
    public async Task<bool> CalibrateAsync()
    {
        return await ExecuteAsync<bool>("sensor.calibrate()");
    }
}
```

**Parameters:**
- **Cache**: Enable result caching for expensive operations
- **TimeoutMs**: Custom timeout for the operation
- **RetryCount**: Number of retry attempts on failure

### `[Setup]` - Initialization

The `[Setup]` attribute runs once when the device is first used:

```csharp
public class SmartSensor : Device  
{
    [Setup]
    public async Task InitializeAsync()
    {
        await ExecuteAsync("""
            from machine import Pin, ADC
            import time
            
            # Initialize hardware
            sensor = ADC(Pin(26))
            led = Pin(25, Pin.OUT)
            
            # Configuration
            led.off()
            print("Sensor initialized")
            """);
    }

    [Task]
    public async Task<float> ReadAsync() =>
        await ExecuteAsync<float>("sensor.read_u16() * 3.3 / 65536");
}

// Setup runs automatically on first task call
var sensor = new SmartSensor();
await sensor.ConnectAsync("COM3");
var reading = await sensor.ReadAsync(); // InitializeAsync runs first
```

### `[Teardown]` - Cleanup

The `[Teardown]` attribute runs when the device is disposed:

```csharp
public class MotorController : Device
{
    [Setup]
    public async Task InitializeAsync()
    {
        await ExecuteAsync("""
            from machine import Pin, PWM
            motor = PWM(Pin(16), freq=1000)
            """);
    }

    [Task]
    public async Task SetSpeedAsync(int speed)
    {
        await ExecuteAsync($"motor.duty_u16({speed})");
    }

    [Teardown]
    public async Task StopMotorAsync()
    {
        await ExecuteAsync("""
            motor.duty_u16(0)
            motor.deinit()
            print("Motor stopped safely")
            """);
    }
}

// Usage with automatic cleanup
using (var motor = new MotorController())
{
    await motor.ConnectAsync("COM3");
    await motor.SetSpeedAsync(50000);
    // StopMotorAsync runs automatically on disposal
}
```

### `[Thread]` - Background Execution

The `[Thread]` attribute runs code in a background thread on the device:

```csharp
public class DataLogger : Device
{
    [Setup]
    public async Task InitializeAsync()
    {
        await ExecuteAsync("""
            from machine import Pin, ADC
            import time
            import _thread
            
            sensor = ADC(Pin(26))
            data_log = []
            logging_active = False
            """);
    }

    [Thread]
    public async Task StartLoggingAsync(int intervalMs = 1000)
    {
        await ExecuteAsync($"""
            def log_data():
                global logging_active, data_log
                logging_active = True
                while logging_active:
                    reading = sensor.read_u16()
                    timestamp = time.ticks_ms()
                    data_log.append((timestamp, reading))
                    time.sleep_ms({intervalMs})
            
            _thread.start_new_thread(log_data, ())
            """);
    }

    [Task]
    public async Task<List<(int, int)>> GetDataAsync()
    {
        return await ExecuteAsync<List<(int, int)>>("data_log");
    }

    [Task] 
    public async Task StopLoggingAsync()
    {
        await ExecuteAsync("logging_active = False");
    }
}
```

## Advanced Attribute Features

### Parameter Passing

Attributes support complex parameter passing with automatic serialization:

```csharp
public class ConfigurableSensor : Device
{
    [Task]
    public async Task<float> ReadWithConfigAsync(SensorConfig config)
    {
        return await ExecuteAsync<float>($"""
            # Config is automatically serialized to Python
            config = {JsonSerializer.Serialize(config)}
            
            sensor.set_gain(config['gain'])
            sensor.set_samples(config['samples'])
            
            readings = []
            for i in range(config['samples']):
                readings.append(sensor.read())
                time.sleep_ms(config['delay_ms'])
                
            sum(readings) / len(readings)
            """);
    }

    [Task]
    public async Task<Dictionary<string, object>> BulkReadAsync(string[] sensors)
    {
        return await ExecuteAsync<Dictionary<string, object>>($"""
            sensors = {JsonSerializer.Serialize(sensors)}
            results = {{}}
            
            for sensor_name in sensors:
                results[sensor_name] = eval(f"{sensor_name}.read()")
                
            results
            """);
    }
}

public record SensorConfig(int Gain, int Samples, int DelayMs);

// Usage with complex parameters
var config = new SensorConfig(Gain: 2, Samples: 10, DelayMs: 100);
var reading = await sensor.ReadWithConfigAsync(config);

var sensors = new[] { "temp_sensor", "humidity_sensor", "pressure_sensor" };
var allReadings = await sensor.BulkReadAsync(sensors);
```

### Error Handling in Attributes

Handle errors gracefully within attributed methods:

```csharp
public class RobustSensor : Device
{
    [Task(RetryCount = 3)]
    public async Task<float> ReadTemperatureAsync()
    {
        try
        {
            return await ExecuteAsync<float>("""
                try:
                    reading = sensor.read_temp()
                    if reading < -40 or reading > 85:
                        raise ValueError("Reading out of range")
                    reading
                except Exception as e:
                    raise RuntimeError(f"Sensor error: {e}")
                """);
        }
        catch (DeviceExecutionException ex) when (ex.PythonExceptionType == "RuntimeError")
        {
            throw new SensorException($"Sensor hardware error: {ex.Message}", ex);
        }
    }

    [Task]
    public async Task<bool> SelfTestAsync()
    {
        try
        {
            await ExecuteAsync("""
                # Run comprehensive self-test
                assert sensor.status() == "OK"
                assert sensor.read_temp() is not None
                assert 10 < sensor.read_temp() < 40  # Reasonable room temp
                """);
            return true;
        }
        catch (DeviceExecutionException)
        {
            return false; // Self-test failed
        }
    }
}

public class SensorException : BelayException
{
    public SensorException(string message, Exception? innerException = null) 
        : base(message, innerException) { }
}
```

## Attribute Composition

Combine multiple attributes for complex behaviors:

```csharp
public class AdvancedSensor : Device
{
    private bool _isCalibrated;

    [Setup]
    public async Task InitializeHardwareAsync()
    {
        await ExecuteAsync("""
            from machine import Pin, ADC, Timer
            import time
            
            sensor = ADC(Pin(26))
            status_led = Pin(25, Pin.OUT)
            calibration_data = None
            
            print("Hardware initialized")
            """);
    }

    [Task(Cache = true, TimeoutMs = 30000)]
    public async Task<bool> CalibrateAsync()
    {
        var success = await ExecuteAsync<bool>("""
            # Calibration procedure
            status_led.on()
            
            readings = []
            for i in range(100):
                readings.append(sensor.read_u16())
                time.sleep_ms(50)
            
            calibration_data = {
                'offset': sum(readings) / len(readings),
                'min_val': min(readings),
                'max_val': max(readings)
            }
            
            status_led.off()
            True  # Calibration successful
            """);

        _isCalibrated = success;
        return success;
    }

    [Thread]
    public async Task StartMonitoringAsync()
    {
        if (!_isCalibrated)
        {
            throw new InvalidOperationException("Device must be calibrated before monitoring");
        }

        await ExecuteAsync("""
            def monitor():
                while True:
                    reading = sensor.read_u16()
                    corrected = reading - calibration_data['offset']
                    
                    if abs(corrected) > 1000:  # Threshold
                        status_led.on()
                        time.sleep_ms(100)
                        status_led.off()
                    
                    time.sleep_ms(500)
            
            _thread.start_new_thread(monitor, ())
            """);
    }

    [Task]
    public async Task<SensorReading> GetCorrectedReadingAsync()
    {
        if (!_isCalibrated)
        {
            await CalibrateAsync(); // Auto-calibrate if needed
        }

        return await ExecuteAsync<SensorReading>("""
            raw_reading = sensor.read_u16()
            corrected = raw_reading - calibration_data['offset']
            voltage = corrected * 3.3 / 65536
            
            {
                'timestamp': time.ticks_ms(),
                'raw_value': raw_reading,
                'corrected_value': corrected,
                'voltage': voltage
            }
            """);
    }

    [Teardown]
    public async Task CleanupAsync()
    {
        await ExecuteAsync("""
            # Stop any running threads
            status_led.off()
            print("Sensor cleanup complete")
            """);
    }
}

public record SensorReading(
    int Timestamp, 
    int RawValue, 
    int CorrectedValue, 
    float Voltage
);
```

## Performance Optimization

### Method Caching

Enable intelligent caching for expensive operations:

```csharp
public class OptimizedSensor : Device
{
    [Task(Cache = true, CacheDurationMs = 60000)]  // Cache for 1 minute
    public async Task<string> GetFirmwareVersionAsync()
    {
        return await ExecuteAsync<string>("sys.version");
    }

    [Task(Cache = true, CacheKey = "device_info")]  // Custom cache key
    public async Task<DeviceInfo> GetDeviceInfoAsync()
    {
        return await ExecuteAsync<DeviceInfo>("""
            import os
            {
                'platform': sys.platform,
                'version': sys.version,
                'free_memory': gc.mem_free(),
                'flash_size': os.statvfs('/')[1] * os.statvfs('/')[2]
            }
            """);
    }
}
```

### Batch Operations

Combine multiple operations for efficiency:

```csharp
public class BatchSensor : Device
{
    [Task]
    public async Task<SensorSnapshot> ReadAllAsync()
    {
        // Single device round-trip for multiple readings
        return await ExecuteAsync<SensorSnapshot>("""
            import time
            timestamp = time.ticks_ms()
            
            {
                'timestamp': timestamp,
                'temperature': temp_sensor.read(),
                'humidity': humidity_sensor.read(),
                'pressure': pressure_sensor.read(),
                'battery': battery.voltage()
            }
            """);
    }

    [Task]
    public async Task ConfigureAllAsync(SensorConfiguration config)
    {
        await ExecuteAsync($"""
            config = {JsonSerializer.Serialize(config)}
            
            # Configure all sensors in one operation
            temp_sensor.set_resolution(config['temp_resolution'])
            humidity_sensor.set_precision(config['humidity_precision']) 
            pressure_sensor.set_oversampling(config['pressure_oversampling'])
            
            print("All sensors configured")
            """);
    }
}
```

## Best Practices

### Design Principles

1. **Single Responsibility**: Each method should have one clear purpose
2. **Fail Fast**: Validate parameters and state early
3. **Resource Cleanup**: Always implement teardown for resource management
4. **Error Context**: Provide meaningful error messages and context

### Performance Tips

1. **Batch Operations**: Combine multiple device calls when possible
2. **Cache Expensive Operations**: Use caching for device info, calibration data
3. **Minimize Round Trips**: Execute complex logic on the device, not in multiple calls
4. **Background Processing**: Use `[Thread]` for monitoring and data collection

### Error Handling

1. **Graceful Degradation**: Handle device errors without crashing the application
2. **Retry Logic**: Use built-in retry mechanisms for transient failures
3. **Custom Exceptions**: Create domain-specific exceptions for better error handling
4. **Logging**: Include sufficient context in error logs for debugging

## Real-World Example

Here's a complete example of a weather station using all attribute types:

```csharp
public class WeatherStation : Device
{
    private bool _isMonitoring;

    [Setup]
    public async Task InitializeAsync()
    {
        await ExecuteAsync("""
            from machine import Pin, I2C, ADC
            import time
            import _thread
            
            # Initialize sensors
            i2c = I2C(0, scl=Pin(1), sda=Pin(0))
            temp_sensor = ADC(Pin(26))
            wind_sensor = Pin(2, Pin.IN, Pin.PULL_UP)
            
            # Data storage
            weather_data = []
            monitoring = False
            
            print("Weather station initialized")
            """);
    }

    [Task(Cache = true, CacheDurationMs = 300000)]  // Cache for 5 minutes
    public async Task<WeatherCalibration> CalibrateAsync()
    {
        return await ExecuteAsync<WeatherCalibration>("""
            # Calibration procedure
            temp_readings = [temp_sensor.read_u16() for _ in range(50)]
            temp_offset = sum(temp_readings) / len(temp_readings)
            
            {
                'temperature_offset': temp_offset,
                'calibration_time': time.time(),
                'status': 'calibrated'
            }
            """);
    }

    [Thread]
    public async Task StartMonitoringAsync(int intervalMs = 30000)
    {
        _isMonitoring = true;
        await ExecuteAsync($"""
            def collect_weather_data():
                global monitoring
                monitoring = True
                
                while monitoring:
                    timestamp = time.time()
                    temp_raw = temp_sensor.read_u16()
                    temp_celsius = (temp_raw * 3.3 / 65536 - 0.5) * 100
                    
                    # Simple wind detection
                    wind_count = 0
                    for _ in range(10):
                        if not wind_sensor.value():
                            wind_count += 1
                        time.sleep_ms(10)
                    
                    weather_data.append({{
                        'timestamp': timestamp,
                        'temperature': temp_celsius,
                        'wind_activity': wind_count / 10
                    }})
                    
                    # Keep only last 100 readings
                    if len(weather_data) > 100:
                        weather_data.pop(0)
                    
                    time.sleep_ms({intervalMs})
            
            _thread.start_new_thread(collect_weather_data, ())
            """);
    }

    [Task]
    public async Task<List<WeatherReading>> GetWeatherDataAsync()
    {
        return await ExecuteAsync<List<WeatherReading>>("weather_data");
    }

    [Task]
    public async Task<WeatherSummary> GetCurrentConditionsAsync()
    {
        return await ExecuteAsync<WeatherSummary>("""
            if not weather_data:
                None
            else:
                recent = weather_data[-10:]  # Last 10 readings
                avg_temp = sum(r['temperature'] for r in recent) / len(recent)
                avg_wind = sum(r['wind_activity'] for r in recent) / len(recent)
                
                {
                    'current_temperature': weather_data[-1]['temperature'],
                    'average_temperature': avg_temp,
                    'wind_activity': avg_wind,
                    'readings_count': len(weather_data),
                    'last_update': weather_data[-1]['timestamp']
                }
            """);
    }

    [Task]
    public async Task StopMonitoringAsync()
    {
        _isMonitoring = false;
        await ExecuteAsync("monitoring = False");
    }

    [Teardown]
    public async Task ShutdownAsync()
    {
        if (_isMonitoring)
        {
            await StopMonitoringAsync();
        }

        await ExecuteAsync("""
            monitoring = False
            print("Weather station shutdown complete")
            """);
    }
}

// Supporting types
public record WeatherCalibration(float TemperatureOffset, long CalibrationTime, string Status);
public record WeatherReading(long Timestamp, float Temperature, float WindActivity);
public record WeatherSummary(float CurrentTemperature, float AverageTemperature, float WindActivity, int ReadingsCount, long LastUpdate);

// Usage
var station = new WeatherStation();
await station.ConnectAsync("COM3");

// Automatic setup and calibration
await station.CalibrateAsync();
await station.StartMonitoringAsync(intervalMs: 60000); // Every minute

// Read current conditions
var conditions = await station.GetCurrentConditionsAsync();
Console.WriteLine($"Temperature: {conditions.CurrentTemperature:F1}°C");

// Get historical data
var history = await station.GetWeatherDataAsync();
Console.WriteLine($"Collected {history.Count} readings");

// Automatic cleanup on disposal
```

## Next Steps

- **[Dependency Injection](./dependency-injection)** - Use attributes with DI containers
- **[Examples](../examples/)** - See practical attribute implementations  
- **[Testing](./testing)** - Test attributed methods effectively
- **[Hardware Guides](../hardware/)** - Device-specific attribute patterns