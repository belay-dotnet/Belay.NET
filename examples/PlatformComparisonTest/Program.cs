// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using Belay.Core;
using Belay.Attributes;
using Belay.Sync;

Console.WriteLine("=== Platform Comparison Test ===");
Console.WriteLine("This example compares ESP32 and Raspberry Pi Pico capabilities side-by-side.");
Console.WriteLine();

if (args.Length < 2)
{
    Console.WriteLine("Usage: PlatformComparisonTest <esp32_connection> <pico_connection>");
    Console.WriteLine("Examples:");
    Console.WriteLine("  Linux:   PlatformComparisonTest serial:/dev/ttyUSB0 serial:/dev/ttyACM0");
    Console.WriteLine("  Windows: PlatformComparisonTest serial:COM3 serial:COM4");
    Console.WriteLine();
    Console.WriteLine("Note: Connect ESP32 first, then Pico, or adjust connection strings accordingly.");
    return;
}

var esp32Connection = args[0];
var picoConnection = args[1];

Console.WriteLine($"ESP32: {esp32Connection}");
Console.WriteLine($"Pico:  {picoConnection}");
Console.WriteLine();

var results = new PlatformComparisonResults();

try
{
    Console.WriteLine("=== Connecting to Devices ===");
    
    using var esp32Device = Device.FromConnectionString(esp32Connection);
    using var picoDevice = Device.FromConnectionString(picoConnection);
    
    await esp32Device.ConnectAsync();
    Console.WriteLine("✓ ESP32 connected");
    
    await picoDevice.ConnectAsync();
    Console.WriteLine("✓ Pico connected");
    
    var esp32Controller = new ESP32Controller(esp32Device);
    var picoController = new PicoController(picoDevice);
    
    // Initialize both platforms
    Console.WriteLine("\n=== Platform Initialization ===");
    await esp32Controller.InitializeESP32Async();
    await picoController.InitializePicoAsync();
    Console.WriteLine("✓ Both platforms initialized");
    
    // Compare basic device information
    Console.WriteLine("\n=== Device Information Comparison ===");
    
    var esp32Info = await esp32Controller.GetSystemInfoAsync();
    var picoInfo = await picoController.GetSystemInfoAsync();
    
    Console.WriteLine($"ESP32: {esp32Info}");
    Console.WriteLine($"Pico:  {picoInfo}");
    
    results.ESP32Info = esp32Info;
    results.PicoInfo = picoInfo;
    
    // Compare memory
    var esp32Memory = await esp32Device.ExecuteAsync<int>("import gc; gc.collect(); gc.mem_free()");
    var picoMemory = await picoDevice.ExecuteAsync<int>("import gc; gc.collect(); gc.mem_free()");
    
    Console.WriteLine($"\nMemory (free bytes):");
    Console.WriteLine($"  ESP32: {esp32Memory:N0}");
    Console.WriteLine($"  Pico:  {picoMemory:N0}");
    Console.WriteLine($"  Ratio: {(double)esp32Memory / picoMemory:F2}x");
    
    results.ESP32Memory = esp32Memory;
    results.PicoMemory = picoMemory;
    
    // Compare LED performance
    Console.WriteLine("\n=== LED Performance Test ===");
    
    var esp32LedTime = await MeasureTaskPerformance(() => esp32Controller.SetLedAsync(true));
    var picoLedTime = await MeasureTaskPerformance(() => picoController.SetLedAsync(true));
    
    // Turn off LEDs
    await esp32Controller.SetLedAsync(false);
    await picoController.SetLedAsync(false);
    
    Console.WriteLine($"LED Control Time:");
    Console.WriteLine($"  ESP32: {esp32LedTime}ms");
    Console.WriteLine($"  Pico:  {picoLedTime}ms");
    Console.WriteLine($"  Difference: {Math.Abs(esp32LedTime - picoLedTime)}ms");
    
    results.ESP32LedTime = esp32LedTime;
    results.PicoLedTime = picoLedTime;
    
    // Compare calculation performance
    Console.WriteLine("\n=== Calculation Performance Test ===");
    
    var esp32CalcTime = await MeasureTaskPerformanceWithResult(() => esp32Controller.CalculateAsync(50, 10));
    var picoCalcTime = await MeasureTaskPerformanceWithResult(() => picoController.CalculateAsync(50, 10));
    
    Console.WriteLine($"Calculation Time:");
    Console.WriteLine($"  ESP32: {esp32CalcTime}ms");
    Console.WriteLine($"  Pico:  {picoCalcTime}ms");
    Console.WriteLine($"  Performance: {(picoCalcTime > esp32CalcTime ? "ESP32" : "Pico")} faster by {Math.Abs(esp32CalcTime - picoCalcTime)}ms");
    
    results.ESP32CalcTime = esp32CalcTime;
    results.PicoCalcTime = picoCalcTime;
    
    // Compare sensor capabilities
    Console.WriteLine("\n=== Sensor Capabilities ===");
    
    try
    {
        var esp32Adc = await esp32Controller.ReadADCAsync();
        Console.WriteLine($"ESP32 ADC: {esp32Adc} (12-bit: 0-4095)");
        results.ESP32SensorValue = esp32Adc;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ESP32 ADC: Error - {ex.Message}");
    }
    
    try
    {
        var picoTemp = await picoController.ReadTemperatureAsync();
        Console.WriteLine($"Pico Temperature: {picoTemp:F1}°C");
        results.PicoSensorValue = picoTemp;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Pico Temperature: Error - {ex.Message}");
    }
    
    // Compare unique capabilities
    Console.WriteLine("\n=== Platform-Specific Features ===");
    
    // ESP32 WiFi
    try
    {
        var wifiAvailable = await esp32Controller.CheckWiFiAvailabilityAsync();
        Console.WriteLine($"ESP32 WiFi: {(wifiAvailable ? "Available" : "Not available")}");
        results.ESP32WiFiAvailable = wifiAvailable;
        
        if (wifiAvailable)
        {
            var networkCount = await esp32Controller.ScanWiFiNetworksAsync();
            if (networkCount >= 0)
            {
                Console.WriteLine($"ESP32 WiFi Networks Found: {networkCount}");
                results.ESP32NetworkCount = networkCount;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ESP32 WiFi: Error - {ex.Message}");
    }
    
    // ESP32 chip info
    try
    {
        var chipInfo = await esp32Controller.GetChipInfoAsync();
        if (chipInfo.ContainsKey("cpu_freq"))
        {
            Console.WriteLine($"ESP32 CPU Frequency: {chipInfo["cpu_freq"]} Hz");
            results.ESP32CpuFreq = Convert.ToInt32(chipInfo["cpu_freq"]);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ESP32 Chip Info: Error - {ex.Message}");
    }
    
    // Compare file transfer performance
    Console.WriteLine("\n=== File Transfer Performance ===");
    
    var testData = new byte[2048]; // 2KB test file
    Random.Shared.NextBytes(testData);
    
    var esp32FileTime = await MeasureTaskPerformance(async () =>
    {
        await esp32Device.FileSystem().WriteFileAsync("/perf_test.bin", testData);
        await esp32Device.FileSystem().ReadFileAsync("/perf_test.bin");
        await esp32Device.FileSystem().DeleteFileAsync("/perf_test.bin");
    });
    
    var picoFileTime = await MeasureTaskPerformance(async () =>
    {
        await picoDevice.FileSystem().WriteFileAsync("/perf_test.bin", testData);
        await picoDevice.FileSystem().ReadFileAsync("/perf_test.bin");
        await picoDevice.FileSystem().DeleteFileAsync("/perf_test.bin");
    });
    
    Console.WriteLine($"File Transfer (2KB write+read+delete):");
    Console.WriteLine($"  ESP32: {esp32FileTime}ms");
    Console.WriteLine($"  Pico:  {picoFileTime}ms");
    Console.WriteLine($"  Performance: {(picoFileTime > esp32FileTime ? "ESP32" : "Pico")} faster by {Math.Abs(esp32FileTime - picoFileTime)}ms");
    
    results.ESP32FileTime = esp32FileTime;
    results.PicoFileTime = picoFileTime;
    
    // Test cached task performance
    Console.WriteLine("\n=== Cached Task Performance ===");
    
    // First calls (not cached)
    var esp32CacheTime1 = await MeasureTaskPerformanceWithResult(() => esp32Controller.GetDeviceIdAsync());
    var picoCacheTime1 = await MeasureTaskPerformanceWithResult(() => picoController.GetDeviceIdAsync());
    
    // Second calls (cached)
    var esp32CacheTime2 = await MeasureTaskPerformanceWithResult(() => esp32Controller.GetDeviceIdAsync());
    var picoCacheTime2 = await MeasureTaskPerformanceWithResult(() => picoController.GetDeviceIdAsync());
    
    Console.WriteLine($"Device ID (first call - not cached):");
    Console.WriteLine($"  ESP32: {esp32CacheTime1}ms");
    Console.WriteLine($"  Pico:  {picoCacheTime1}ms");
    
    Console.WriteLine($"Device ID (second call - cached):");
    Console.WriteLine($"  ESP32: {esp32CacheTime2}ms");
    Console.WriteLine($"  Pico:  {picoCacheTime2}ms");
    
    Console.WriteLine($"Cache speedup:");
    Console.WriteLine($"  ESP32: {(double)esp32CacheTime1 / esp32CacheTime2:F1}x faster");
    Console.WriteLine($"  Pico:  {(double)picoCacheTime1 / picoCacheTime2:F1}x faster");
    
    results.ESP32CacheSpeedup = (double)esp32CacheTime1 / esp32CacheTime2;
    results.PicoCacheSpeedup = (double)picoCacheTime1 / picoCacheTime2;
    
    await esp32Device.DisconnectAsync();
    await picoDevice.DisconnectAsync();
    
    // Generate comparison report
    Console.WriteLine("\n" + "=".PadLeft(50, '='));
    Console.WriteLine("PLATFORM COMPARISON REPORT");
    Console.WriteLine("=".PadLeft(50, '='));
    
    results.PrintSummary();
    
    Console.WriteLine("\n🎉 Platform comparison completed successfully!");
    Console.WriteLine("✅ Both ESP32 and Pico are ready for development");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Platform comparison failed: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }
    Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
}

static async Task<long> MeasureTaskPerformance(Func<Task> taskFunc)
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    await taskFunc();
    stopwatch.Stop();
    return stopwatch.ElapsedMilliseconds;
}

static async Task<long> MeasureTaskPerformanceWithResult<T>(Func<Task<T>> taskFunc)
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    await taskFunc();
    stopwatch.Stop();
    return stopwatch.ElapsedMilliseconds;
}

