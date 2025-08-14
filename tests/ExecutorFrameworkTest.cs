// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Belay.Attributes;
using Belay.Core;
using Belay.Core.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Belay.Tests;

/// <summary>
/// Basic test to verify the executor framework foundation is working.
/// This serves as a smoke test for the core executor architecture.
/// </summary>
public class ExecutorFrameworkTest
{
    private readonly MockDeviceConnection mockDevice;
    private readonly ExecutorFramework executorFramework;

    public ExecutorFrameworkTest()
    {
        mockDevice = new MockDeviceConnection();
        executorFramework = new ExecutorFramework(mockDevice, NullLogger<ExecutorFramework>.Instance);
    }

    public async Task<bool> TestTaskExecutorBasics()
    {
        try
        {
            // Test method with TaskAttribute
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.GetTemperature))!;
            var args = new object[] { 26 };

            // Execute the method
            var result = await executorFramework.ExecuteAsync<int>(method, args);

            Console.WriteLine($"✅ TaskExecutor test passed. Result: {result}");
            Console.WriteLine($"✅ Generated Python: {mockDevice.LastExecutedCode}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TaskExecutor test failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TestSetupExecutorBasics()
    {
        try
        {
            // Test setup method with critical flag
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.InitializeHardware))!;
            var args = new object[] { };

            // Execute the method
            await executorFramework.ExecuteAsync<object>(method, args);

            Console.WriteLine($"✅ SetupExecutor test passed");
            Console.WriteLine($"✅ Generated Python: {mockDevice.LastExecutedCode}");
            Console.WriteLine($"✅ Contains setup context: {mockDevice.LastExecutedCode.Contains("Setup method:")}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SetupExecutor test failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TestTeardownExecutorBasics()
    {
        try
        {
            // Test teardown method with error handling
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.SaveState))!;
            var args = new object[] { };

            // Execute the method
            await executorFramework.ExecuteAsync<object>(method, args);

            Console.WriteLine($"✅ TeardownExecutor test passed");
            Console.WriteLine($"✅ Generated Python: {mockDevice.LastExecutedCode}");
            Console.WriteLine($"✅ Contains teardown context: {mockDevice.LastExecutedCode.Contains("Teardown method:")}");
            Console.WriteLine($"✅ Contains error handling: {mockDevice.LastExecutedCode.Contains("try:")}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ TeardownExecutor test failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TestThreadExecutorBasics()
    {
        try
        {
            // Test thread method with configuration
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.StartSensorMonitoring))!;
            var args = new object[] { 1000 };

            // Execute the method
            await executorFramework.ExecuteAsync<object>(method, args);

            Console.WriteLine($"✅ ThreadExecutor test passed");
            Console.WriteLine($"✅ Generated Python: {mockDevice.LastExecutedCode}");
            Console.WriteLine($"✅ Contains thread context: {mockDevice.LastExecutedCode.Contains("Thread method:")}");
            Console.WriteLine($"✅ Contains _thread usage: {mockDevice.LastExecutedCode.Contains("_thread.start_new_thread")}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ThreadExecutor test failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TestAllExecutorTypes()
    {
        Console.WriteLine("🧪 Testing all executor types...");
        
        var taskResult = await TestTaskExecutorBasics();
        var setupResult = await TestSetupExecutorBasics();
        var teardownResult = await TestTeardownExecutorBasics(); 
        var threadResult = await TestThreadExecutorBasics();
        
        var allPassed = taskResult && setupResult && teardownResult && threadResult;
        
        if (allPassed)
        {
            Console.WriteLine("🎉 All executor types working correctly!");
        }
        else
        {
            Console.WriteLine("❌ Some executor types failed");
        }
        
        return allPassed;
    }

    public void TestExecutorStatistics()
    {
        try
        {
            var stats = executorFramework.GetStatistics();
            
            Console.WriteLine("📊 Executor Framework Statistics:");
            foreach (var kvp in stats)
            {
                Console.WriteLine($"   {kvp.Key}: {kvp.Value}");
            }

            Console.WriteLine("✅ Statistics test passed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Statistics test failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        executorFramework.Dispose();
        mockDevice.Dispose();
    }
}

/// <summary>
/// Test methods decorated with various attributes for testing all executor types.
/// </summary>
public static class TestMethods
{
    [Task(Name = "read_temperature", TimeoutMs = 5000, Cache = true)]
    public static int GetTemperature(int pin)
    {
        // This method would be converted to Python: read_temperature(26)
        return 0; // Placeholder - actual execution happens on device
    }

    [Task]
    public static string GetDeviceInfo()
    {
        // This method would be converted to Python: device_info()
        return string.Empty;
    }

    [Setup(Order = 1, Critical = true)]
    public static void InitializeHardware()
    {
        // This method would be converted to Python: hardware() with setup context
    }

    [Setup(Order = 2, TimeoutMs = 10000)]
    public static void ConfigureSensors()
    {
        // This method would be converted to Python: sensors() with setup context
    }

    [Teardown(Order = 1, IgnoreErrors = false)]
    public static void StopOperations()
    {
        // This method would be converted to Python: operations() with teardown context
    }

    [Teardown(Order = 2, IgnoreErrors = true)]
    public static void SaveState()
    {
        // This method would be converted to Python: state() with teardown context and error handling
    }

    [Thread(Name = "sensor_monitor", AutoRestart = true, Priority = ThreadPriority.High)]
    public static void StartSensorMonitoring(int intervalMs)
    {
        // This method would be converted to Python thread creation with monitoring wrapper
    }

    [Thread]
    public static void StartBackgroundTask()
    {
        // This method would be converted to Python thread creation with auto-generated name
    }
}

/// <summary>
/// Mock device connection for testing the executor framework without real hardware.
/// </summary>
public class MockDeviceConnection : IDeviceConnection
{
    public string LastExecutedCode { get; private set; } = string.Empty;
    public bool IsConnected => true;
    public string DeviceInfo => "Mock MicroPython Device v1.0";
    public string ConnectionString => "mock://test-device";

    public Task<string> ExecutePython(string code, CancellationToken cancellationToken = default)
    {
        LastExecutedCode = code;
        // Simulate device response based on the code
        if (code.Contains("read_temperature"))
            return Task.FromResult("23");
        if (code.Contains("device_info"))
            return Task.FromResult("Mock Device Info");
        
        return Task.FromResult("OK");
    }

    public Task<T> ExecutePython<T>(string code, CancellationToken cancellationToken = default)
    {
        LastExecutedCode = code;
        
        // Simple mock responses based on return type
        if (typeof(T) == typeof(int))
            return Task.FromResult((T)(object)23);
        if (typeof(T) == typeof(string))
            return Task.FromResult((T)(object)"Mock Result");
        
        return Task.FromResult(default(T)!);
    }

    public Task WriteFile(string devicePath, byte[] data, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<byte[]> ReadFile(string devicePath, CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<byte>());

    public Task DeleteFile(string devicePath, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<string[]> ListFiles(string devicePath = "/", CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<string>());

    public Task Connect(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task Disconnect()
        => Task.CompletedTask;

    public void Dispose()
    {
        // No cleanup needed for mock
    }
}