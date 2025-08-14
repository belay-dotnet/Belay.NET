// Hardware Validation Test for Simplified Raw REPL Protocol
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

public class HardwareValidationTest
{
    private static readonly string[] TestDevices = {
        "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94",   // ESP32C6
        "/dev/usb/tty-STM32_STLink-066FFF303430484257255318",           // STM32WB55
        "/dev/usb/tty-Board_in_FS_mode-e6614c311b7e6f35"              // RPI Pico (if available)
    };
    
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🔧 HARDWARE VALIDATION TEST - Simplified Raw REPL Protocol");
        Console.WriteLine("==========================================================");
        
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        var logger = loggerFactory.CreateLogger<DeviceConnection>();
        
        int passedTests = 0;
        int totalTests = 0;
        
        for (int i = 0; i < TestDevices.Length; i++)
        {
            var devicePath = TestDevices[i];
            var deviceName = i switch 
            {
                0 => "ESP32C6",
                1 => "STM32WB55", 
                2 => "RPI Pico",
                _ => $"Device{i}"
            };
            
            Console.WriteLine($"\n🔍 Testing {deviceName}: {devicePath}");
            Console.WriteLine(new string('=', 80));
            
            // Check if device exists first
            if (!System.IO.File.Exists(devicePath))
            {
                Console.WriteLine($"⚠️  Device file does not exist: {devicePath}");
                continue;
            }
            
            try
            {
                var result = await TestDevice(devicePath, deviceName, logger);
                if (result)
                {
                    Console.WriteLine($"✅ {deviceName} VALIDATION PASSED");
                    passedTests++;
                }
                else
                {
                    Console.WriteLine($"❌ {deviceName} VALIDATION FAILED");
                }
                totalTests++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {deviceName} VALIDATION ERROR: {ex.Message}");
                totalTests++;
            }
        }
        
        Console.WriteLine($"\n📊 VALIDATION SUMMARY");
        Console.WriteLine($"===================");
        Console.WriteLine($"Total Tests: {totalTests}");
        Console.WriteLine($"Passed: {passedTests}");
        Console.WriteLine($"Failed: {totalTests - passedTests}");
        Console.WriteLine($"Success Rate: {(totalTests > 0 ? (passedTests * 100.0 / totalTests):0):F1}%");
        
        return totalTests > 0 && passedTests == totalTests ? 0 : 1;
    }
    
    private static async Task<bool> TestDevice(string devicePath, string deviceName, ILogger<DeviceConnection> logger)
    {
        using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
        
        try
        {
            // Test 1: Basic Connection
            Console.WriteLine("🔌 Test 1: Basic Connection");
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await connection.ConnectAsync(connectCts.Token);
            Console.WriteLine("   ✅ Connected successfully");
            
            // Test 2: Simple Math Expression
            Console.WriteLine("🧮 Test 2: Simple Math Expression");
            using var mathCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var mathResult = await connection.ExecuteAsync("2 + 2", mathCts.Token);
            Console.WriteLine($"   Input: 2 + 2");
            Console.WriteLine($"   Output: '{mathResult.Trim()}'");
            
            if (!mathResult.Contains("4"))
            {
                Console.WriteLine("   ❌ Math result incorrect");
                return false;
            }
            Console.WriteLine("   ✅ Math expression correct");
            
            // Test 3: Print Statement
            Console.WriteLine("📝 Test 3: Print Statement");
            using var printCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var printResult = await connection.ExecuteAsync($"print('Hello from {deviceName}'); 'success'", printCts.Token);
            Console.WriteLine($"   Input: print('Hello from {deviceName}'); 'success'");
            Console.WriteLine($"   Output: '{printResult.Trim()}'");
            
            if (!printResult.Contains("Hello from") || !printResult.Contains("success"))
            {
                Console.WriteLine("   ❌ Print statement failed");
                return false;
            }
            Console.WriteLine("   ✅ Print statement working");
            
            // Test 4: Variable Assignment
            Console.WriteLine("🔢 Test 4: Variable Assignment");
            using var varCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var varResult = await connection.ExecuteAsync("x = 42; y = x * 2; y", varCts.Token);
            Console.WriteLine($"   Input: x = 42; y = x * 2; y");
            Console.WriteLine($"   Output: '{varResult.Trim()}'");
            
            if (!varResult.Contains("84"))
            {
                Console.WriteLine("   ❌ Variable assignment failed");
                return false;
            }
            Console.WriteLine("   ✅ Variable assignment working");
            
            // Test 5: MicroPython System Info
            Console.WriteLine("ℹ️ Test 5: MicroPython System Info");
            using var infoCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var infoResult = await connection.ExecuteAsync("import sys; sys.implementation.name", infoCts.Token);
            Console.WriteLine($"   Input: import sys; sys.implementation.name");
            Console.WriteLine($"   Output: '{infoResult.Trim()}'");
            Console.WriteLine("   ✅ System info retrieved");
            
            await connection.DisconnectAsync();
            Console.WriteLine("🔌 Disconnected successfully");
            
            return true;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("   ⏰ Test timed out");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   💥 Test exception: {ex.Message}");
            return false;
        }
    }
}