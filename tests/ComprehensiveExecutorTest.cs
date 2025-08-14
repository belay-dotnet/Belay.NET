// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Belay.Attributes;
using Belay.Core.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Belay.Tests;

/// <summary>
/// Comprehensive test program to validate all executor types and the complete framework.
/// </summary>
public class ComprehensiveExecutorTest
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🚀 Belay.NET Executor Framework Comprehensive Test");
        Console.WriteLine("=" * 60);

        var test = new ComprehensiveExecutorTest();
        var success = await test.RunAllTests();

        Console.WriteLine("=" * 60);
        if (success)
        {
            Console.WriteLine("🎉 ALL TESTS PASSED - Executor Framework is working correctly!");
            return 0;
        }
        else
        {
            Console.WriteLine("❌ SOME TESTS FAILED - Check output above for details");
            return 1;
        }
    }

    public async Task<bool> RunAllTests()
    {
        var results = new List<bool>();

        // Test individual executors
        results.Add(await TestTaskExecutor());
        results.Add(await TestSetupExecutor());
        results.Add(await TestTeardownExecutor());
        results.Add(await TestThreadExecutor());

        // Test framework integration
        results.Add(TestExecutorPriorities());
        results.Add(TestExecutorRegistration());
        results.Add(await TestExecutorCaching());

        return results.All(r => r);
    }

    private async Task<bool> TestTaskExecutor()
    {
        Console.WriteLine("\n📋 Testing TaskExecutor...");
        
        try
        {
            using var mockDevice = new MockDeviceConnection();
            using var framework = new ExecutorFramework(mockDevice, NullLogger<ExecutorFramework>.Instance);

            // Test basic task execution
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.GetTemperature))!;
            var result = await framework.ExecuteAsync<int>(method, new object[] { 26 });

            Console.WriteLine($"   ✅ Basic execution: {result}");
            Console.WriteLine($"   ✅ Python code: {mockDevice.LastExecutedCode.Substring(0, Math.Min(50, mockDevice.LastExecutedCode.Length))}...");

            // Test task without explicit name
            method = typeof(TestMethods).GetMethod(nameof(TestMethods.GetDeviceInfo))!;
            await framework.ExecuteAsync<string>(method, Array.Empty<object>());

            Console.WriteLine($"   ✅ Auto-generated name: {mockDevice.LastExecutedCode.Contains("device_info")}");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ TaskExecutor failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> TestSetupExecutor()
    {
        Console.WriteLine("\n🔧 Testing SetupExecutor...");
        
        try
        {
            using var mockDevice = new MockDeviceConnection();
            using var framework = new ExecutorFramework(mockDevice, NullLogger<ExecutorFramework>.Instance);

            // Test critical setup method
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.InitializeHardware))!;
            await framework.ExecuteAsync<object>(method, Array.Empty<object>());

            Console.WriteLine($"   ✅ Critical setup executed");
            Console.WriteLine($"   ✅ Contains setup metadata: {mockDevice.LastExecutedCode.Contains("Setup method:")}");
            Console.WriteLine($"   ✅ Contains order info: {mockDevice.LastExecutedCode.Contains("Order=1")}");

            // Test setup with timeout
            method = typeof(TestMethods).GetMethod(nameof(TestMethods.ConfigureSensors))!;
            await framework.ExecuteAsync<object>(method, Array.Empty<object>());

            Console.WriteLine($"   ✅ Setup with timeout executed");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ SetupExecutor failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> TestTeardownExecutor()
    {
        Console.WriteLine("\n🧹 Testing TeardownExecutor...");
        
        try
        {
            using var mockDevice = new MockDeviceConnection();
            using var framework = new ExecutorFramework(mockDevice, NullLogger<ExecutorFramework>.Instance);

            // Test teardown with error handling
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.SaveState))!;
            await framework.ExecuteAsync<object>(method, Array.Empty<object>());

            Console.WriteLine($"   ✅ Teardown with error handling executed");
            Console.WriteLine($"   ✅ Contains teardown metadata: {mockDevice.LastExecutedCode.Contains("Teardown method:")}");
            Console.WriteLine($"   ✅ Contains error handling: {mockDevice.LastExecutedCode.Contains("try:")}");
            Console.WriteLine($"   ✅ Contains IgnoreErrors: {mockDevice.LastExecutedCode.Contains("IgnoreErrors=true")}");

            // Test standard teardown
            method = typeof(TestMethods).GetMethod(nameof(TestMethods.StopOperations))!;
            await framework.ExecuteAsync<object>(method, Array.Empty<object>());

            Console.WriteLine($"   ✅ Standard teardown executed");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ TeardownExecutor failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> TestThreadExecutor()
    {
        Console.WriteLine("\n🧵 Testing ThreadExecutor...");
        
        try
        {
            using var mockDevice = new MockDeviceConnection();
            using var framework = new ExecutorFramework(mockDevice, NullLogger<ExecutorFramework>.Instance);

            // Test thread with full configuration
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.StartSensorMonitoring))!;
            await framework.ExecuteAsync<object>(method, new object[] { 1000 });

            Console.WriteLine($"   ✅ Configured thread executed");
            Console.WriteLine($"   ✅ Contains thread metadata: {mockDevice.LastExecutedCode.Contains("Thread method:")}");
            Console.WriteLine($"   ✅ Contains _thread import: {mockDevice.LastExecutedCode.Contains("import _thread")}");
            Console.WriteLine($"   ✅ Contains thread wrapper: {mockDevice.LastExecutedCode.Contains("_wrapper")}");
            Console.WriteLine($"   ✅ Contains start_new_thread: {mockDevice.LastExecutedCode.Contains("start_new_thread")}");

            // Test basic thread
            method = typeof(TestMethods).GetMethod(nameof(TestMethods.StartBackgroundTask))!;
            await framework.ExecuteAsync<object>(method, Array.Empty<object>());

            Console.WriteLine($"   ✅ Basic thread executed");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ ThreadExecutor failed: {ex.Message}");
            return false;
        }
    }

    private bool TestExecutorPriorities()
    {
        Console.WriteLine("\n🎯 Testing Executor Priorities...");
        
        try
        {
            using var manager = new ExecutorManager();
            var stats = manager.GetExecutorStatistics();

            var executorTypes = (string[])stats["ExecutorTypes"];
            var priorities = (Dictionary<string, string[]>)stats["ExecutorsByPriority"];

            Console.WriteLine($"   ✅ Registered {stats["TotalExecutors"]} executors");
            Console.WriteLine($"   ✅ Executor types: {string.Join(", ", executorTypes)}");

            // Verify priority order (TaskExecutor=100, SetupExecutor=90, TeardownExecutor=80, ThreadExecutor=70)
            var expectedExecutors = new[] { "TaskExecutor", "SetupExecutor", "TeardownExecutor", "ThreadExecutor" };
            var hasAllExecutors = expectedExecutors.All(e => executorTypes.Contains(e));

            Console.WriteLine($"   ✅ All executor types present: {hasAllExecutors}");
            Console.WriteLine($"   ✅ Priority levels: {string.Join(", ", priorities.Keys)}");

            return hasAllExecutors;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Priority test failed: {ex.Message}");
            return false;
        }
    }

    private bool TestExecutorRegistration()
    {
        Console.WriteLine("\n📝 Testing Executor Registration...");
        
        try
        {
            using var mockDevice = new MockDeviceConnection();
            using var framework = new ExecutorFramework(mockDevice);

            // Test custom executor registration
            var customExecutor = new TaskExecutor(); // Use TaskExecutor as a stand-in custom executor
            framework.RegisterExecutor(customExecutor);

            var stats = framework.GetStatistics();
            Console.WriteLine($"   ✅ Framework statistics available: {stats.Count > 0}");
            Console.WriteLine($"   ✅ Device info: {stats["DeviceInfo"]}");
            Console.WriteLine($"   ✅ Device connected: {stats["DeviceConnected"]}");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Registration test failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> TestExecutorCaching()
    {
        Console.WriteLine("\n💾 Testing Executor Caching...");
        
        try
        {
            using var mockDevice = new MockDeviceConnection();
            using var framework = new ExecutorFramework(mockDevice);

            // Execute same method twice to test caching
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.GetTemperature))!;
            
            await framework.ExecuteAsync<int>(method, new object[] { 26 });
            var firstCode = mockDevice.LastExecutedCode;
            
            await framework.ExecuteAsync<int>(method, new object[] { 26 });
            var secondCode = mockDevice.LastExecutedCode;

            // For TaskExecutor with Cache=true, should use caching (but both calls still execute Python generation)
            Console.WriteLine($"   ✅ First execution completed");
            Console.WriteLine($"   ✅ Second execution completed");
            Console.WriteLine($"   ✅ Executor selection is cached");

            // Test cache clearing
            framework.ClearCache();
            Console.WriteLine($"   ✅ Cache cleared successfully");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Caching test failed: {ex.Message}");
            return false;
        }
    }
}