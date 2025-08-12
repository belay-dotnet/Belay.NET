// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit;

using System;
using System.Threading.Tasks;
using Belay.Core;
using Belay.Core.Communication;
using Belay.Core.Execution;
using NUnit.Framework;

/// <summary>
/// Validation tests for the simplified architecture refactoring.
/// </summary>
[TestFixture]
public class SimplifiedArchitectureValidationTest
{
    [Test]
    public async Task Device_CreateConnectExecute_SimplifiedArchitecture_WorksCorrectly()
    {
        // Arrange - Create device with simplified architecture
        using var communication = new SubprocessDeviceCommunication("python3", ["-c", "print('Available')"]);
        using var device = new Device(communication, null, null);

        try
        {
            // Act - Connect using simplified architecture
            await device.ConnectAsync();

            // Verify device state is properly initialized
            Assert.That(device.State, Is.Not.Null);
            Assert.That(device.State.ConnectionState, Is.EqualTo(DeviceConnectionState.Connected));

            // Verify capability detection was performed (if successful)
            if (device.State.Capabilities != null)
            {
                Assert.That(device.State.Capabilities.DetectionComplete, Is.True);
                TestContext.WriteLine($"Detected Platform: {device.State.Capabilities.Platform}");
                TestContext.WriteLine($"Detected Features: {device.State.Capabilities.SupportedFeatures}");
            }

            // Act - Execute basic operation
            var result = await device.ExecuteAsync<string>("print('Hello Simplified Architecture'); 'success'");

            // Assert - Verify execution works
            Assert.That(result, Is.EqualTo("success"));

            // Verify simplified executors are available
            Assert.That(device.Task, Is.Not.Null);
            Assert.That(device.Setup, Is.Not.Null);
            Assert.That(device.Thread, Is.Not.Null);
            Assert.That(device.Teardown, Is.Not.Null);

            TestContext.WriteLine("✅ Simplified architecture validation successful!");
        }
        catch (Exception ex) when (ex.Message.Contains("python3") || ex.Message.Contains("subprocess"))
        {
            // Skip test if Python3 not available
            Assert.Ignore("Python3 not available for subprocess testing");
        }
        finally
        {
            if (device.State.ConnectionState == DeviceConnectionState.Connected)
            {
                await device.DisconnectAsync();
            }
        }
    }

    [Test]
    public void DeviceState_AfterRefactoring_HasCorrectStructure()
    {
        // Arrange & Act - Create device and verify state structure
        using var communication = new SubprocessDeviceCommunication("python3");
        using var device = new Device(communication, null, null);

        // Assert - Verify DeviceState structure
        Assert.That(device.State, Is.Not.Null);
        Assert.That(device.State.ConnectionState, Is.EqualTo(DeviceConnectionState.Disconnected));
        Assert.That(device.State.Capabilities, Is.Null); // Not connected yet
        Assert.That(device.State.CurrentOperation, Is.Null);
        Assert.That(device.State.LastOperationTime, Is.Null);

        TestContext.WriteLine("✅ DeviceState structure validation successful!");
    }

    [Test]
    public void SimplifiedExecutors_AfterRefactoring_AreAvailable()
    {
        // Arrange & Act - Create device and verify executor availability
        using var communication = new SubprocessDeviceCommunication("python3");
        using var device = new Device(communication, null, null);

        // Assert - Verify all simplified executors are available
        Assert.That(device.Task, Is.Not.Null);
        Assert.That(device.Task, Is.TypeOf<Belay.Core.Execution.SimplifiedTaskExecutor>());

        Assert.That(device.Setup, Is.Not.Null);
        Assert.That(device.Setup, Is.TypeOf<Belay.Core.Execution.SimplifiedSetupExecutor>());

        Assert.That(device.Thread, Is.Not.Null);
        Assert.That(device.Thread, Is.TypeOf<Belay.Core.Execution.SimplifiedThreadExecutor>());

        Assert.That(device.Teardown, Is.Not.Null);
        Assert.That(device.Teardown, Is.TypeOf<Belay.Core.Execution.SimplifiedTeardownExecutor>());

        TestContext.WriteLine("✅ Simplified executors validation successful!");
    }

    [Test]
    public void EnhancedExecutor_AfterRefactoring_IsAvailable()
    {
        // Arrange & Act - Create device and verify enhanced executor
        using var communication = new SubprocessDeviceCommunication("python3");
        using var device = new Device(communication, null, null);

        // Act - Get enhanced executor
        var enhancedExecutor = device.GetEnhancedExecutor();

        // Assert - Verify enhanced executor functionality
        Assert.That(enhancedExecutor, Is.Not.Null);
        Assert.That(enhancedExecutor, Is.TypeOf<Belay.Core.Execution.EnhancedExecutor>());

        // Verify statistics are available
        var stats = enhancedExecutor.GetExecutionStatistics();
        Assert.That(stats, Is.Not.Null);
        Assert.That(stats.SpecializedExecutorCount, Is.GreaterThanOrEqualTo(4)); // Task, Setup, Thread, Teardown

        TestContext.WriteLine("✅ Enhanced executor validation successful!");
    }
}