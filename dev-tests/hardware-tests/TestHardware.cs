// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

/// <summary>
/// Simple hardware validation runner for Priority 2: Hardware Validation.
/// Tests the simplified architecture with actual hardware devices.
/// </summary>
public class TestHardware
{
    private static readonly string[] AvailableDevices = {
        "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94",
        "/dev/usb/tty-STM32_STLink-066FFF303430484257255318"
    };
    
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Belay.NET Hardware Validation Test");
        Console.WriteLine("===================================");
        Console.WriteLine("Available devices:");
        for (int i = 0; i < AvailableDevices.Length; i++)
        {
            Console.WriteLine($"  {i + 1}. {AvailableDevices[i]}");
        }
        Console.WriteLine();

        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        var connectionLogger = loggerFactory.CreateLogger<DeviceConnection>();
        var deviceLogger = loggerFactory.CreateLogger<SimplifiedDevice>();

        int successCount = 0;
        int totalDevices = AvailableDevices.Length;

        for (int i = 0; i < AvailableDevices.Length; i++)
        {
            string devicePath = AvailableDevices[i];
            Console.WriteLine($"🔍 Testing device {i + 1}/{totalDevices}:");
            Console.WriteLine($"   Path: {devicePath}");
            Console.WriteLine(new string('-', 60));
            
            try
            {
                await TestDevice(devicePath, connectionLogger, deviceLogger);
                Console.WriteLine($"✅ Device {i + 1} validation PASSED");
                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Device {i + 1} validation FAILED: {ex.Message}");
                
                // Show more details for debugging
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
                }
            }
            
            Console.WriteLine();
        }

        Console.WriteLine($"Hardware Validation Summary: {successCount}/{totalDevices} devices validated");
        
        if (successCount > 0)
        {
            Console.WriteLine("✅ HARDWARE VALIDATION SUCCESSFUL - Simplified architecture working!");
            return 0;
        }
        else
        {
            Console.WriteLine("❌ HARDWARE VALIDATION FAILED - Check device connections and permissions");
            return 1;
        }
    }

    private static async Task TestDevice(
        string devicePath,
        ILogger<DeviceConnection> connectionLogger, 
        ILogger<SimplifiedDevice> deviceLogger)
    {
        Console.WriteLine("  🔌 Creating device connection...");
        
        var connection = new DeviceConnection(
            DeviceConnection.ConnectionType.Serial, 
            devicePath, 
            connectionLogger);
            
        using var device = new SimplifiedDevice(connection, deviceLogger);

        Console.WriteLine("  🔗 Connecting to device...");
        await device.Connect();
        Console.WriteLine($"     Connected: {device.IsConnected}");

        Console.WriteLine("  🧮 Testing basic Python execution...");
        var result = await device.ExecutePython("print('Belay.NET Test'); 2 + 3");
        Console.WriteLine($"     Result: {result.Trim()}");

        Console.WriteLine("  📁 Testing file operations...");
        const string testFile = "/belay_test.txt";
        const string testContent = "Hardware Validation Test";
        
        await device.WriteFile(testFile, System.Text.Encoding.UTF8.GetBytes(testContent));
        var readContent = await device.ReadFile(testFile);
        var readText = System.Text.Encoding.UTF8.GetString(readContent);
        
        if (readText != testContent)
        {
            throw new InvalidOperationException($"File content mismatch: expected '{testContent}', got '{readText}'");
        }
        
        await device.DeleteFile(testFile);
        Console.WriteLine("     File operations working");

        Console.WriteLine("  ⚙️ Testing system information...");
        var sysInfo = await device.ExecutePython(@"
import sys
import gc
f'Platform: {sys.platform}, Memory: {gc.mem_free()} bytes'
");
        Console.WriteLine($"     System: {sysInfo.Trim()}");

        Console.WriteLine("  🔌 Disconnecting...");
        await device.Disconnect();
        Console.WriteLine("     Disconnected successfully");
    }
}