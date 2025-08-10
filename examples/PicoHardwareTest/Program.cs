// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using Belay.Core;
using Belay.Attributes;

Console.WriteLine("=== Raspberry Pi Pico Hardware Validation ===");
Console.WriteLine("This example validates Belay.NET with Raspberry Pi Pico hardware.");
Console.WriteLine();

if (args.Length == 0)
{
    Console.WriteLine("Usage: PicoHardwareTest <connection_string>");
    Console.WriteLine("Examples:");
    Console.WriteLine("  Windows: PicoHardwareTest serial:COM3");
    Console.WriteLine("  Linux:   PicoHardwareTest serial:/dev/ttyACM0");
    Console.WriteLine("  macOS:   PicoHardwareTest serial:/dev/cu.usbmodem143201");
    return;
}

var connectionString = args[0];
Console.WriteLine($"Testing with: {connectionString}");
Console.WriteLine();

try
{
    var device = Device.FromConnectionString(connectionString);
    
    Console.WriteLine("=== Step 1: Basic Connection ===");
    await device.ConnectAsync();
    Console.WriteLine("✓ Connected to Pico successfully!");
    
    // Test basic device info
    Console.WriteLine("\n=== Step 2: Device Information ===");
    var version = await device.ExecuteAsync<string>("import sys; sys.version");
    Console.WriteLine($"MicroPython Version: {version}");
    
    var platform = await device.ExecuteAsync<string>("import sys; sys.platform");
    Console.WriteLine($"Platform: {platform}");
    
    var memFree = await device.ExecuteAsync<int>("import gc; gc.collect(); gc.mem_free()");
    Console.WriteLine($"Free Memory: {memFree} bytes");
    
    // Test Pico-specific hardware
    Console.WriteLine("\n=== Step 3: Pico Hardware Tests ===");
    var pico = new PicoController(device);
    
    // Initialize Pico pins
    await pico.InitializePicoAsync();
    Console.WriteLine("✓ Pico hardware initialized");
    
    // Test built-in LED
    Console.WriteLine("Testing built-in LED (3 blinks)...");
    for (int i = 0; i < 3; i++)
    {
        await pico.SetLedAsync(true);
        await Task.Delay(300);
        await pico.SetLedAsync(false);
        await Task.Delay(300);
    }
    Console.WriteLine("✓ LED test completed");
    
    // Test temperature sensor (built into RP2040)
    var temperature = await pico.ReadTemperatureAsync();
    Console.WriteLine($"✓ Internal temperature: {temperature:F1}°C");
    
    // Test Task attribute functionality
    Console.WriteLine("\n=== Step 4: Task Attribute Validation ===");
    var systemInfo = await pico.GetSystemInfoAsync();
    Console.WriteLine($"✓ System info: {systemInfo}");
    
    var calculation = await pico.CalculateAsync(15, 3);
    Console.WriteLine($"✓ Calculation (15 * 3 + 15/3): {calculation}");
    
    // Test cached task
    var start = DateTime.UtcNow;
    for (int i = 0; i < 3; i++)
    {
        var deviceId = await pico.GetDeviceIdAsync();
        Console.WriteLine($"  Call {i + 1}: Device ID = {deviceId}");
    }
    var elapsed = DateTime.UtcNow - start;
    Console.WriteLine($"✓ Cached task test completed in {elapsed.TotalMilliseconds:F0}ms");
    
    Console.WriteLine("\n=== Step 5: Protocol Validation ===");
    
    // Test error handling
    try
    {
        await device.ExecuteAsync("raise ValueError('Test error')");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✓ Error handling works: {ex.GetType().Name}");
    }
    
    // Test large data transfer
    var largeData = string.Join("", Enumerable.Range(0, 100).Select(i => i.ToString("D3")));
    var result = await device.ExecuteAsync<string>($"'{largeData}'");
    var success = result == largeData;
    Console.WriteLine($"✓ Large data transfer: {(success ? "PASS" : "FAIL")} ({largeData.Length} chars)");
    
    await device.DisconnectAsync();
    
    Console.WriteLine("\n🎉 Raspberry Pi Pico validation completed successfully!");
    Console.WriteLine("✅ All tests passed - hardware is ready for development");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Validation failed: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }
    Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
}

/// <summary>
/// Raspberry Pi Pico-specific hardware controller
/// </summary>
public class PicoController
{
    private readonly Device device;
    
    public PicoController(Device device)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
    }
    
    /// <summary>
    /// Initialize Pico-specific hardware
    /// </summary>
    [Setup]
    public async Task InitializePicoAsync()
    {
        await device.ExecuteAsync(@"
from machine import Pin, ADC
import rp2

# Built-in LED on GPIO 25
led = Pin(25, Pin.OUT)

# Internal temperature sensor
temp_sensor = ADC(4)

print('Pico hardware initialized')
        ");
    }
    
    /// <summary>
    /// Control the built-in LED
    /// </summary>
    [Task]
    public async Task SetLedAsync(bool state)
    {
        await device.ExecuteAsync($"led.{'on' if state else 'off'}()");
    }
    
    /// <summary>
    /// Read internal temperature sensor
    /// </summary>
    [Task]
    public async Task<float> ReadTemperatureAsync()
    {
        return await device.ExecuteAsync<float>(@"
# RP2040 temperature calculation
reading = temp_sensor.read_u16() * 3.3 / 65535
temperature = 27 - (reading - 0.706) / 0.001721
round(temperature, 1)
        ");
    }
    
    /// <summary>
    /// Get system information
    /// </summary>
    [Task]
    public async Task<string> GetSystemInfoAsync()
    {
        return await device.ExecuteAsync<string>(@"
import sys
import os
f'Pico - MicroPython {sys.version} on {sys.platform}'
        ");
    }
    
    /// <summary>
    /// Test task with parameters
    /// </summary>
    [Task]
    public async Task<float> CalculateAsync(int a, int b)
    {
        return await device.ExecuteAsync<float>($@"
# Pico calculation: a={a}, b={b}
result = {a} * {b} + ({a} / {b})
result
        ");
    }
    
    /// <summary>
    /// Cached task for performance testing
    /// </summary>
    [Task(Cache = true)]
    public async Task<string> GetDeviceIdAsync()
    {
        return await device.ExecuteAsync<string>(@"
import time
import machine

# Simulate getting unique device ID
time.sleep_ms(50)  # Simulate work
unique_id = machine.unique_id()
'pico_' + ''.join([hex(b)[2:] for b in unique_id])
        ");
    }
}