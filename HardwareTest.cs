// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

/// <summary>
/// Hardware integration test for Priority 2: Hardware Validation.
/// Tests SimplifiedDevice, DirectExecutor, and DeviceConnection with real MicroPython hardware.
/// </summary>
public class HardwareTest
{
    private static readonly string[] TestDevices = {
        "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94",
        "/dev/usb/tty-STM32_STLink-066FFF303430484257255318"
    };
    
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Belay.NET Hardware Integration Test");
        Console.WriteLine("Priority 2: Simplified Architecture Validation");
        Console.WriteLine("==============================================");
        
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        int successfulDevices = 0;
        int totalTests = 0;
        int passedTests = 0;

        foreach (var devicePath in TestDevices)
        {
            Console.WriteLine($"\n🔍 Testing Device: {devicePath}");
            Console.WriteLine(new string('=', 80));
            
            try
            {
                var (tests, passed) = await RunDeviceTests(devicePath, loggerFactory);
                totalTests += tests;
                passedTests += passed;
                
                if (passed == tests)
                {
                    successfulDevices++;
                    Console.WriteLine($"✅ Device PASSED all tests ({passed}/{tests})");
                }
                else
                {
                    Console.WriteLine($"⚠️  Device PARTIAL ({passed}/{tests} tests passed)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Device FAILED: {ex.Message}");
                Console.WriteLine($"   Exception: {ex.GetType().Name}");
            }
        }

        Console.WriteLine("\n" + new string('=', 80));
        Console.WriteLine("HARDWARE INTEGRATION TEST SUMMARY");
        Console.WriteLine($"Devices Tested: {TestDevices.Length}");
        Console.WriteLine($"Fully Successful: {successfulDevices}");
        Console.WriteLine($"Total Tests: {totalTests}");
        Console.WriteLine($"Passed Tests: {passedTests}");
        Console.WriteLine($"Success Rate: {(totalTests > 0 ? (passedTests * 100 / totalTests) : 0)}%");
        
        if (successfulDevices > 0)
        {
            Console.WriteLine("\n✅ HARDWARE VALIDATION SUCCESSFUL");
            Console.WriteLine("   Simplified architecture is working with physical hardware!");
            return 0;
        }
        else
        {
            Console.WriteLine("\n❌ HARDWARE VALIDATION FAILED");
            Console.WriteLine("   No devices could be fully validated. Check connections and permissions.");
            return 1;
        }
    }

