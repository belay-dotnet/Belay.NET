// Simple Hardware Integration Test
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

public class SimpleHardwareTest
{
    private static readonly string[] TestDevices = {
        "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94",   // ESP32C6
        "/dev/usb/tty-STM32_STLink-066FFF303430484257255318"             // STM32WB55
    };
    
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🚀 ENHANCED PROTOCOL HARDWARE TEST");
        Console.WriteLine("===================================");
        
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        var logger = loggerFactory.CreateLogger<DeviceConnection>();
        
        for (int i = 0; i < TestDevices.Length; i++)
        {
            var devicePath = TestDevices[i];
            var deviceName = i == 0 ? "ESP32C6" : "STM32WB55";
            
            Console.WriteLine($"\n🔍 Testing {deviceName}: {devicePath}");
            Console.WriteLine(new string('=', 60));
            
            try
            {
                await TestDevice(devicePath, deviceName, logger);
                Console.WriteLine($"✅ {deviceName} PASSED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {deviceName} FAILED: {ex.Message}");
            }
        }
        
        return 0;
    }
    
    private static async Task TestDevice(string devicePath, string deviceName, ILogger<DeviceConnection> logger)
    {
        using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
        
        // Test 1: Basic connection
        Console.WriteLine("🔌 Basic Connection Test");
        await connection.ConnectAsync();
        Console.WriteLine("   ✅ Connected");
        
        // Test 2: Small code (basic Raw REPL)
        Console.WriteLine("📝 Small Code Test (Basic Raw REPL)");
        var result1 = await connection.ExecuteAsync("print('Hello from " + deviceName + "'); 42");
        Console.WriteLine($"   Result: {result1.Trim()}");
        
        // Test 3: Large code (Raw-Paste mode)
        Console.WriteLine("📄 Large Code Test (Raw-Paste Mode)");
        var largeCode = @"
# Large code block to trigger Raw-Paste mode
import sys
print('=== Large Operation Test ===')
results = []
for i in range(10):
    result = i * 2 + 10
    results.append(result)
    print(f'Iteration {i}: {result}')
print(f'Total: {len(results)} items')
'large_operation_success'
";
        var result2 = await connection.ExecuteAsync(largeCode);
        Console.WriteLine($"   Result: {result2.Trim()}");
        
        // Test 4: Multi-line code (Raw-Paste mode)
        Console.WriteLine("📋 Multi-line Code Test (Raw-Paste Mode)");
        var multiLineCode = @"
def test_function(x, y):
    return x * y + 100

def wrapper():
    val = test_function(5, 7)
    return val

result = wrapper()
f'multiline_success_{result}'
";
        var result3 = await connection.ExecuteAsync(multiLineCode);
        Console.WriteLine($"   Result: {result3.Trim()}");
        
        // Test 5: Error handling
        Console.WriteLine("⚠️  Error Handling Test");
        bool errorCaught = false;
        try
        {
            await connection.ExecuteAsync("invalid syntax !!!");
        }
        catch (Exception)
        {
            errorCaught = true;
        }
        
        if (!errorCaught)
            throw new InvalidOperationException("Error not caught");
        
        // Test recovery
        var recovery = await connection.ExecuteAsync("print('Recovery test'); 'recovery_success'");
        if (!recovery.Contains("recovery_success"))
            throw new InvalidOperationException("Recovery failed");
        
        Console.WriteLine("   ✅ Error handling and recovery successful");
        
        await connection.DisconnectAsync();
        Console.WriteLine("   ✅ Disconnected");
    }
}