public class PlatformComparisonResults
{
    public string ESP32Info { get; set; } = "";
    public string PicoInfo { get; set; } = "";
    public int ESP32Memory { get; set; }
    public int PicoMemory { get; set; }
    public long ESP32LedTime { get; set; }
    public long PicoLedTime { get; set; }
    public long ESP32CalcTime { get; set; }
    public long PicoCalcTime { get; set; }
    public int ESP32SensorValue { get; set; }
    public float PicoSensorValue { get; set; }
    public bool ESP32WiFiAvailable { get; set; }
    public int ESP32NetworkCount { get; set; }
    public int ESP32CpuFreq { get; set; }
    public long ESP32FileTime { get; set; }
    public long PicoFileTime { get; set; }
    public double ESP32CacheSpeedup { get; set; }
    public double PicoCacheSpeedup { get; set; }
    
    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("FEATURE COMPARISON:");
        Console.WriteLine($"{"Feature",-25} {"ESP32",-15} {"Pico",-15} {"Winner",-10}");
        Console.WriteLine("-".PadLeft(70, '-'));
        
        Console.WriteLine($"{"Memory (KB)",-25} {ESP32Memory / 1024,-15:N0} {PicoMemory / 1024,-15:N0} {(ESP32Memory > PicoMemory ? "ESP32" : "Pico"),-10}");
        Console.WriteLine($"{"LED Control (ms)",-25} {ESP32LedTime,-15} {PicoLedTime,-15} {(ESP32LedTime < PicoLedTime ? "ESP32" : "Pico"),-10}");
        Console.WriteLine($"{"Calculation (ms)",-25} {ESP32CalcTime,-15} {PicoCalcTime,-15} {(ESP32CalcTime < PicoCalcTime ? "ESP32" : "Pico"),-10}");
        Console.WriteLine($"{"File Transfer (ms)",-25} {ESP32FileTime,-15} {PicoFileTime,-15} {(ESP32FileTime < PicoFileTime ? "ESP32" : "Pico"),-10}");
        Console.WriteLine($"{"Cache Speedup",-25} {ESP32CacheSpeedup,-15:F1}x {PicoCacheSpeedup,-15:F1}x {(ESP32CacheSpeedup > PicoCacheSpeedup ? "ESP32" : "Pico"),-10}");
        