    private static async Task<(int total, int passed)> RunDeviceTests(
        string devicePath,
        ILoggerFactory loggerFactory)
    {
        var connectionLogger = loggerFactory.CreateLogger<DeviceConnection>();
        var deviceLogger = loggerFactory.CreateLogger<SimplifiedDevice>();
        
        int totalTests = 0;
        int passedTests = 0;

        // Test 1: Basic Connection
        totalTests++;
        Console.WriteLine("🔌 Test 1: Basic Connection");
        try
        {
            await TestBasicConnection(devicePath, connectionLogger, deviceLogger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }

        // Test 2: Python Execution
        totalTests++;
        Console.WriteLine("🐍 Test 2: Python Code Execution");
        try
        {
            await TestPythonExecution(devicePath, connectionLogger, deviceLogger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }

        // Test 3: File Operations
        totalTests++;
        Console.WriteLine("📁 Test 3: File Operations");
        try
        {
            await TestFileOperations(devicePath, connectionLogger, deviceLogger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }

        // Test 4: Typed Execution
        totalTests++;
        Console.WriteLine("🔢 Test 4: Typed Execution");
        try
        {
            await TestTypedExecution(devicePath, connectionLogger, deviceLogger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }

        // Test 5: System Information
        totalTests++;
        Console.WriteLine("ℹ️  Test 5: System Information");
        try
        {
            await TestSystemInformation(devicePath, connectionLogger, deviceLogger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }

        // Test 6: Error Handling
        totalTests++;
        Console.WriteLine("⚠️  Test 6: Error Handling");
        try
        {
            await TestErrorHandling(devicePath, connectionLogger, deviceLogger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }

        // Test 7: Connection Stability
        totalTests++;
        Console.WriteLine("🔄 Test 7: Connection Stability");
        try
        {
            await TestConnectionStability(devicePath, connectionLogger, deviceLogger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }

        return (totalTests, passedTests);
    }

    private static async Task TestBasicConnection(
        string devicePath,
        ILogger<DeviceConnection> connectionLogger,
        ILogger<SimplifiedDevice> deviceLogger)
    {
        var connection = new DeviceConnection(
            DeviceConnection.ConnectionType.Serial,
            devicePath,
            connectionLogger);

        using var device = new SimplifiedDevice(connection, deviceLogger);
        
        await device.Connect();
        
        if (!device.IsConnected)
            throw new InvalidOperationException("Device reports not connected after Connect()");
            
        if (string.IsNullOrEmpty(device.ConnectionString))
            throw new InvalidOperationException("Device connection string is empty");
            
        await device.Disconnect();
    }

    private static async Task TestPythonExecution(
        string devicePath,
        ILogger<DeviceConnection> connectionLogger,
        ILogger<SimplifiedDevice> deviceLogger)
    {
        var connection = new DeviceConnection(
            DeviceConnection.ConnectionType.Serial,
            devicePath,
            connectionLogger);

        using var device = new SimplifiedDevice(connection, deviceLogger);
        
        await device.Connect();
        
        // Test basic print
        var result = await device.ExecutePython("print('Hardware Test'); 42");
        if (string.IsNullOrEmpty(result))
            throw new InvalidOperationException("No result from Python execution");
            
        // Test calculation
        var calc = await device.ExecutePython("2 + 3 * 4");
        if (!calc.Contains("14"))
            throw new InvalidOperationException($"Unexpected calculation result: {calc}");
        
        await device.Disconnect();
    }

    private static async Task TestFileOperations(
        string devicePath,
        ILogger<DeviceConnection> connectionLogger,
        ILogger<SimplifiedDevice> deviceLogger)
    {
        var connection = new DeviceConnection(
            DeviceConnection.ConnectionType.Serial,
            devicePath,
            connectionLogger);

        using var device = new SimplifiedDevice(connection, deviceLogger);
        
        await device.Connect();
        
        const string testFile = "/hardware_test.txt";
        const string testContent = "Belay.NET Hardware Integration Test Content";
        
        // Write file
        await device.WriteFile(testFile, Encoding.UTF8.GetBytes(testContent));
        
        // Read file back
        var readBytes = await device.ReadFile(testFile);
        var readContent = Encoding.UTF8.GetString(readBytes);
        
        if (readContent != testContent)
            throw new InvalidOperationException($"File content mismatch: expected '{testContent}', got '{readContent}'");
        
        // List files
        var files = await device.ListFiles("/");
        if (!Array.Exists(files, f => f == "hardware_test.txt"))
            throw new InvalidOperationException("Test file not found in directory listing");
        
        // Delete file
        await device.DeleteFile(testFile);
        
        await device.Disconnect();
    }

    private static async Task TestTypedExecution(
        string devicePath,
        ILogger<DeviceConnection> connectionLogger,
        ILogger<SimplifiedDevice> deviceLogger)
    {
        var connection = new DeviceConnection(
            DeviceConnection.ConnectionType.Serial,
            devicePath,
            connectionLogger);

        using var device = new SimplifiedDevice(connection, deviceLogger);
        
        await device.Connect();
        
        // Test integer result
        var intResult = await device.ExecutePython<int>("100 + 23");
        if (intResult != 123)
            throw new InvalidOperationException($"Integer execution failed: expected 123, got {intResult}");
        
        // Test string result
        var strResult = await device.ExecutePython<string>("'Hello ' + 'World'");
        if (!strResult.Contains("Hello World"))
            throw new InvalidOperationException($"String execution failed: got '{strResult}'");
        
        await device.Disconnect();
    }

    private static async Task TestSystemInformation(
        string devicePath,
        ILogger<DeviceConnection> connectionLogger,
        ILogger<SimplifiedDevice> deviceLogger)
    {
        var connection = new DeviceConnection(
            DeviceConnection.ConnectionType.Serial,
            devicePath,
            connectionLogger);

        using var device = new SimplifiedDevice(connection, deviceLogger);
        
        await device.Connect();
        
        var sysInfo = await device.ExecutePython(@"
import sys
import gc
platform = sys.platform
memory = gc.mem_free()
f'Platform: {platform}, Memory: {memory}'
");
        
        if (!sysInfo.Contains("Platform:") || !sysInfo.Contains("Memory:"))
            throw new InvalidOperationException($"Invalid system info: {sysInfo}");
        
        Console.WriteLine($"     System: {sysInfo.Trim()}");
        
        await device.Disconnect();
    }

    private static async Task TestErrorHandling(
        string devicePath,
        ILogger<DeviceConnection> connectionLogger,
        ILogger<SimplifiedDevice> deviceLogger)
    {
        var connection = new DeviceConnection(
            DeviceConnection.ConnectionType.Serial,
            devicePath,
            connectionLogger);

        await connection.ConnectAsync();
        
        // Test syntax error
        bool syntaxErrorCaught = false;
        try
        {
            await connection.ExecuteAsync("invalid syntax !!!");
        }
        catch (Exception)
        {
            syntaxErrorCaught = true;
        }
        
        if (!syntaxErrorCaught)
            throw new InvalidOperationException("Syntax error was not properly caught");
        
        // Test empty code
        bool emptyCodeErrorCaught = false;
        try
        {
            await connection.ExecuteAsync("");
        }
        catch (ArgumentException)
        {
            emptyCodeErrorCaught = true;
        }
        
        if (!emptyCodeErrorCaught)
            throw new InvalidOperationException("Empty code error was not properly caught");
        
        // Verify connection still works after errors
        var recovery = await connection.ExecuteAsync("1 + 1");
        if (!recovery.Contains("2"))
            throw new InvalidOperationException("Connection not recovered after errors");
        
        await connection.DisconnectAsync();
    }

    private static async Task TestConnectionStability(
        string devicePath,
        ILogger<DeviceConnection> connectionLogger,
        ILogger<SimplifiedDevice> deviceLogger)
    {
        var connection = new DeviceConnection(
            DeviceConnection.ConnectionType.Serial,
            devicePath,
            connectionLogger);

        // Test multiple connect/disconnect cycles
        for (int i = 0; i < 3; i++)
        {
            await connection.ConnectAsync();
            
            if (connection.State != DeviceConnectionState.Connected)
                throw new InvalidOperationException($"Connection state incorrect after connect: {connection.State}");
            
            var testResult = await connection.ExecuteAsync($"print('Cycle {i}'); {i + 1}");
            if (!testResult.Contains((i + 1).ToString()))
                throw new InvalidOperationException($"Execution failed in cycle {i}");
            
            await connection.DisconnectAsync();
            
            if (connection.State != DeviceConnectionState.Disconnected)
                throw new InvalidOperationException($"Connection state incorrect after disconnect: {connection.State}");
            
            // Brief pause between cycles
            await Task.Delay(100);
        }
    }
}