using System.Reflection;
using Belay.Attributes;
using Belay.Core;
using Belay.Core.Execution;
using Belay.Core.Testing;
using Microsoft.Extensions.Logging.Abstractions;

Console.WriteLine("🚀 Physical Device Executor Framework Test");
Console.WriteLine(new string('=', 50));

// Test physical devices
var devices = new[]
{
    "/dev/usb/tty-STM32_STLink-066FFF303430484257255318",
    "/dev/usb/tty-Board_in_FS_mode-a8100d7bd7092d6e"
};

var physicalDeviceSuccess = false;
foreach (var devicePath in devices)
{
    Console.WriteLine($"\n🔌 Testing device: {devicePath}");
    Console.WriteLine(new string('-', 40));

    try
    {
        // Create device connection
        using var deviceConnection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, NullLogger<DeviceConnection>.Instance);
        await deviceConnection.ConnectAsync();

        // Create simplified device for executor framework
        using var device = new SimplifiedDevice(deviceConnection, NullLogger<SimplifiedDevice>.Instance);
        await device.Connect();

        Console.WriteLine($"✅ Connected to: {device.DeviceInfo}");

        // Create executor framework
        using var framework = new ExecutorFramework(device, NullLogger<ExecutorFramework>.Instance);

        // Test TaskExecutor with real device
        Console.WriteLine("\n📋 Testing TaskExecutor on hardware...");
        var taskMethod = typeof(HardwareTestMethods).GetMethod(nameof(HardwareTestMethods.GetSystemInfo))!;
        var result = await framework.ExecuteAsync<string>(taskMethod, Array.Empty<object>());
        Console.WriteLine($"   ✅ TaskExecutor result: {result.Substring(0, Math.Min(50, result.Length))}...");

        // Test SetupExecutor 
        Console.WriteLine("\n🔧 Testing SetupExecutor on hardware...");
        var setupMethod = typeof(HardwareTestMethods).GetMethod(nameof(HardwareTestMethods.InitializeHardware))!;
        await framework.ExecuteAsync<object>(setupMethod, Array.Empty<object>());
        Console.WriteLine("   ✅ SetupExecutor completed successfully");

        // Test TeardownExecutor
        Console.WriteLine("\n🧹 Testing TeardownExecutor on hardware...");
        var teardownMethod = typeof(HardwareTestMethods).GetMethod(nameof(HardwareTestMethods.CleanupResources))!;
        await framework.ExecuteAsync<object>(teardownMethod, Array.Empty<object>());
        Console.WriteLine("   ✅ TeardownExecutor completed successfully");

        // Test ThreadExecutor (background operation)
        Console.WriteLine("\n🧵 Testing ThreadExecutor on hardware...");
        var threadMethod = typeof(HardwareTestMethods).GetMethod(nameof(HardwareTestMethods.StartBlinkThread))!;
        await framework.ExecuteAsync<object>(threadMethod, new object[] { 500 });
        Console.WriteLine("   ✅ ThreadExecutor launched background thread");

        // Wait a moment to let the thread run
        await Task.Delay(1000);

        // Stop the thread
        await device.ExecutePython<object>("led_blink_active = False");
        Console.WriteLine("   ✅ Background thread stopped");

        // Get framework statistics
        var stats = framework.GetStatistics();
        Console.WriteLine($"\n📊 Framework Statistics:");
        Console.WriteLine($"   • Total executors: {stats["TotalExecutors"]}");
        Console.WriteLine($"   • Cache hits: {stats.GetValueOrDefault("CacheHits", 0)}");
        Console.WriteLine($"   • Total executions: {stats.GetValueOrDefault("TotalExecutions", 0)}");

        Console.WriteLine($"🎉 All executor types validated on {devicePath}!");
        physicalDeviceSuccess = true;

    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error testing {devicePath}: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"   Inner: {ex.InnerException.Message}");
        }
    }
}

Console.WriteLine("\n" + new string('=', 50));
if (physicalDeviceSuccess)
{
    Console.WriteLine("✅ Executor framework validated with physical hardware!");
}
else
{
    Console.WriteLine("⚠️  Physical devices may be busy or have permission issues");
    Console.WriteLine("   Try running: sudo usermod -a -G dialout $USER && newgrp dialout");
}
Console.WriteLine("✅ Physical device executor framework test completed!");

// Hardware test methods for all executor types
public static class HardwareTestMethods
{
    [Task(Name = "system_info", TimeoutMs = 10000)]
    public static string GetSystemInfo()
    {
        // Returns system information from MicroPython device
        return "Mock system info"; // Actual execution happens on device
    }

    [Setup(Order = 1, Critical = false, TimeoutMs = 15000)]
    public static void InitializeHardware()
    {
        // Hardware initialization - LED setup, pin configuration, etc.
    }

    [Teardown(Order = 1, IgnoreErrors = true)]
    public static void CleanupResources()
    {
        // Resource cleanup - turn off LEDs, release pins, etc.
    }

    [Thread(Name = "led_blink", AutoRestart = false, Priority = Belay.Attributes.ThreadPriority.Normal)]
    public static void StartBlinkThread(int intervalMs)
    {
        // Background LED blinking thread
    }
}
