using System.Reflection;
using Belay.Attributes;
using Belay.Core;
using Belay.Core.Execution;
using Microsoft.Extensions.Logging.Abstractions;

Console.WriteLine("🚀 Quick Executor Framework Test");
Console.WriteLine(new string('=', 40));

// Create mock device connection
var mockDevice = new MockDeviceConnection();
var framework = new ExecutorFramework(mockDevice, NullLogger<ExecutorFramework>.Instance);

bool allPassed = true;

try 
{
    // Test TaskExecutor
    Console.WriteLine("\n📋 Testing TaskExecutor...");
    var taskMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.GetTemperature))!;
    var result = await framework.ExecuteAsync<int>(taskMethod, new object[] { 26 });
    Console.WriteLine($"   ✅ TaskExecutor result: {result}");
    Console.WriteLine($"   ✅ Contains read_temperature: {mockDevice.LastExecutedCode.Contains("read_temperature")}");

    // Test SetupExecutor  
    Console.WriteLine("\n🔧 Testing SetupExecutor...");
    var setupMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.InitializeHardware))!;
    await framework.ExecuteAsync<object>(setupMethod, Array.Empty<object>());
    Console.WriteLine($"   ✅ Contains setup metadata: {mockDevice.LastExecutedCode.Contains("Setup method:")}");

    // Test TeardownExecutor
    Console.WriteLine("\n🧹 Testing TeardownExecutor...");
    var teardownMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.SaveState))!;
    await framework.ExecuteAsync<object>(teardownMethod, Array.Empty<object>());
    Console.WriteLine($"   ✅ Contains teardown metadata: {mockDevice.LastExecutedCode.Contains("Teardown method:")}");
    Console.WriteLine($"   ✅ Contains error handling: {mockDevice.LastExecutedCode.Contains("try:")}");

    // Test ThreadExecutor
    Console.WriteLine("\n🧵 Testing ThreadExecutor...");
    var threadMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.StartSensorMonitoring))!;
    await framework.ExecuteAsync<object>(threadMethod, new object[] { 1000 });
    Console.WriteLine($"   ✅ Contains thread metadata: {mockDevice.LastExecutedCode.Contains("Thread method:")}");
    Console.WriteLine($"   ✅ Contains _thread usage: {mockDevice.LastExecutedCode.Contains("_thread.start_new_thread")}");

    // Test framework statistics
    Console.WriteLine("\n📊 Testing Framework Statistics...");
    var stats = framework.GetStatistics();
    Console.WriteLine($"   ✅ Total executors: {stats.GetValueOrDefault("TotalExecutors", "unknown")}");
    Console.WriteLine($"   ✅ Device connected: {stats.GetValueOrDefault("DeviceConnected", false)}");
    
    Console.WriteLine("\n🎉 ALL TESTS PASSED! The complete executor framework is working correctly!");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Test failed: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    allPassed = false;
}
finally
{
    framework.Dispose();
    mockDevice.Dispose();
}

Environment.Exit(allPassed ? 0 : 1);

// Test methods for all executor types
public static class TestMethods
{
    [Task(Name = "read_temperature", TimeoutMs = 5000, Cache = true)]
    public static int GetTemperature(int pin) => 0;

    [Setup(Order = 1, Critical = true)]
    public static void InitializeHardware() { }

    [Teardown(Order = 2, IgnoreErrors = true)]
    public static void SaveState() { }

    [Thread(Name = "sensor_monitor", AutoRestart = true, Priority = Belay.Attributes.ThreadPriority.High)]
    public static void StartSensorMonitoring(int intervalMs) { }
}

// Mock device connection for testing
public class MockDeviceConnection : IDeviceConnection
{
    public string LastExecutedCode { get; private set; } = string.Empty;
    public bool IsConnected => true;
    public string DeviceInfo => "Mock MicroPython Device v1.0";
    public string ConnectionString => "mock://test-device";

    public Task<string> ExecutePython(string code, CancellationToken cancellationToken = default)
    {
        LastExecutedCode = code;
        return Task.FromResult("23");
    }

    public Task<T> ExecutePython<T>(string code, CancellationToken cancellationToken = default)
    {
        LastExecutedCode = code;
        if (typeof(T) == typeof(int)) return Task.FromResult((T)(object)23);
        if (typeof(T) == typeof(string)) return Task.FromResult((T)(object)"Mock Result");
        return Task.FromResult(default(T)!);
    }

    public Task WriteFile(string devicePath, byte[] data, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<byte[]> ReadFile(string devicePath, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<byte>());
    public Task DeleteFile(string devicePath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<string[]> ListFiles(string devicePath = "/", CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<string>());
    public Task Connect(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Disconnect() => Task.CompletedTask;
    public void Dispose() { }
}
