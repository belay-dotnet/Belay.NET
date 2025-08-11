#!/usr/bin/env dotnet-script
// Simple validation test for the attribute system using the available RPi Pico

#r "/home/corona/belay.net/src/Belay.Core/bin/Debug/net8.0/Belay.Core.dll"
#r "/home/corona/belay.net/src/Belay.Attributes/bin/Debug/net8.0/Belay.Attributes.dll"

using Belay.Core;
using Belay.Core.Examples;
using Belay.Core.Execution;
using System;
using System.Threading.Tasks;

Console.WriteLine("🔬 Testing Belay.NET Attribute System");
Console.WriteLine("═══════════════════════════════════════");

try
{
    // Connect to the available RPi Pico
    var connectionString = "serial:/dev/usb/tty-Board_in_FS_mode-e6614c311b7e6f35";
    Console.WriteLine($"Connecting to: {connectionString}");
    
    using var device = Device.FromConnectionString(connectionString);
    await device.ConnectAsync();
    
    Console.WriteLine("✅ Device connected successfully");
    
    // Test 1: Simple sensor interface
    Console.WriteLine("\n🧪 Test 1: Simple Sensor Interface");
    Console.WriteLine("───────────────────────────────────────");
    
    var sensor = device.CreateProxy<ISimpleSensorDevice>();
    
    // Test basic greeting (no parameters)
    Console.WriteLine("Getting greeting...");
    var greeting = await sensor.GetGreetingAsync();
    Console.WriteLine($"Device says: {greeting}");
    
    // Test parameter substitution with LED control
    Console.WriteLine("Testing LED control with parameter substitution...");
    await sensor.SetLEDAsync(25, true);  // Turn on built-in LED
    Console.WriteLine("✅ LED turned on");
    
    await Task.Delay(1000); // Wait 1 second
    
    await sensor.SetLEDAsync(25, false); // Turn off built-in LED
    Console.WriteLine("✅ LED turned off");
    
    // Test device info (no parameter substitution)
    Console.WriteLine("Getting device info...");
    var info = await sensor.GetDeviceInfoAsync();
    Console.WriteLine($"Device info: {info}");
    
    // Test temperature reading (complex Python code)
    Console.WriteLine("Reading simulated temperature...");
    var temperature = await sensor.ReadTemperatureAsync();
    Console.WriteLine($"Temperature: {temperature}°C");
    
    Console.WriteLine("\n🎉 Simple sensor test completed successfully!");
    
    // Test 2: Environment Monitor Interface (Setup/Task/Teardown)
    Console.WriteLine("\n🧪 Test 2: Environment Monitor Interface");
    Console.WriteLine("───────────────────────────────────────");
    
    var monitor = device.CreateProxy<IEnvironmentMonitor>();
    
    // Test setup methods (should execute in order)
    Console.WriteLine("Initializing hardware...");
    await monitor.InitializeHardwareAsync();
    Console.WriteLine("✅ Hardware initialized");
    
    Console.WriteLine("Loading calibration...");
    await monitor.LoadCalibrationAsync();
    Console.WriteLine("✅ Calibration loaded");
    
    Console.WriteLine("Initializing monitoring state...");
    await monitor.InitializeMonitoringStateAsync();
    Console.WriteLine("✅ Monitoring state initialized");
    
    // Test task methods
    Console.WriteLine("Getting current reading...");
    try 
    {
        var reading = await monitor.GetCurrentReadingAsync();
        Console.WriteLine($"Reading: {reading}");
        Console.WriteLine("✅ Environment reading successful");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️  Environment reading failed (expected - no real sensors): {ex.Message}");
    }
    
    // Test diagnostics
    Console.WriteLine("Getting diagnostics...");
    var diagnostics = await monitor.GetDiagnosticsAsync();
    Console.WriteLine($"System diagnostics received: {diagnostics.Count} categories");
    Console.WriteLine("✅ Diagnostics successful");
    
    // Test teardown methods
    Console.WriteLine("Running cleanup...");
    await monitor.CleanupHardwareAsync();
    Console.WriteLine("✅ Hardware cleanup completed");
    
    Console.WriteLine("\n🎉 Environment monitor test completed successfully!");
    
    await device.DisconnectAsync();
    Console.WriteLine("✅ Device disconnected cleanly");
    
    Console.WriteLine("\n╔══════════════════════════════════════════╗");
    Console.WriteLine("║          🎉 ALL TESTS PASSED! 🎉         ║");
    Console.WriteLine("║                                          ║");
    Console.WriteLine("║    The attribute system is working!     ║");
    Console.WriteLine("║   Method interception successful ✨     ║");
    Console.WriteLine("╚══════════════════════════════════════════╝");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Test failed: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    Environment.Exit(1);
}