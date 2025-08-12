// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System;
using Belay.Core;
using Belay.Core.Communication;
using FluentAssertions;
using NUnit.Framework;

namespace Belay.Tests.Unit;

/// <summary>
/// Tests for the DeviceState class.
/// Validates the simplified state tracking functionality that replaces
/// complex session management.
/// </summary>
[TestFixture]
public class DeviceStateTests
{
    private DeviceState deviceState = null!;

    [SetUp]
    public void SetUp()
    {
        this.deviceState = new DeviceState();
    }

    [Test]
    public void DeviceState_InitialState_IsCorrect()
    {
        // Arrange & Act - deviceState created in SetUp

        // Assert
        this.deviceState.Capabilities.Should().BeNull("capabilities not detected yet");
        this.deviceState.CurrentOperation.Should().BeNull("no operation in progress");
        this.deviceState.LastOperationTime.Should().BeNull("no operations completed yet");
        this.deviceState.ConnectionState.Should().Be(DeviceConnectionState.Disconnected, "initial state should be disconnected");
    }

    [Test]
    public void SetCapabilities_WithValidData_UpdatesState()
    {
        // Arrange
        var capabilities = new SimpleDeviceCapabilities
        {
            Platform = "esp32",
            Version = "3.4.0",
            SupportedFeatures = SimpleDeviceFeatureSet.GPIO | SimpleDeviceFeatureSet.WiFi,
            AvailableMemory = 50000,
            DetectionComplete = true
        };

        // Act
        this.deviceState.Capabilities = capabilities;

        // Assert
        this.deviceState.Capabilities.Should().NotBeNull();
        this.deviceState.Capabilities!.Platform.Should().Be("esp32");
        this.deviceState.Capabilities.Version.Should().Be("3.4.0");
        this.deviceState.Capabilities.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.GPIO);
        this.deviceState.Capabilities.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.WiFi);
        this.deviceState.Capabilities.AvailableMemory.Should().Be(50000);
        this.deviceState.Capabilities.DetectionComplete.Should().BeTrue();
    }

    [Test]
    public void SetCurrentOperation_TracksOperation()
    {
        // Arrange
        const string operationName = "ExecutePythonCode";

        // Act
        this.deviceState.SetCurrentOperation(operationName);

        // Assert
        this.deviceState.CurrentOperation.Should().Be(operationName);
        this.deviceState.LastOperationTime.Should().BeNull("operation not completed yet");
    }

    [Test]
    public void SetCurrentOperation_WithNull_ClearsOperation()
    {
        // Arrange
        this.deviceState.SetCurrentOperation("test");

        // Act
        this.deviceState.SetCurrentOperation(null);

        // Assert
        this.deviceState.CurrentOperation.Should().BeNull();
    }

    [Test]
    public void CompleteOperation_UpdatesTimestamp()
    {
        // Arrange
        this.deviceState.SetCurrentOperation("test operation");
        var beforeComplete = DateTime.UtcNow;

        // Act
        this.deviceState.CompleteOperation();
        var afterComplete = DateTime.UtcNow;

        // Assert
        this.deviceState.CurrentOperation.Should().BeNull("operation should be cleared");
        this.deviceState.LastOperationTime.Should().NotBeNull("completion time should be set");
        this.deviceState.LastOperationTime!.Value.Should().BeAfter(beforeComplete.AddMilliseconds(-10));
        this.deviceState.LastOperationTime.Value.Should().BeBefore(afterComplete.AddMilliseconds(10));
    }

    [Test]
    public void ToString_WithNoCapabilities_ReturnsFormattedString()
    {
        // Arrange
        this.deviceState.ConnectionState = DeviceConnectionState.Connected;

        // Act
        var result = this.deviceState.ToString();

        // Assert
        result.Should().Contain("DeviceState");
        result.Should().Contain("Connected");
        result.Should().Contain("Platform: Unknown");
        result.Should().Contain("Idle");
    }

    [Test]
    public void ToString_WithCapabilitiesAndOperation_ReturnsFormattedString()
    {
        // Arrange
        this.deviceState.Capabilities = new SimpleDeviceCapabilities { Platform = "esp32" };
        this.deviceState.SetCurrentOperation("TestOperation");
        this.deviceState.ConnectionState = DeviceConnectionState.Connected;

        // Act
        var result = this.deviceState.ToString();

        // Assert
        result.Should().Contain("DeviceState");
        result.Should().Contain("Connected");
        result.Should().Contain("Platform: esp32");
        result.Should().Contain("Operation: TestOperation");
    }

    [Test]
    public void ConnectionState_CanBeUpdated()
    {
        // Arrange
        this.deviceState.ConnectionState = DeviceConnectionState.Disconnected;

        // Act
        this.deviceState.ConnectionState = DeviceConnectionState.Connecting;

        // Assert
        this.deviceState.ConnectionState.Should().Be(DeviceConnectionState.Connecting);

        // Act
        this.deviceState.ConnectionState = DeviceConnectionState.Connected;

        // Assert
        this.deviceState.ConnectionState.Should().Be(DeviceConnectionState.Connected);
    }
}

