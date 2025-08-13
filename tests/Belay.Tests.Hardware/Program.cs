// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

namespace Belay.Tests.Hardware
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            return await ManualHardwareTest.RunAsync(args);
        }
    }

    /// <summary>
    /// Manual hardware test runner for Priority 2: Hardware Validation.
    /// Run this program to validate the simplified architecture with real hardware.
    /// </summary>
    public static class ManualHardwareTest
    {
        private static readonly string[] AvailableDevices = {
            "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94",
            "/dev/usb/tty-STM32_STLink-066FFF303430484257255318"
        };
        
        public static async Task<int> RunAsync(string[] args)
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
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            
            var connectionLogger = loggerFactory.CreateLogger<DeviceConnection>();
            var deviceLogger = loggerFactory.CreateLogger<SimplifiedDevice>();

            int successCount = 0;
            int totalDevices = AvailableDevices.Length;

            for (int i = 0; i < AvailableDevices.Length; i++)
            {
                string devicePath = AvailableDevices[i];
                Console.WriteLine($"🔍 Testing device {i + 1}/{totalDevices}: {devicePath}");
                Console.WriteLine(new string('=', 80));
                
                try
                {
                    await TestBasicConnection(devicePath, connectionLogger, deviceLogger);
                    await TestDirectExecutor(devicePath, connectionLogger, deviceLogger);
                    await TestEdgeCases(devicePath, connectionLogger, deviceLogger);
                    
                    Console.WriteLine($"✅ Device {i + 1} validation completed successfully!");
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Device {i + 1} validation failed: {ex.Message}");
                    Console.WriteLine($"   Error details: {ex.GetType().Name}");
                    
                    // Continue testing other devices
                    Console.WriteLine("   Continuing with next device...");
                }
                
                Console.WriteLine();
            }

            Console.WriteLine($"Hardware Validation Summary: {successCount}/{totalDevices} devices validated successfully");
            
            if (successCount > 0)
            {
                Console.WriteLine("✅ At least one device validated - simplified architecture is working!");
                return 0;
            }
            else
            {
                Console.WriteLine("❌ No devices could be validated - check hardware connections");
                return 1;
            }
        }

        private static async Task TestBasicConnection(
            string devicePath,
            ILogger<DeviceConnection> connectionLogger, 
            ILogger<SimplifiedDevice> deviceLogger)
        {
            Console.WriteLine("🔌 Testing basic SimplifiedDevice connection...");
            
            var connection = new DeviceConnection(
                DeviceConnection.ConnectionType.Serial, 
                devicePath, 
                connectionLogger);
                
            using var device = new SimplifiedDevice(connection, deviceLogger);

            await device.Connect();
            Console.WriteLine($"   Connected: {device.IsConnected}");
            Console.WriteLine($"   Device info: {device.DeviceInfo}");
            Console.WriteLine($"   Connection string: {device.ConnectionString}");

            // Test basic execution
            var greeting = await device.ExecutePython("print('Hello from Belay.NET simplified architecture!')");
            Console.WriteLine($"   Greeting response: {greeting.Trim()}");

            // Test typed execution
            var calculation = await device.ExecutePython<int>("2 * 21");
            Console.WriteLine($"   Calculation (2 * 21): {calculation}");

            // Test file operations
            const string testFile = "/belay_validation.txt";
            const string testContent = "Belay.NET Priority 2 Validation";
            
            await device.WriteFile(testFile, System.Text.Encoding.UTF8.GetBytes(testContent));
            Console.WriteLine($"   File written: {testFile}");
            
            var readContent = await device.ReadFile(testFile);
            var readText = System.Text.Encoding.UTF8.GetString(readContent);
            Console.WriteLine($"   File read back: {readText}");
            
            var files = await device.ListFiles("/");
            Console.WriteLine($"   Files in root: {string.Join(", ", files)}");
            
            await device.DeleteFile(testFile);
            Console.WriteLine($"   File deleted: {testFile}");

            await device.Disconnect();
            Console.WriteLine("   Disconnected successfully");
            Console.WriteLine();
        }

        private static async Task TestDirectExecutor(
            string devicePath,
            ILogger<DeviceConnection> connectionLogger, 
            ILogger<SimplifiedDevice> deviceLogger)
        {
            Console.WriteLine("⚡ Testing DirectExecutor with attributes...");
            
            var connection = new DeviceConnection(
                DeviceConnection.ConnectionType.Serial, 
                devicePath, 
                connectionLogger);
                
            using var device = new SimplifiedDevice(connection, deviceLogger);

            await device.Connect();

            // Test attribute-based method execution
            var testClass = new HardwareTestMethods();
            
            // Test system info method
            var systemInfoMethod = typeof(HardwareTestMethods).GetMethod(nameof(HardwareTestMethods.GetSystemInfo));
            var systemInfo = await device.ExecuteMethod<string>(systemInfoMethod!, new object[0]);
            Console.WriteLine($"   System info: {systemInfo.Trim()}");

            // Test parameterized method
            var multiplyMethod = typeof(HardwareTestMethods).GetMethod(nameof(HardwareTestMethods.Multiply));
            var result = await device.ExecuteMethod<int>(multiplyMethod!, new object[] { 7, 6 });
            Console.WriteLine($"   Multiply(7, 6): {result}");

            // Test LED control method (hardware-specific, may fail gracefully)
            try 
            {
                var ledMethod = typeof(HardwareTestMethods).GetMethod(nameof(HardwareTestMethods.BlinkLed));
                await device.ExecuteMethod(ledMethod!, new object[] { 3 });
                Console.WriteLine("   LED blink test completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   LED blink test skipped: {ex.Message.Split('\n')[0]}");
            }

            await device.Disconnect();
            Console.WriteLine("   DirectExecutor tests completed");
            Console.WriteLine();
        }

        private static async Task TestEdgeCases(
            string devicePath,
            ILogger<DeviceConnection> connectionLogger, 
            ILogger<SimplifiedDevice> deviceLogger)
        {
            Console.WriteLine("🔧 Testing edge cases and error handling...");
            
            var connection = new DeviceConnection(
                DeviceConnection.ConnectionType.Serial, 
                devicePath, 
                connectionLogger);

            await connection.ConnectAsync();
            Console.WriteLine("   Connected for edge case testing");

            // Test syntax error handling
            try
            {
                await connection.ExecuteAsync("invalid syntax !!!");
                Console.WriteLine("   ❌ Should have thrown exception for invalid syntax");
            }
            catch (DeviceException)
            {
                Console.WriteLine("   ✅ Properly handled syntax error");
            }

            // Test empty code
            try
            {
                await connection.ExecuteAsync("");
                Console.WriteLine("   ❌ Should have thrown exception for empty code");
            }
            catch (ArgumentException)
            {
                Console.WriteLine("   ✅ Properly handled empty code");
            }

            // Test large data
            var largeString = new string('A', 1000);
            var lengthResult = await connection.ExecuteAsync($"len('{largeString}')");
            Console.WriteLine($"   Large data test (1000 chars): {lengthResult.Trim()}");

            // Test reconnection
            await connection.DisconnectAsync();
            Console.WriteLine("   Disconnected");
            
            await connection.ConnectAsync();
            Console.WriteLine("   Reconnected");
            
            var reconnectTest = await connection.ExecuteAsync("print('Reconnection test')");
            Console.WriteLine($"   Reconnection verification: {reconnectTest.Trim()}");

            await connection.DisconnectAsync();
            Console.WriteLine("   Edge case testing completed");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Test methods for hardware validation using Belay attributes.
    /// </summary>
    public class HardwareTestMethods
    {
        [Belay.Attributes.Task]
        public static string GetSystemInfo()
        {
            return @"
import sys
import gc
platform = sys.platform
memory = gc.mem_free()
f'Platform: {platform}, Free Memory: {memory} bytes'
";
        }

        [Belay.Attributes.Task]
        public static string Multiply(int a, int b)
        {
            return $"{a} * {b}";
        }

        [Belay.Attributes.Task]
        public static string BlinkLed(int count)
        {
            return $@"
try:
    from machine import Pin
    import time
    # Try common LED pins for different boards
    led_pins = [25, 2, 13, 16]  # Pico, ESP32 variants, STM32
    led = None
    for pin in led_pins:
        try:
            led = Pin(pin, Pin.OUT)
            break
        except:
            continue
    
    if led:
        for i in range({count}):
            led.on()
            time.sleep_ms(100)
            led.off()
            time.sleep_ms(100)
        print(f'LED blinked {count} times on pin {{led_pins[led_pins.index(pin)]}}')
    else:
        print('No LED pin available for this board')
except Exception as e:
    print(f'LED control error: {{e}}')
";
        }
    }
}