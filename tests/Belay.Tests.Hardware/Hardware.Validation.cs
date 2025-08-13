// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;
using Xunit;

/// <summary>
/// Hardware validation tests for the simplified Belay.NET architecture.
/// Tests actual device communication with physical MicroPython hardware.
/// </summary>
public class HardwareValidationTests
{
    private readonly ILogger<HardwareValidationTests> logger;

    public HardwareValidationTests()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        this.logger = loggerFactory.CreateLogger<HardwareValidationTests>();
    }

    /// <summary>
    /// Tests basic SimplifiedDevice functionality with physical hardware.
    /// This test requires an actual MicroPython device to be connected.
    /// </summary>
    [Fact(Skip = "Requires physical hardware - run manually")]
    public async Task SimplifiedDevice_BasicCommunication_Success()
    {
        // Arrange
        const string devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var connectionLogger = loggerFactory.CreateLogger<DeviceConnection>();
        var deviceLogger = loggerFactory.CreateLogger<SimplifiedDevice>();
        
        var connection = new DeviceConnection(
            DeviceConnection.ConnectionType.Serial, 
            devicePath, 
            connectionLogger);
            
        var device = new SimplifiedDevice(connection, deviceLogger);

        try
        {
            // Act & Assert - Test connection
            await device.Connect();
            Assert.True(device.IsConnected, "Device should be connected");

            // Test basic Python execution
            var result = await device.ExecutePython("print('Hello from Belay.NET!')");
            Assert.Contains("Hello from Belay.NET!", result);

            // Test typed execution  
            var mathResult = await device.ExecutePython<int>("2 + 3");
            Assert.Equal(5, mathResult);

            // Test file operations
            const string testFile = "/test_belay.txt";
            const string testContent = "Belay.NET Hardware Test";
            
            await device.WriteFile(testFile, System.Text.Encoding.UTF8.GetBytes(testContent));
            
            var readContent = await device.ReadFile(testFile);
            var readText = System.Text.Encoding.UTF8.GetString(readContent);
            Assert.Equal(testContent, readText);

            // Test file listing
            var files = await device.ListFiles("/");
            Assert.Contains("test_belay.txt", files);

            // Cleanup
            await device.DeleteFile(testFile);
        }
        finally
        {
            await device.Disconnect();
        }
    }

    /// <summary>
    /// Tests DeviceConnection edge cases and error handling.
    /// </summary>
    [Fact(Skip = "Requires physical hardware - run manually")]
    public async Task DeviceConnection_EdgeCases_HandledProperly()
    {
        // Arrange
        const string devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<DeviceConnection>();
        
        var connection = new DeviceConnection(
            DeviceConnection.ConnectionType.Serial, 
            devicePath, 
            logger);

        try
        {
            // Test connection
            await connection.ConnectAsync();
            Assert.Equal(DeviceConnectionState.Connected, connection.State);

            // Test invalid code execution
            await Assert.ThrowsAsync<DeviceException>(async () =>
            {
                await connection.ExecuteAsync("invalid_python_syntax !!!");
            });

            // Test empty code
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await connection.ExecuteAsync("");
            });

            // Test null code
            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await connection.ExecuteAsync(null!);
            });

            // Test cancellation token
            using var cts = new CancellationTokenSource(100); // 100ms timeout
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await connection.ExecuteAsync("import time; time.sleep(1)", cts.Token);
            });

            // Test large data transfer
            var largeData = new string('X', 10000);
            var result = await connection.ExecuteAsync($"len('{largeData}')");
            Assert.Contains("10000", result);

            // Verify connection is still working after errors
            var recoveryTest = await connection.ExecuteAsync("1 + 1");
            Assert.Contains("2", recoveryTest);
        }
        finally
        {
            await connection.DisconnectAsync();
            Assert.Equal(DeviceConnectionState.Disconnected, connection.State);
        }
    }

    /// <summary>
    /// Tests attribute-based execution with DirectExecutor pattern.
    /// </summary>
    [Fact(Skip = "Requires physical hardware - run manually")]
    public async Task DirectExecutor_AttributeExecution_WorksCorrectly()
    {
        // Arrange
        const string devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var connectionLogger = loggerFactory.CreateLogger<DeviceConnection>();
        var deviceLogger = loggerFactory.CreateLogger<SimplifiedDevice>();
        
        var connection = new DeviceConnection(
            DeviceConnection.ConnectionType.Serial, 
            devicePath, 
            connectionLogger);
            
        var device = new SimplifiedDevice(connection, deviceLogger);

        try
        {
            // Connect to device
            await device.Connect();

            // Test method execution using attribute system
            var testClass = new TestMicroPythonMethods();
            var method = typeof(TestMicroPythonMethods).GetMethod(nameof(TestMicroPythonMethods.GetSystemInfo));
            
            var result = await device.ExecuteMethod<string>(method!, new object[0]);
            
            // Verify the result contains expected system information
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            
            // Test method with parameters
            var addMethod = typeof(TestMicroPythonMethods).GetMethod(nameof(TestMicroPythonMethods.Add));
            var addResult = await device.ExecuteMethod<int>(addMethod!, new object[] { 10, 15 });
            
            Assert.Equal(25, addResult);
        }
        finally
        {
            await device.Disconnect();
        }
    }

    /// <summary>
    /// Tests reconnection scenarios and connection stability.
    /// </summary>
    [Fact(Skip = "Requires physical hardware - run manually")]
    public async Task DeviceConnection_ReconnectionScenarios_HandlesProperly()
    {
        // Arrange
        const string devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<DeviceConnection>();
        
        var connection = new DeviceConnection(
            DeviceConnection.ConnectionType.Serial, 
            devicePath, 
            logger);

        // Test initial connection
        await connection.ConnectAsync();
        Assert.Equal(DeviceConnectionState.Connected, connection.State);

        // Test basic operation
        var result1 = await connection.ExecuteAsync("print('Test 1')");
        Assert.Contains("Test 1", result1);

        // Test disconnection
        await connection.DisconnectAsync();
        Assert.Equal(DeviceConnectionState.Disconnected, connection.State);

        // Test reconnection
        await connection.ConnectAsync();
        Assert.Equal(DeviceConnectionState.Connected, connection.State);

        // Test operation after reconnection
        var result2 = await connection.ExecuteAsync("print('Test 2')");
        Assert.Contains("Test 2", result2);

        // Test multiple rapid connections/disconnections
        for (int i = 0; i < 3; i++)
        {
            await connection.DisconnectAsync();
            await Task.Delay(100); // Brief pause
            await connection.ConnectAsync();
            
            var testResult = await connection.ExecuteAsync($"print('Rapid test {i}')");
            Assert.Contains($"Rapid test {i}", testResult);
        }

        // Final cleanup
        await connection.DisconnectAsync();
    }
}

/// <summary>
/// Test class with MicroPython methods for attribute testing.
/// </summary>
public class TestMicroPythonMethods
{
    [Belay.Attributes.Task]
    public static string GetSystemInfo()
    {
        return @"
import sys
import gc
info = {
    'platform': sys.platform,
    'implementation': sys.implementation.name,
    'free_memory': gc.mem_free()
}
str(info)
";
    }

    [Belay.Attributes.Task]
    public static string Add(int a, int b)
    {
        return $"{a} + {b}";
    }
}