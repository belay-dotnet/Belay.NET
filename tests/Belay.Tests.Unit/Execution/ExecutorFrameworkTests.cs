// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit.Execution;

using System;
using System.Reflection;
using System.Threading.Tasks;
using Belay.Attributes;
using Belay.Core;
using Belay.Core.Communication;
using Belay.Core.Execution;
using Belay.Core.Sessions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

/// <summary>
/// Tests for the executor framework method interception capabilities.
/// </summary>
public class ExecutorFrameworkTests {
    private readonly Mock<IDeviceCommunication> mockCommunication;
    private readonly Mock<IDeviceSessionManager> mockSessionManager;
    private readonly Mock<ILogger<Device>> mockLogger;
    private readonly Device device;

    public ExecutorFrameworkTests() {
        this.mockCommunication = new Mock<IDeviceCommunication>();
        this.mockSessionManager = new Mock<IDeviceSessionManager>();
        this.mockLogger = new Mock<ILogger<Device>>();
        
        this.device = new Device(
            this.mockCommunication.Object,
            this.mockSessionManager.Object,
            this.mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteMethodAsync_WithTaskAttribute_UsesTaskExecutor() {
        // Arrange
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.TaskMethod))!;
        this.mockCommunication.Setup(x => x.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync("test_result");

        // Act
        var result = await this.device.ExecuteMethodAsync<string>(method, null, new object[] { 42 });

        // Assert
        Assert.Equal("test_result", result);
    }

    [Fact]
    public async Task ExecuteMethodAsync_WithSetupAttribute_UsesSetupExecutor() {
        // Arrange
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.SetupMethod))!;
        this.mockCommunication.Setup(x => x.ExecuteAsync<object>(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((object?)null);

        // Act
        await this.device.ExecuteMethodAsync(method);

        // Assert - No exception thrown means success
    }

    [Fact]
    public async Task ExecuteMethodAsync_WithThreadAttribute_UsesThreadExecutor() {
        // Arrange
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.ThreadMethod))!;
        this.mockCommunication.Setup(x => x.ExecuteAsync<object>(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((object?)null);

        // Act
        await this.device.ExecuteMethodAsync(method);

        // Assert - No exception thrown means success
    }

    [Fact]
    public async Task ExecuteMethodAsync_WithTeardownAttribute_UsesTeardownExecutor() {
        // Arrange
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.TeardownMethod))!;
        this.mockCommunication.Setup(x => x.ExecuteAsync<object>(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((object?)null);

        // Act
        await this.device.ExecuteMethodAsync(method);

        // Assert - No exception thrown means success
    }

    [Fact]
    public async Task ExecuteMethodAsync_WithoutAttribute_ThrowsInvalidOperationException() {
        // Arrange
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.MethodWithoutAttribute))!;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => this.device.ExecuteMethodAsync<string>(method));
    }

    [Fact]
    public void TaskExecutor_CanHandle_ReturnsTrueForTaskAttribute() {
        // Arrange
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.TaskMethod))!;

        // Act
        var canHandle = this.device.Task.CanHandle(method);

        // Assert
        Assert.True(canHandle);
    }

    [Fact]
    public void SetupExecutor_CanHandle_ReturnsTrueForSetupAttribute() {
        // Arrange
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.SetupMethod))!;

        // Act
        var canHandle = this.device.Setup.CanHandle(method);

        // Assert
        Assert.True(canHandle);
    }
}

/// <summary>
/// Test methods with various attributes for testing the executor framework.
/// </summary>
public class TestMethods {
    [Task]
    public string TaskMethod(int value) => "task_result";

    [Setup]
    public void SetupMethod() { }

    [Thread]
    public void ThreadMethod() { }

    [Teardown]
    public void TeardownMethod() { }

    public string MethodWithoutAttribute() => "no_attribute";
}