        Console.WriteLine();
        Console.WriteLine("UNIQUE CAPABILITIES:");
        Console.WriteLine($"ESP32: WiFi ({(ESP32WiFiAvailable ? "Available" : "N/A")}), Hall Sensor, Higher CPU Freq ({ESP32CpuFreq / 1000000}MHz)");
        Console.WriteLine($"Pico:  Temperature Sensor ({PicoSensorValue:F1}°C), Lower Power, Better ADC Resolution");
        
        Console.WriteLine();
        Console.WriteLine("DEVELOPMENT RECOMMENDATIONS:");
        
        if (ESP32Memory > PicoMemory * 1.5)
        {
            Console.WriteLine("• ESP32 recommended for memory-intensive applications");
        }
        
        if (ESP32WiFiAvailable)
        {
            Console.WriteLine("• ESP32 recommended for IoT/wireless applications");
        }
        
        if (PicoSensorValue > 0)
        {
            Console.WriteLine("• Pico recommended for temperature monitoring applications");
        }
        
        var esp32Overall = (ESP32LedTime < PicoLedTime ? 1 : 0) + 
                          (ESP32CalcTime < PicoCalcTime ? 1 : 0) + 
                          (ESP32FileTime < PicoFileTime ? 1 : 0) +
                          (ESP32Memory > PicoMemory ? 1 : 0);
        
