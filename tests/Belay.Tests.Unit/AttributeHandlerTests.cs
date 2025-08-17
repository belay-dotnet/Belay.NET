// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit;

using System.Reflection;
using Belay.Attributes;
using Belay.Core;
using Xunit;

/// <summary>
/// Unit tests for AttributeHandler error scenarios and edge cases.
/// </summary>
public class AttributeHandlerTests {
    public AttributeHandlerTests() {
        // Clear cache before each test to ensure test isolation
        SimpleCache.Clear();
    }

    [Fact]
    public void ExecuteMethod_NullDevice_ThrowsArgumentNullException() {
        // Arrange
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.SimpleTask))!;
        var args = Array.Empty<object>();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() =>
            AttributeHandler.ExecuteMethod<string>(null!, method, args));
    }

    [Fact]
    public void ExecuteMethod_NullMethod_ThrowsArgumentNullException() {
        // Arrange
        var mockDevice = new MockDeviceConnection();
        var args = Array.Empty<object>();

        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() =>
            AttributeHandler.ExecuteMethod<string>(mockDevice, null!, args));
    }

    [Fact]
    public void ExecuteMethod_NullArgs_ThrowsArgumentNullException() {
        // Arrange
        var mockDevice = new MockDeviceConnection();
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.SimpleTask))!;

        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() =>
            AttributeHandler.ExecuteMethod<string>(mockDevice, method, null!));
    }

    [Fact]
    public async Task ExecuteMethod_ValidTaskAttribute_ExecutesSuccessfully() {
        // Arrange
        var mockDevice = new MockDeviceConnection();
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.SimpleTask))!;
        var args = Array.Empty<object>();

        // Act
        var result = await AttributeHandler.ExecuteMethod<string>(mockDevice, method, args);

        // Assert
        Assert.NotNull(result);
        Assert.Single(mockDevice.ExecutedCode);
    }

    [Fact]
    public async Task ExecuteMethod_TaskWithTimeout_RespectsTimeout() {
        // Arrange
        var mockDevice = new MockDeviceConnection();
        mockDevice.SimulateTimeout = true;
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.TaskWithTimeout))!;
        var args = Array.Empty<object>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DeviceException>(() =>
            AttributeHandler.ExecuteMethod<string>(mockDevice, method, args));

        Assert.Contains("timed out", exception.Message);
        Assert.Contains("5000ms", exception.Message);
    }

    [Fact]
    public async Task ExecuteMethod_SetupAttributeWithTimeout_RespectsTimeout() {
        // Arrange
        var mockDevice = new MockDeviceConnection();
        mockDevice.SimulateTimeout = true;
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.SetupWithTimeout))!;
        var args = Array.Empty<object>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DeviceException>(() =>
            AttributeHandler.ExecuteMethod<string>(mockDevice, method, args));

        Assert.Contains("timed out", exception.Message);
        Assert.Contains("10000ms", exception.Message);
    }

    [Fact]
    public async Task ExecuteMethod_TeardownAttributeWithTimeout_RespectsTimeout() {
        // Arrange
        var mockDevice = new MockDeviceConnection();
        mockDevice.SimulateTimeout = true;
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.TeardownWithTimeout))!;
        var args = Array.Empty<object>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DeviceException>(() =>
            AttributeHandler.ExecuteMethod<string>(mockDevice, method, args));

        Assert.Contains("timed out", exception.Message);
        Assert.Contains("3000ms", exception.Message);
    }

    [Fact]
    public async Task ExecuteMethod_PythonCodeAttributeWithInvalidParameter_ThrowsArgumentException() {
        // Arrange
        var mockDevice = new MockDeviceConnection();
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.InvalidParameterName))!;
        var args = new object[] { "test" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            AttributeHandler.ExecuteMethod<string>(mockDevice, method, args));

        Assert.Contains("unreplaced placeholders", exception.Message);
    }

    [Fact]
    public async Task ExecuteMethod_PythonCodeAttributeWithDangerousTemplate_ThrowsArgumentException() {
        // Arrange
        var mockDevice = new MockDeviceConnection();
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.DangerousTemplate))!;
        var args = new object[] { "test" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            AttributeHandler.ExecuteMethod<string>(mockDevice, method, args));

        Assert.Contains("security validation", exception.Message);
    }

    [Fact]
    public async Task ExecuteMethod_MethodWithoutAttributes_GeneratesSimpleCall() {
        // Arrange
        var mockDevice = new MockDeviceConnection();
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.NoAttributes))!;
        var args = new object[] { 42 };

        // Act
        await AttributeHandler.ExecuteMethod<string>(mockDevice, method, args);

        // Assert
        Assert.Single(mockDevice.ExecutedCode);
        var generatedCode = mockDevice.ExecutedCode[0];
        Assert.Contains("no_attributes(42)", generatedCode);
    }

    [Fact]
    public async Task ExecuteMethod_TaskAttributeWithSpecialCharacters_SanitizesInput() {
        // Arrange
        var mockDevice = new MockDeviceConnection();
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.TaskWithString))!;
        var args = new object[] { "test'string\"with\nspecial\tchars" };

        // Act
        await AttributeHandler.ExecuteMethod<string>(mockDevice, method, args);

        // Assert
        Assert.Single(mockDevice.ExecutedCode);
        var generatedCode = mockDevice.ExecutedCode[0];
        Assert.DoesNotContain("test'string\"", generatedCode); // Should be escaped
        Assert.Contains("\\'", generatedCode); // Should contain escaped quotes
    }

    [Fact]
    public async Task ExecuteMethod_MultipleAttributeTypes_TaskTakesPrecedence() {
        // Arrange
        var mockDevice = new MockDeviceConnection();
        mockDevice.SimulateTimeout = true;
        var method = typeof(TestMethods).GetMethod(nameof(TestMethods.MultipleAttributes))!;
        var args = Array.Empty<object>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DeviceException>(() =>
            AttributeHandler.ExecuteMethod<string>(mockDevice, method, args));

        // TaskAttribute timeout (8000ms) should take precedence over SetupAttribute (12000ms)
        Assert.Contains("8000ms", exception.Message);
    }

    // Test methods with various attribute configurations
    public static class TestMethods {
        [Task]
        public static void SimpleTask() { }

        [Task(TimeoutMs = 5000)]
        public static void TaskWithTimeout() { }

        [Setup(TimeoutMs = 10000)]
        public static void SetupWithTimeout() { }

        [Teardown(TimeoutMs = 3000)]
        public static void TeardownWithTimeout() { }

        [PythonCode("test({invalid-param})", EnableParameterSubstitution = true)]
        public static void InvalidParameterName(string invalidParam) { }

        [PythonCode("os.system('{value}')", EnableParameterSubstitution = true)]
        public static void DangerousTemplate(string value) { }

        public static void NoAttributes(int value) { }

        [Task]
        public static void TaskWithString(string input) { }

        [Task(TimeoutMs = 8000)]
        [Setup(TimeoutMs = 12000)]
        public static void MultipleAttributes() { }
    }

    // Mock device connection for testing
    private class MockDeviceConnection : IDeviceConnection {
        public bool SimulateTimeout { get; set; }
        public List<string> ExecutedCode { get; } = new();

        public async Task<string> ExecutePython(string code, CancellationToken cancellationToken = default) {
            this.ExecutedCode.Add(code);

            if (this.SimulateTimeout) {
                // Simulate a long-running operation that gets cancelled by the timeout
                try {
                    await Task.Delay(15000, cancellationToken); // Longer delay to ensure timeout
                }
                catch (OperationCanceledException) {
                    // Re-throw as expected by timeout logic
                    throw;
                }
            }

            return "mock_result";
        }

        public async Task<T> ExecutePython<T>(string code, CancellationToken cancellationToken = default) {
            this.ExecutedCode.Add(code);

            if (this.SimulateTimeout) {
                // Simulate a long-running operation that gets cancelled by the timeout
                try {
                    await Task.Delay(15000, cancellationToken); // Longer delay to ensure timeout
                }
                catch (OperationCanceledException) {
                    // Re-throw as expected by timeout logic
                    throw;
                }
            }

            // Simple conversion for testing
            if (typeof(T) == typeof(string)) {
                return (T)(object)"mock_result";
            }

            return default(T)!;
        }

        // IDeviceConnection interface implementation
        public Task WriteFile(string devicePath, byte[] data, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<byte[]> ReadFile(string devicePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());

        public Task DeleteFile(string devicePath, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string[]> ListFiles(string devicePath = "/", CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<string>());

        public Task Connect(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Disconnect() =>
            Task.CompletedTask;

        public bool IsConnected => true;

        public string DeviceInfo => "Mock Device";

        public string ConnectionString => "mock://device";

        public void Dispose() { }
    }
}