/// <summary>
/// Tests for the SimpleDeviceCapabilities class.
/// Validates simplified capability detection functionality.
/// </summary>
[TestFixture]
public class SimpleDeviceCapabilitiesTests
{
    [Test]
    public void SimpleDeviceCapabilities_DefaultState_IsCorrect()
    {
        // Arrange & Act
        var capabilities = new SimpleDeviceCapabilities();

        // Assert
        capabilities.Platform.Should().BeNull();
        capabilities.Version.Should().BeNull();
        capabilities.SupportedFeatures.Should().Be(SimpleDeviceFeatureSet.None);
        capabilities.AvailableMemory.Should().Be(0);
        capabilities.DetectionComplete.Should().BeFalse();
    }

    [Test]
    public void SupportsFeature_WithSupportedFeature_ReturnsTrue()
    {
        // Arrange
        var capabilities = new SimpleDeviceCapabilities
        {
            SupportedFeatures = SimpleDeviceFeatureSet.GPIO | SimpleDeviceFeatureSet.I2C
        };

        // Act & Assert
        capabilities.SupportsFeature(SimpleDeviceFeatureSet.GPIO).Should().BeTrue();
        capabilities.SupportsFeature(SimpleDeviceFeatureSet.I2C).Should().BeTrue();
    }

    [Test]
    public void SupportsFeature_WithUnsupportedFeature_ReturnsFalse()
    {
        // Arrange
        var capabilities = new SimpleDeviceCapabilities
        {
            SupportedFeatures = SimpleDeviceFeatureSet.GPIO
        };

        // Act & Assert
        capabilities.SupportsFeature(SimpleDeviceFeatureSet.WiFi).Should().BeFalse();
        capabilities.SupportsFeature(SimpleDeviceFeatureSet.Bluetooth).Should().BeFalse();
    }

    [Test]
    public void SupportsFeature_WithNoFeatures_ReturnsFalse()
    {
        // Arrange
        var capabilities = new SimpleDeviceCapabilities
        {
            SupportedFeatures = SimpleDeviceFeatureSet.None
        };

        // Act & Assert
        capabilities.SupportsFeature(SimpleDeviceFeatureSet.GPIO).Should().BeFalse();
    }

    [Test]
    public void ToString_WithCompleteDetection_ReturnsFormattedString()
    {
        // Arrange
        var capabilities = new SimpleDeviceCapabilities
        {
            Platform = "esp32",
            Version = "3.4.0",
            SupportedFeatures = SimpleDeviceFeatureSet.GPIO | SimpleDeviceFeatureSet.WiFi | SimpleDeviceFeatureSet.I2C,
            AvailableMemory = 45000,
            DetectionComplete = true
        };

        // Act
        var result = capabilities.ToString();

        // Assert
        result.Should().Contain("DeviceCapabilities");
        result.Should().Contain("Complete");
        result.Should().Contain("Platform: esp32");
        result.Should().Contain("Version: 3.4.0");
        result.Should().Contain("Features: 3"); // GPIO + WiFi + I2C
        result.Should().Contain("Memory: 45000 bytes");
    }

    [Test]
    public void ToString_WithPendingDetection_ReturnsFormattedString()
    {
        // Arrange
        var capabilities = new SimpleDeviceCapabilities
        {
            DetectionComplete = false
        };

        // Act
        var result = capabilities.ToString();

        // Assert
        result.Should().Contain("DeviceCapabilities");
        result.Should().Contain("Pending");
        result.Should().Contain("Platform: Unknown");
        result.Should().Contain("Version: Unknown");
        result.Should().Contain("Features: 0");
    }
}

/// <summary>
/// Tests for the DeviceFeatureSet enumeration.
/// Validates flag enumeration behavior.
/// </summary>
[TestFixture]
public class SimpleDeviceFeatureSetTests
{
    [Test]
    public void SimpleDeviceFeatureSet_FlagCombination_WorksCorrectly()
    {
        // Arrange
        var combinedFeatures = SimpleDeviceFeatureSet.GPIO | SimpleDeviceFeatureSet.I2C | SimpleDeviceFeatureSet.SPI;

        // Act & Assert
        combinedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.GPIO);
        combinedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.I2C);
        combinedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.SPI);
        combinedFeatures.Should().NotHaveFlag(SimpleDeviceFeatureSet.WiFi);
    }

    [Test]
    public void SimpleDeviceFeatureSet_None_HasNoFlags()
    {
        // Arrange
        var noFeatures = SimpleDeviceFeatureSet.None;

        // Act & Assert
        noFeatures.Should().NotHaveFlag(SimpleDeviceFeatureSet.GPIO);
        noFeatures.Should().NotHaveFlag(SimpleDeviceFeatureSet.WiFi);
        noFeatures.Should().NotHaveFlag(SimpleDeviceFeatureSet.I2C);
    }

    [Test]
    public void SimpleDeviceFeatureSet_AllValues_AreValid()
    {
        // This test ensures all enum values are powers of 2 (valid flags)
        var values = Enum.GetValues<SimpleDeviceFeatureSet>();
        
        foreach (var value in values)
        {
            if (value == SimpleDeviceFeatureSet.None) continue;
            
            // Each flag should be a power of 2
            var intValue = (int)value;
            var isPowerOfTwo = (intValue & (intValue - 1)) == 0;
            isPowerOfTwo.Should().BeTrue($"{value} should be a power of 2 for flag enumeration");
        }
    }
}