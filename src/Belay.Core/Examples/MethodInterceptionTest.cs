// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Examples;

using Belay.Core.Communication;
using Belay.Core.Execution;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

/// <summary>
/// Test class demonstrating method interception with the [Task] and [PythonCode] attributes.
/// This shows how to create a proxy that automatically routes method calls to a MicroPython device.
/// </summary>
public static class MethodInterceptionTest
{
    /// <summary>
    /// Demonstrates basic method interception using an interface proxy.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <returns>A task representing the test operation.</returns>
    public static async Task RunBasicInterceptionTestAsync(ILogger? logger = null)
    {
        logger?.LogInformation("Starting method interception test...");

        // Create a device with subprocess communication for testing
        using var communication = new SubprocessDeviceCommunication("python3", logger: logger as ILogger<SubprocessDeviceCommunication>);
        using var device = new Device(communication, logger as ILogger<Device>);

        try
        {
            // Connect to the device
            await device.ConnectAsync();
            logger?.LogInformation("Connected to device successfully");

            // Create a proxy for the interface
            var sensor = device.CreateProxy<ISimpleSensorDevice>(logger);
            logger?.LogInformation("Created device proxy for ISimpleSensorDevice");

            // Test basic method interception
            logger?.LogInformation("Testing GetGreetingAsync...");
            var greeting = await sensor.GetGreetingAsync();
            logger?.LogInformation("Greeting from device: {Greeting}", greeting);

            // Test method with return value
            logger?.LogInformation("Testing ReadTemperatureAsync...");
            var temperature = await sensor.ReadTemperatureAsync();
            logger?.LogInformation("Temperature reading: {Temperature}°C", temperature);

            // Test method with parameters
            logger?.LogInformation("Testing SetLEDAsync with parameters...");
            await sensor.SetLEDAsync(2, true);
            logger?.LogInformation("LED command sent successfully");

            // Test method without parameter substitution
            logger?.LogInformation("Testing GetDeviceInfoAsync...");
            var deviceInfo = await sensor.GetDeviceInfoAsync();
            logger?.LogInformation("Device info: {DeviceInfo}", deviceInfo);

            // Test setup and teardown methods
            logger?.LogInformation("Testing setup method...");
            await sensor.InitializeAsync();
            logger?.LogInformation("Setup completed");

            logger?.LogInformation("Testing teardown method...");
            await sensor.CleanupAsync();
            logger?.LogInformation("Cleanup completed");

            logger?.LogInformation("All method interception tests completed successfully!");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Method interception test failed");
            throw;
        }
        finally
        {
            await device.DisconnectAsync();
            logger?.LogInformation("Disconnected from device");
        }
    }

    /// <summary>
    /// Demonstrates enhanced executor statistics and capabilities.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <returns>A task representing the test operation.</returns>
    public static async Task RunEnhancedExecutorTestAsync(ILogger? logger = null)
    {
        logger?.LogInformation("Starting enhanced executor test...");

        using var communication = new SubprocessDeviceCommunication("python3", logger: logger as ILogger<SubprocessDeviceCommunication>);
        using var device = new Device(communication, logger as ILogger<Device>);

        try
        {
            await device.ConnectAsync();

            // Get the enhanced executor
            var enhancedExecutor = device.GetEnhancedExecutor(logger);
            logger?.LogInformation("Retrieved enhanced executor");

            // Get initial statistics
            var initialStats = enhancedExecutor.GetExecutionStatistics();
            logger?.LogInformation("Initial enhanced execution statistics:");
            logger?.LogInformation("  - Specialized executors: {Count}", initialStats.SpecializedExecutorCount);
            logger?.LogInformation("  - Pipeline stages: {Count}", initialStats.PipelineStageCount);
            logger?.LogInformation("  - Intercepted methods: {Count}", initialStats.InterceptedMethodCount);

            // Create proxy and execute some methods
            var sensor = device.CreateProxy<ISimpleSensorDevice>(logger);
            await sensor.GetGreetingAsync();
            await sensor.ReadTemperatureAsync();

            // Get updated statistics
            var finalStats = enhancedExecutor.GetExecutionStatistics();
            logger?.LogInformation("Final enhanced execution statistics:");
            logger?.LogInformation("  - Specialized executors: {Count}", finalStats.SpecializedExecutorCount);
            logger?.LogInformation("  - Pipeline stages: {Count}", finalStats.PipelineStageCount);
            logger?.LogInformation("  - Intercepted methods: {Count}", finalStats.InterceptedMethodCount);

            logger?.LogInformation("Enhanced executor test completed successfully!");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Enhanced executor test failed");
            throw;
        }
        finally
        {
            await device.DisconnectAsync();
        }
    }
}