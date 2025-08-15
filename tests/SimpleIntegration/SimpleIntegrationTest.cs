// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Belay.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Belay.Tests;

/// <summary>
/// Simple integration test for the simplified architecture.
/// Tests basic device communication using the new consolidated DeviceConnection.
/// </summary>
public class SimpleIntegrationTest
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🚀 Belay.NET Simple Integration Test");
        Console.WriteLine("Testing simplified architecture with subprocess connection");
        Console.WriteLine(new string('=', 60));

        var test = new SimpleIntegrationTest();
        var success = await test.RunAllTests();

        Console.WriteLine(new string('=', 60));
        if (success)
        {
            Console.WriteLine("🎉 ALL TESTS PASSED - Simplified architecture is working!");
            return 0;
        }
        else
        {
            Console.WriteLine("❌ SOME TESTS FAILED - Check output above for details");
            return 1;
        }
    }

    public async Task<bool> RunAllTests()
    {
        var results = new List<bool>();

        try
        {
            // Test device creation and connection
            results.Add(await TestDeviceCreation());
            results.Add(await TestBasicExecution());
            results.Add(await TestFileTransfer());
            results.Add(await TestErrorHandling());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test suite failed with exception: {ex.Message}");
            return false;
        }

        return results.All(r => r);
    }

    private async Task<bool> TestDeviceCreation()
    {
        Console.WriteLine("\n📱 Testing Device Creation...");
        
        try
        {
            // Test hardware device creation (try RPI Pico first)
            var device = Device.FromConnectionString("serial:/dev/ttyACM0", 
                LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug)));
            
            Console.WriteLine("   ✅ Device created successfully");
            
            await device.ConnectAsync();
            Console.WriteLine("   ✅ Device connected successfully");
            Console.WriteLine($"   ✅ Connection state: {device.ConnectionState}");
            
            await device.DisconnectAsync();
            Console.WriteLine("   ✅ Device disconnected successfully");
            
            device.Dispose();
            Console.WriteLine("   ✅ Device disposed successfully");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Device creation test failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> TestBasicExecution()
    {
        Console.WriteLine("\n⚡ Testing Basic Code Execution...");
        
        try
        {
            using var device = Device.FromConnectionString("serial:/dev/ttyACM0",
                LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug)));
            
            await device.ConnectAsync();
            
            // Test simple expression
            var result = await device.ExecuteAsync("2 + 3");
            Console.WriteLine($"   ✅ Basic math: {result.Trim()}");
            
            // Test string operation
            var stringResult = await device.ExecuteAsync("'Hello' + ' ' + 'World'");
            Console.WriteLine($"   ✅ String operation: {stringResult.Trim()}");
            
            // Test typed execution
            var typedResult = await device.ExecuteAsync<int>("10 * 5");
            Console.WriteLine($"   ✅ Typed execution: {typedResult}");
            
            // Test Python version detection
            var version = await device.ExecuteAsync("import sys; sys.implementation.name");
            Console.WriteLine($"   ✅ Python implementation: {version.Trim()}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Basic execution test failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> TestFileTransfer()
    {
        Console.WriteLine("\n📁 Testing Enhanced File Transfer...");
        
        try
        {
            using var device = Device.FromConnectionString("serial:/dev/ttyACM0",
                LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug)));
            
            await device.ConnectAsync();
            
            // Test file write and read with base64 encoding
            var testData = "Hello from Belay.NET simplified architecture!\nLine 2\nLine 3"u8.ToArray();
            var remotePath = "/tmp/belay_test.txt";
            
            // Write file using enhanced WriteFileAsync (with base64 encoding)
            await device.PutFileAsync("/dev/null", remotePath); // Use a dummy local file
            Console.WriteLine("   ✅ File write operation completed");
            
            // Write specific data using the new WriteFileAsync method directly
            var connection = (DeviceConnection)device.GetType()
                .GetField("connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(device)!;
            
            await connection.WriteFileAsync(remotePath, testData);
            Console.WriteLine("   ✅ Binary data written using base64 encoding");
            
            // Read file back using enhanced GetFileAsync
            var readData = await device.GetFileAsync(remotePath);
            var readString = System.Text.Encoding.UTF8.GetString(readData);
            
            Console.WriteLine($"   ✅ File read back: {readString.Length} bytes");
            Console.WriteLine($"   ✅ Content preview: {readString.Substring(0, Math.Min(30, readString.Length))}...");
            
            // Verify content integrity
            var originalString = System.Text.Encoding.UTF8.GetString(testData);
            var contentMatch = readString.Equals(originalString, StringComparison.Ordinal);
            Console.WriteLine($"   ✅ Content integrity: {contentMatch}");
            
            // Clean up
            await device.ExecuteAsync($"import os; os.remove('{remotePath}')");
            Console.WriteLine("   ✅ Test file cleaned up");
            
            return contentMatch;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ File transfer test failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> TestErrorHandling()
    {
        Console.WriteLine("\n🛡️ Testing Error Handling...");
        
        try
        {
            using var device = Device.FromConnectionString("serial:/dev/ttyACM0",
                LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug)));
            
            await device.ConnectAsync();
            
            // Test syntax error handling
            try
            {
                await device.ExecuteAsync("invalid syntax here");
                Console.WriteLine("   ❌ Expected exception for syntax error");
                return false;
            }
            catch (DeviceException ex)
            {
                Console.WriteLine($"   ✅ Syntax error handled: {ex.Message.Substring(0, Math.Min(50, ex.Message.Length))}...");
            }
            
            // Test runtime error handling
            try
            {
                await device.ExecuteAsync("1 / 0");
                Console.WriteLine("   ❌ Expected exception for division by zero");
                return false;
            }
            catch (DeviceException ex)
            {
                Console.WriteLine($"   ✅ Runtime error handled: {ex.Message.Substring(0, Math.Min(50, ex.Message.Length))}...");
            }
            
            // Test device recovery after error
            var recoveryResult = await device.ExecuteAsync("7 * 6");
            Console.WriteLine($"   ✅ Device recovery after error: {recoveryResult.Trim()}");
            
            // Test file operation with invalid path
            try
            {
                await device.GetFileAsync("/invalid/path/that/does/not/exist.txt");
                Console.WriteLine("   ❌ Expected exception for invalid file path");
                return false;
            }
            catch (DeviceException ex)
            {
                Console.WriteLine($"   ✅ File error handled: {ex.Message.Substring(0, Math.Min(50, ex.Message.Length))}...");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Error handling test failed: {ex.Message}");
            return false;
        }
    }
}