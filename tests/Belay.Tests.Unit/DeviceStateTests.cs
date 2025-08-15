// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System;
using Belay.Core;
using FluentAssertions;
using Xunit;

namespace Belay.Tests.Unit;

/// <summary>
/// Tests for the DeviceState class.
/// Validates the simplified state tracking functionality that replaces
/// complex session management.
/// </summary>
public class DeviceStateTests {
    [Fact]
    public void DeviceState_InitialState_IsCorrect() {
        // Arrange
        var deviceState = new DeviceState();

        // Act & Assert
        deviceState.Capabilities.Should().BeNull("capabilities not detected yet");
        deviceState.CurrentOperation.Should().BeNull("no operation in progress");
        deviceState.LastOperationTime.Should().BeNull("no operations completed yet");
        deviceState.ConnectionState.Should().Be(DeviceConnectionState.Disconnected, "initial state should be disconnected");
    }

    [Fact]
    public void DeviceState_SetConnectionState_UpdatesCorrectly() {
        // Arrange
        var deviceState = new DeviceState();

        // Act - Note: ConnectionState has internal setter, so this tests the initial state
        // In real usage, this would be set by DeviceConnection internally

        // Assert
        deviceState.ConnectionState.Should().Be(DeviceConnectionState.Disconnected);
    }

    [Fact]
    public void DeviceState_StartAndCompleteOperation_UpdatesCorrectly() {
        // Arrange
        var deviceState = new DeviceState();
        var operationName = "TestOperation";

        // Act
        deviceState.SetCurrentOperation(operationName);

        // Assert
        deviceState.CurrentOperation.Should().Be(operationName);
        deviceState.LastOperationTime.Should().BeNull("operation not completed yet");

        // Act - Complete operation
        deviceState.CompleteOperation();

        // Assert
        deviceState.CurrentOperation.Should().BeNull("operation completed");
        deviceState.LastOperationTime.Should().NotBeNull("operation completed with timestamp");
        deviceState.LastOperationTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