        Console.WriteLine($"• Overall performance winner: {(esp32Overall >= 2 ? "ESP32" : "Pico")} ({esp32Overall}/4 categories)");
    }
}

/// <summary>
/// ESP32-specific controller for comparison testing
/// </summary>
public class ESP32Controller
{
    private readonly Device device;
    
    public ESP32Controller(Device device)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
    }
    
    [Setup]
    public async Task InitializeESP32Async()
    {
        await device.ExecuteAsync(@"
from machine import Pin, ADC
import esp32
led = Pin(2, Pin.OUT)
adc = ADC(Pin(36))
adc.atten(ADC.ATTN_11DB)
        ");
    }
    
    [Task]
    public async Task SetLedAsync(bool state)
    {
        await device.ExecuteAsync(state ? "led.on()" : "led.off()");
    }
    
    [Task]
    public async Task<int> ReadADCAsync()
    {
        return await device.ExecuteAsync<int>("adc.read()");
    }
    
    [Task]
    public async Task<bool> CheckWiFiAvailabilityAsync()
    {
        return await device.ExecuteAsync<bool>(@"
try:
    import network
    True
except:
    False
        ");
    }
    
    [Task]
    public async Task<string> GetSystemInfoAsync()
    {
        return await device.ExecuteAsync<string>(@"
import sys
import esp32
f'ESP32 - MicroPython {sys.version.split()[0]} @ {esp32.cpu_freq()}Hz'
        ");
    }
    
    [Task]
    public async Task<float> CalculateAsync(int a, int b)
    {
        return await device.ExecuteAsync<float>($"({a} * {b} + {a} / {b})");
    }
    
    [Task(Cache = true)]
    public async Task<string> GetDeviceIdAsync()
    {
        return await device.ExecuteAsync<string>(@"
import machine
import esp32
import time
time.sleep_ms(50)
'esp32_' + hex(esp32.chip_id())[2:]
        ");
    }
    
    [Task(Cache = true)]
    public async Task<Dictionary<string, object>> GetChipInfoAsync()
    {
        var chipInfo = await device.ExecuteAsync<string>(@"
import esp32
import json
info = {'chip_id': hex(esp32.chip_id()), 'cpu_freq': esp32.cpu_freq()}
json.dumps(info)
        ");
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(chipInfo) ?? new();
    }
    
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
except:
    -1
        ");
    }
}

/// <summary>
/// Pico-specific controller for comparison testing
/// </summary>
public class PicoController
{
    private readonly Device device;
    
    public PicoController(Device device)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
    }
    
    [Setup]
    public async Task InitializePicoAsync()
    {
        await device.ExecuteAsync(@"
from machine import Pin, ADC
led = Pin(25, Pin.OUT)
temp_sensor = ADC(4)
        ");
    }
    
    [Task]
    public async Task SetLedAsync(bool state)
    {
        await device.ExecuteAsync(state ? "led.on()" : "led.off()");
    }
    
    [Task]
    public async Task<float> ReadTemperatureAsync()
    {
        return await device.ExecuteAsync<float>(@"
reading = temp_sensor.read_u16() * 3.3 / 65535
temperature = 27 - (reading - 0.706) / 0.001721
round(temperature, 1)
        ");
    }
    
    [Task]
    public async Task<string> GetSystemInfoAsync()
    {
        return await device.ExecuteAsync<string>(@"
import sys
f'Pico - MicroPython {sys.version.split()[0]} on {sys.platform}'
        ");
    }
    
    [Task]
    public async Task<float> CalculateAsync(int a, int b)
    {
        return await device.ExecuteAsync<float>($"({a} * {b} + {a} / {b})");
    }
    
    [Task(Cache = true)]
    public async Task<string> GetDeviceIdAsync()
    {
        return await device.ExecuteAsync<string>(@"
import machine
import time
time.sleep_ms(50)
unique_id = machine.unique_id()
'pico_' + ''.join([hex(b)[2:] for b in unique_id])
        ");
    }
}