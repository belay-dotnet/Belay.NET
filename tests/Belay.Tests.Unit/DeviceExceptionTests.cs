// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit;

using Belay.Core;
using Xunit;

/// <summary>
/// Unit tests for DeviceException error handling and context preservation.
/// </summary>
public class DeviceExceptionTests {

    [Fact]
    public void DeviceException_MessageConstructor_SetsDefaultMessage() {
        // Act
        var exception = new DeviceException("Default device error");

        // Assert
        Assert.NotNull(exception.Message);
        Assert.NotEmpty(exception.Message);
        Assert.Equal("Default device error", exception.Message);
    }

    [Fact]
    public void DeviceException_MessageConstructor_SetsMessage() {
        // Arrange
        var message = "Test device error";

        // Act
        var exception = new DeviceException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void DeviceException_MessageAndInnerExceptionConstructor_SetsValues() {
        // Arrange
        var message = "Device operation failed";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new DeviceException(message, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(innerException, exception.InnerException);
    }

    [Fact]
    public void DeviceException_InheritanceChain_IsCorrect() {
        // Act
        var exception = new DeviceException("Test inheritance");

        // Assert
        Assert.IsAssignableFrom<Exception>(exception);
        Assert.Equal(typeof(DeviceException), exception.GetType());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Simple message")]
    [InlineData("Complex message with symbols !@#$%^&*()")]
    [InlineData("Message with\nnewlines\nand\ttabs")]
    public void DeviceException_VariousMessageFormats_AreHandledCorrectly(string message) {
        // Act
        var exception = new DeviceException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void DeviceException_NullMessage_IsHandledGracefully() {
        // Act & Assert
        var exception = new DeviceException((string)null!);
        Assert.NotNull(exception); // Should not throw during construction
    }

    [Fact]
    public void DeviceException_NullInnerException_IsHandledGracefully() {
        // Act
        var exception = new DeviceException("Test message", (Exception)null!);

        // Assert
        Assert.Equal("Test message", exception.Message);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void DeviceException_NestedExceptions_PreservesInnerExceptionChain() {
        // Arrange
        var rootCause = new ArgumentException("Root cause");
        var middleException = new InvalidOperationException("Middle layer", rootCause);

        // Act
        var deviceException = new DeviceException("Device layer", middleException);

        // Assert
        Assert.Equal("Device layer", deviceException.Message);
        Assert.Equal(middleException, deviceException.InnerException);
        Assert.Equal(rootCause, deviceException.InnerException?.InnerException);
    }

    [Fact]
    public void DeviceException_ToString_IncludesMessageAndType() {
        // Arrange
        var message = "Test device error";
        var exception = new DeviceException(message);

        // Act
        var toString = exception.ToString();

        // Assert
        Assert.Contains(message, toString);
        Assert.Contains("DeviceException", toString);
    }

    [Fact]
    public void DeviceException_ToString_WithInnerException_IncludesBoth() {
        // Arrange
        var innerMessage = "Inner error details";
        var innerException = new ArgumentException(innerMessage);
        var deviceException = new DeviceException("Device error", innerException);

        // Act
        var toString = deviceException.ToString();

        // Assert
        Assert.Contains("Device error", toString);
        Assert.Contains("DeviceException", toString);
        Assert.Contains(innerMessage, toString);
        Assert.Contains("ArgumentException", toString);
    }
}
