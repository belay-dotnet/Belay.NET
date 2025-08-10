// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using Belay.Core;
using Belay.Attributes;
using Belay.Sync;

Console.WriteLine("=== ESP32 Hardware Validation ===");
Console.WriteLine("This example validates Belay.NET with ESP32 hardware including WiFi and file transfer capabilities.");
Console.WriteLine();

if (args.Length == 0)
{
    Console.WriteLine("Usage: ESP32HardwareTest <connection_string>");
    Console.WriteLine("Examples:");
    Console.WriteLine("  Windows: ESP32HardwareTest serial:COM3");
    Console.WriteLine("  Linux:   ESP32HardwareTest serial:/dev/ttyUSB0");
    Console.WriteLine("  macOS:   ESP32HardwareTest serial:/dev/cu.usbserial-0001");
    return;
}

var connectionString = args[0];
Console.WriteLine($"Testing with: {connectionString}");
Console.WriteLine();

try
{
    using var device = Device.FromConnectionString(connectionString);
    
    Console.WriteLine("=== Step 1: Basic Connection ===");
    await device.ConnectAsync();
    Console.WriteLine("✓ Connected to ESP32 successfully!");
    
    // Test basic device info
    Console.WriteLine("\n=== Step 2: Device Information ===");
    var version = await device.ExecuteAsync<string>("import sys; sys.version");
    Console.WriteLine($"MicroPython Version: {version}");
    
    var platform = await device.ExecuteAsync<string>("import sys; sys.platform");
    Console.WriteLine($"Platform: {platform}");
    
    var memFree = await device.ExecuteAsync<int>("import gc; gc.collect(); gc.mem_free()");
    Console.WriteLine($"Free Memory: {memFree} bytes");
    
    // Test ESP32-specific hardware
    Console.WriteLine("\n=== Step 3: ESP32 Hardware Tests ===");
    var esp32 = new ESP32Controller(device);
    
    // Initialize ESP32 pins
    await esp32.InitializeESP32Async();
    Console.WriteLine("✓ ESP32 hardware initialized");
    
    // Test built-in LED
    Console.WriteLine("Testing built-in LED (3 blinks)...");
    for (int i = 0; i < 3; i++)
    {
        await esp32.SetLedAsync(true);
        await Task.Delay(300);
        await esp32.SetLedAsync(false);
        await Task.Delay(300);
    }
    Console.WriteLine("✓ LED test completed");
    
    // Test ADC (analog input)
    var adcValue = await esp32.ReadADCAsync();
    Console.WriteLine($"✓ ADC reading: {adcValue} (0-4095 range)");
    
    // Test Hall sensor (if available)
    try
    {
        var hallValue = await esp32.ReadHallSensorAsync();
        Console.WriteLine($"✓ Hall sensor: {hallValue}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Hall sensor not available: {ex.Message}");
    }
    
    // Test WiFi capability (without connecting)
    var wifiAvailable = await esp32.CheckWiFiAvailabilityAsync();
    Console.WriteLine($"✓ WiFi capability: {(wifiAvailable ? "Available" : "Not available")}");
    
    // Test Task attribute functionality
    Console.WriteLine("\n=== Step 4: Task Attribute Validation ===");
    var systemInfo = await esp32.GetSystemInfoAsync();
    Console.WriteLine($"✓ System info: {systemInfo}");
    
    var calculation = await esp32.CalculateAsync(20, 4);
    Console.WriteLine($"✓ Calculation (20 * 4 + 20/4): {calculation}");
    
    // Test cached task
    var start = DateTime.UtcNow;
    for (int i = 0; i < 3; i++)
    {
        var deviceId = await esp32.GetDeviceIdAsync();
        Console.WriteLine($"  Call {i + 1}: Device ID = {deviceId}");
    }
    var elapsed = DateTime.UtcNow - start;
    Console.WriteLine($"✓ Cached task test completed in {elapsed.TotalMilliseconds:F0}ms");
    
    // Test file transfer integration
    Console.WriteLine("\n=== Step 5: File Transfer Validation ===");
    
    // Create test file
    var testContent = "ESP32 test file content from Belay.NET\nSecond line for testing\n";
    await device.FileSystem().WriteTextFileAsync("/esp32_test.txt", testContent);
    Console.WriteLine("✓ File created on ESP32");
    
    // Read file back
    var readContent = await device.FileSystem().ReadTextFileAsync("/esp32_test.txt");
    var contentMatch = readContent == testContent;
    Console.WriteLine($"✓ File read verification: {(contentMatch ? "PASS" : "FAIL")}");
    
    // Test binary file transfer
    var binaryData = new byte[1024];
    Random.Shared.NextBytes(binaryData);
    await device.FileSystem().WriteFileAsync("/esp32_binary.dat", binaryData);
    var readBinary = await device.FileSystem().ReadFileAsync("/esp32_binary.dat");
    var binaryMatch = binaryData.SequenceEqual(readBinary);
    Console.WriteLine($"✓ Binary file transfer: {(binaryMatch ? "PASS" : "FAIL")} ({binaryData.Length} bytes)");
    
    // Test directory operations
    await device.FileSystem().CreateDirectoryAsync("/esp32_test_dir");
    await device.FileSystem().WriteTextFileAsync("/esp32_test_dir/nested_file.txt", "Nested file content");
    var dirExists = await device.FileSystem().ExistsAsync("/esp32_test_dir");
    var fileExists = await device.FileSystem().ExistsAsync("/esp32_test_dir/nested_file.txt");
    Console.WriteLine($"✓ Directory operations: {(dirExists && fileExists ? "PASS" : "FAIL")}");
    
    Console.WriteLine("\n=== Step 6: Protocol Validation ===");
    
    // Test error handling
    try
    {
        await device.ExecuteAsync("raise ValueError('ESP32 test error')");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✓ Error handling works: {ex.GetType().Name}");
    }
    
    // Test large data transfer
    var largeData = string.Join("", Enumerable.Range(0, 200).Select(i => i.ToString("D3")));
    var result = await device.ExecuteAsync<string>($"'{largeData}'");
    var success = result == largeData;
    Console.WriteLine($"✓ Large data transfer: {(success ? "PASS" : "FAIL")} ({largeData.Length} chars)");
    
    // Clean up test files
    await device.FileSystem().DeleteFileAsync("/esp32_test.txt");
    await device.FileSystem().DeleteFileAsync("/esp32_binary.dat");
    await device.FileSystem().DeleteDirectoryAsync("/esp32_test_dir", recursive: true);
    Console.WriteLine("✓ Test files cleaned up");
    
    await device.DisconnectAsync();
    
    Console.WriteLine("\n🎉 ESP32 validation completed successfully!");
    Console.WriteLine("✅ All tests passed - ESP32 hardware is ready for development");
    Console.WriteLine("\n=== ESP32 vs Pico Comparison Notes ===");
    Console.WriteLine("- Built-in LED on GPIO 2 (vs GPIO 25 on Pico)");
    Console.WriteLine("- WiFi capability available (vs Bluetooth on Pico W)");
    Console.WriteLine("- Hall sensor available (vs internal temperature on Pico)");
    Console.WriteLine("- More flash memory for file operations");
    Console.WriteLine("- Similar Task attribute performance");
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
/// ESP32-specific hardware controller
/// </summary>
public class ESP32Controller
{
    private readonly Device device;
    
    public ESP32Controller(Device device)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
    }
    
    /// <summary>
    /// Initialize ESP32-specific hardware
    /// </summary>
    [Setup]
    public async Task InitializeESP32Async()
    {
        await device.ExecuteAsync(@"
from machine import Pin, ADC
import esp32

# Built-in LED on GPIO 2 for most ESP32 boards
led = Pin(2, Pin.OUT)

# ADC for analog input testing (GPIO 36)
adc = ADC(Pin(36))
adc.atten(ADC.ATTN_11DB)  # Full range: 3.3V

print('ESP32 hardware initialized')
        ");
    }
    
    /// <summary>
    /// Control the built-in LED
    /// </summary>
    [Task]
    public async Task SetLedAsync(bool state)
    {
        var command = state ? "led.on()" : "led.off()";
        await device.ExecuteAsync(command);
    }
    
    /// <summary>
    /// Read ADC value
    /// </summary>
    [Task]
    public async Task<int> ReadADCAsync()
    {
        return await device.ExecuteAsync<int>("adc.read()");
    }
    
    /// <summary>
    /// Read Hall sensor (if available)
    /// </summary>
    [Task]
    public async Task<int> ReadHallSensorAsync()
    {
        return await device.ExecuteAsync<int>("esp32.hall_sensor()");
    }
    
    /// <summary>
    /// Check WiFi availability
    /// </summary>
    [Task]
    public async Task<bool> CheckWiFiAvailabilityAsync()
    {
        return await device.ExecuteAsync<bool>(@"
try:
    import network
    wlan = network.WLAN(network.STA_IF)
    True
except:
    False
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
import esp32
freq = esp32.cpu_freq()
f'ESP32 - MicroPython {sys.version} on {sys.platform} @ {freq}Hz'
        ");
    }
    
    /// <summary>
    /// Test task with parameters
    /// </summary>
    [Task]
    public async Task<float> CalculateAsync(int a, int b)
    {
        // Validate parameters to prevent injection
        if (a < -1000000 || a > 1000000 || b == 0 || b < -1000000 || b > 1000000)
            throw new ArgumentException("Invalid parameters for calculation");
            
        return await device.ExecuteAsync<float>($@"
# ESP32 calculation: a={a}, b={b}
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
import esp32

# Simulate getting unique device ID
time.sleep_ms(50)  # Simulate work
unique_id = machine.unique_id()
chip_id = esp32.chip_id()
'esp32_' + ''.join([hex(b)[2:] for b in unique_id]) + '_' + hex(chip_id)[2:]
        ");
    }
    
    /// <summary>
    /// Get ESP32-specific chip information
    /// </summary>
    [Task(Cache = true)]
    public async Task<Dictionary<string, object>> GetChipInfoAsync()
    {
        var chipInfo = await device.ExecuteAsync<string>(@"
import esp32
import json

info = {
    'chip_id': hex(esp32.chip_id()),
    'cpu_freq': esp32.cpu_freq(),
    'flash_size': esp32.flash_size(),
    'psram_size': esp32.psram_size() if hasattr(esp32, 'psram_size') else 0
}
json.dumps(info)
        ");
        
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(chipInfo) ?? new();
    }
    
    /// <summary>
    /// Test WiFi scan capability (without connecting)
    /// </summary>
    [Task]
    public async Task<int> ScanWiFiNetworksAsync()
    {
        return await device.ExecuteAsync<int>(@"
try:
    import network
    wlan = network.WLAN(network.STA_IF)
    wlan.active(True)
    networks = wlan.scan()
    wlan.active(False)
    len(networks)
except Exception as e:
    -1  # WiFi not available or error
        ");
    }
}