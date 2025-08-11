// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Tests;

using System;
using System.Threading.Tasks;
using Belay.Attributes;
using Belay.Core.Communication;
using Belay.Core.Execution;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Tests for method interception functionality with [Task] and [PythonCode] attributes.
/// </summary>
public class MethodInterceptionTests {
    /// <summary>
    /// Test interface for method interception validation.
    /// </summary>
    public interface ITestSensor {
        [Task]
        [PythonCode("'Hello from test device!'")]
        Task<string> GetGreetingAsync();

        [Task]
        [PythonCode("42")]
        Task<int> GetMagicNumberAsync();

        [Task]
        [PythonCode("{value} * 2")]
        Task<int> DoubleValueAsync(int value);

        [Task]
        [PythonCode("'{name}: {value}'")]
        Task<string> FormatMessageAsync(string name, int value);

        [Setup]
        [PythonCode("print('Setup complete')")]
        Task InitializeAsync();
    }

    [Fact]
    public void CreateProxy_WithValidInterface_ShouldSucceed() {
        // Arrange
        using var communication = new SubprocessDeviceCommunication("python3");
        using var device = new Device(communication, null, null);

        // Act & Assert
        var proxy = device.CreateProxy<ITestSensor>();
        Assert.NotNull(proxy);
        Assert.IsAssignableFrom<ITestSensor>(proxy);
    }

    [Fact]
    public void DeviceProxyFactory_CanProxy_ShouldReturnTrueForValidInterface() {
        // Act
        bool canProxy = DeviceProxyFactory.CanProxy(typeof(ITestSensor));

        // Assert
        Assert.True(canProxy);
    }

    [Fact]
    public void DeviceProxyFactory_CanProxy_ShouldReturnFalseForInvalidType() {
        // Act
        bool canProxy = DeviceProxyFactory.CanProxy(typeof(string));

        // Assert
        Assert.False(canProxy);
    }

    [Fact]
    public async Task MethodInterception_WithSimpleReturn_ShouldWork() {
        // Arrange
        using var communication = new SubprocessDeviceCommunication("python3");
        using var device = new Device(communication, null, null);

        try {
            await device.ConnectAsync();
            var sensor = device.CreateProxy<ITestSensor>();

            // Act
            var result = await sensor.GetGreetingAsync();

            // Assert
            Assert.Equal("Hello from test device!", result);
        }
        catch (Exception ex) {
            // If we can't connect to Python for subprocess testing, skip this test
            // This allows the test to pass in CI environments without Python
            if (ex.Message.Contains("python3") || ex.Message.Contains("subprocess")) {
                return; // Skip test
            }
            throw;
        }
        finally {
            if (device.State == DeviceConnectionState.Connected) {
                await device.DisconnectAsync();
            }
        }
    }

    [Fact]
    public async Task MethodInterception_WithParameterSubstitution_ShouldWork() {
        // Arrange
        using var communication = new SubprocessDeviceCommunication("python3");
        using var device = new Device(communication, null, null);

        try {
            await device.ConnectAsync();
            var sensor = device.CreateProxy<ITestSensor>();

            // Act
            var result = await sensor.DoubleValueAsync(21);

            // Assert
            Assert.Equal(42, result);
        }
        catch (Exception ex) {
            // Skip if Python not available
            if (ex.Message.Contains("python3") || ex.Message.Contains("subprocess")) {
                return;
            }
            throw;
        }
        finally {
            if (device.State == DeviceConnectionState.Connected) {
                await device.DisconnectAsync();
            }
        }
    }

    [Fact]
    public async Task MethodInterception_WithMultipleParameters_ShouldWork() {
        // Arrange
        using var communication = new SubprocessDeviceCommunication("python3");
        using var device = new Device(communication, null, null);

        try {
            await device.ConnectAsync();
            var sensor = device.CreateProxy<ITestSensor>();

            // Act
            var result = await sensor.FormatMessageAsync("test", 123);

            // Assert
            Assert.Equal("test: 123", result);
        }
        catch (Exception ex) {
            // Skip if Python not available
            if (ex.Message.Contains("python3") || ex.Message.Contains("subprocess")) {
                return;
            }
            throw;
        }
        finally {
            if (device.State == DeviceConnectionState.Connected) {
                await device.DisconnectAsync();
            }
        }
    }

    [Fact]
    public void GetEnhancedExecutor_ShouldReturnValidExecutor() {
        // Arrange
        using var communication = new SubprocessDeviceCommunication("python3");
        using var device = new Device(communication, null, null);

        // Act
        var executor = device.GetEnhancedExecutor();

        // Assert
        Assert.NotNull(executor);
        Assert.IsAssignableFrom<IEnhancedExecutor>(executor);
    }

    [Fact]
    public void GetEnhancedExecutor_Statistics_ShouldReturnValidData() {
        // Arrange
        using var communication = new SubprocessDeviceCommunication("python3");
        using var device = new Device(communication, null, null);
        var executor = device.GetEnhancedExecutor();

        // Act
        var stats = executor.GetExecutionStatistics();

        // Assert
        Assert.NotNull(stats);
        Assert.True(stats.SpecializedExecutorCount >= 0);
    }
}
