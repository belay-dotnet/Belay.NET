// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using Belay.Core;
using Belay.Attributes;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== Belay.NET Task Attribute MVP Example ===");
Console.WriteLine("This example demonstrates the basic [Task] attribute functionality.");
Console.WriteLine();

try
{
    // Create device connection - supports both subprocess and hardware
    var connectionString = args.Length > 0 
        ? args[0] 
        : "subprocess:../../micropython/ports/unix/build-standard/micropython";
        
    Console.WriteLine($"Connection string: {connectionString}");
    var device = Device.FromConnectionString(connectionString, loggerFactory: null);
    
    Console.WriteLine("Connecting to MicroPython device...");
    await device.ConnectAsync();
    
    Console.WriteLine("✓ Connected successfully!");
    Console.WriteLine();
    
    // Create a sensor controller instance
    var sensor = new TemperatureSensor(device);
    
    // Example 1: Simple task with no parameters
    Console.WriteLine("=== Example 1: Simple Task ===");
    var systemInfo = await sensor.GetSystemInfoAsync();
    Console.WriteLine($"Device: {systemInfo}");
    Console.WriteLine();
    
    // Example 2: Task with parameters
    Console.WriteLine("=== Example 2: Task with Parameters ===");
    var calculation = await sensor.CalculateAsync(10, 5);
    Console.WriteLine($"Calculation result: {calculation}");
    Console.WriteLine();
    
    // Example 3: Task with complex return type
    Console.WriteLine("=== Example 3: Task with Complex Return Type ===");
    var sensorData = await sensor.ReadSensorDataAsync(26);
    Console.WriteLine($"Sensor Data:");
    Console.WriteLine($"  Temperature: {sensorData.Temperature:F2}°C");
    Console.WriteLine($"  Voltage: {sensorData.Voltage:F3}V");
    Console.WriteLine($"  Raw Reading: {sensorData.RawReading}");
    Console.WriteLine($"  Timestamp: {sensorData.Timestamp}");
    Console.WriteLine();
    
    // Example 4: Cached task (called multiple times)
    Console.WriteLine("=== Example 4: Cached Task (called 3 times) ===");
    var start = DateTime.UtcNow;
    for (int i = 0; i < 3; i++)
    {
        var deviceId = await sensor.GetDeviceIdAsync();
        Console.WriteLine($"Call {i + 1}: Device ID = {deviceId}");
    }
    var elapsed = DateTime.UtcNow - start;
    Console.WriteLine($"Total time: {elapsed.TotalMilliseconds:F0}ms (caching should make subsequent calls faster)");
    Console.WriteLine();
    
    // Example 5: Exclusive task
    Console.WriteLine("=== Example 5: Exclusive Task ===");
    await sensor.CriticalOperationAsync();
    Console.WriteLine("Critical operation completed");
    Console.WriteLine();
    
    await device.DisconnectAsync();
    Console.WriteLine("✓ All examples completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }
}

/// <summary>
/// Example class demonstrating various Task attribute scenarios
/// </summary>
public class TemperatureSensor
{
    private readonly Device device;
    
    public TemperatureSensor(Device device)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
    }
    
    /// <summary>
    /// Simple task with no parameters - gets basic system information
    /// </summary>
    [Task]
    public async Task<string> GetSystemInfoAsync()
    {
        return await device.ExecuteAsync<string>(@"
import sys
f'MicroPython {sys.version} on {sys.platform}'
        ");
    }
    
    /// <summary>
    /// Task with parameters - demonstrates parameter marshaling
    /// </summary>
    [Task]
    public async Task<float> CalculateAsync(int a, int b)
    {
        return await device.ExecuteAsync<float>($@"
# Parameters: a={a}, b={b}
result = {a} * {b} + ({a} / {b})
result
        ");
    }
    
    /// <summary>
    /// Task with complex return type - demonstrates JSON deserialization
    /// </summary>
    [Task]
    public async Task<SensorReading> ReadSensorDataAsync(int pin)
    {
        return await device.ExecuteAsync<SensorReading>($@"
import json
import time

# Simulate reading from ADC pin {pin}
raw_reading = 32768 + (time.ticks_ms() % 1000)  # Simulate varying reading
voltage = raw_reading * 3.3 / 65535
temperature = 27 - (voltage - 0.706) / 0.001721

# Return as JSON for automatic deserialization
json.dumps({{
    'temperature': round(temperature, 2),
    'voltage': round(voltage, 3), 
    'rawReading': raw_reading,
    'timestamp': time.ticks_ms()
}})
        ");
    }
    
    /// <summary>
    /// Cached task - should be faster on subsequent calls
    /// </summary>
    [Task(Cache = true)]
    public async Task<string> GetDeviceIdAsync()
    {
        return await device.ExecuteAsync<string>(@"
import time
# Simulate expensive operation
time.sleep_ms(100)  
# Return simulated device ID
'device_12345'
        ");
    }
    
    /// <summary>
    /// Exclusive task - prevents concurrent execution
    /// </summary>
    [Task(Exclusive = true)]
    public async Task CriticalOperationAsync()
    {
        await device.ExecuteAsync(@"
import time
# Simulate critical operation that cannot be interrupted
for i in range(3):
    print(f'Critical step {i+1}/3')
    time.sleep_ms(200)
print('Critical operation completed')
        ");
    }
}

/// <summary>
/// Data transfer object for sensor readings
/// </summary>
public class SensorReading
{
    public float Temperature { get; set; }
    public float Voltage { get; set; }
    public int RawReading { get; set; }
    public long Timestamp { get; set; }
}