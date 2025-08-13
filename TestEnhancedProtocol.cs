// Test Enhanced Raw-Paste Protocol Implementation
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

public class TestEnhancedProtocol
{
    private static readonly string[] TestDevices = {
        "/dev/usb/tty-Board_in_FS_mode-e6614c311b7e6f35",
        "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94",
        "/dev/usb/tty-STM32_STLink-066FFF303430484257255318"
    };
    
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🚀 Enhanced Raw-Paste Protocol Test");
        Console.WriteLine("Testing adaptive protocol selection and flow control");
        Console.WriteLine("=================================================");
        
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        var logger = loggerFactory.CreateLogger<DeviceConnection>();
        
        foreach (var devicePath in TestDevices)
        {
            Console.WriteLine($"\n🔍 Testing Device: {devicePath}");
            Console.WriteLine(new string('=', 60));
            
            try
            {
                await TestDevice(devicePath, logger);
                Console.WriteLine("✅ DEVICE TEST PASSED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ DEVICE TEST FAILED: {ex.Message}");
                Console.WriteLine($"   Exception Type: {ex.GetType().Name}");
            }
        }
        
        return 0;
    }
    
    private static async Task TestDevice(string devicePath, ILogger<DeviceConnection> logger)
    {
        var connection = new DeviceConnection(
            DeviceConnection.ConnectionType.Serial,
            devicePath,
            logger);
        
        await connection.ConnectAsync();
        Console.WriteLine("✅ Device connected successfully");
        
        // Test 1: Basic Raw REPL (should use basic mode)
        Console.WriteLine("🧪 Test 1: Basic Raw REPL");
        var result1 = await connection.ExecuteAsync("print('Hello'); 42");
        Console.WriteLine($"   Result: {result1}");
        
        // Test 2: Large code (should trigger Raw-Paste mode)
        Console.WriteLine("🧪 Test 2: Large code (should trigger Raw-Paste)");
        var largeCode = @"
# This is a large code block to trigger Raw-Paste mode
import sys
import gc
print('Starting large operation...')
for i in range(10):
    print(f'Iteration {i}')
    if i % 3 == 0:
        gc.collect()
print('Large operation completed')
print(f'Platform: {sys.platform}')
print(f'Memory: {gc.mem_free()}')
result = 'Large operation success'
result
";
        var result2 = await connection.ExecuteAsync(largeCode);
        Console.WriteLine($"   Result: {result2}");
        
        // Test 3: Multi-line code (should trigger Raw-Paste mode)
        Console.WriteLine("🧪 Test 3: Multi-line code (should trigger Raw-Paste)");
        var multiLineCode = @"
def test_function():
    x = 10
    y = 20
    z = x + y
    return z

result = test_function()
print(f'Function result: {result}')
result
";
        var result3 = await connection.ExecuteAsync(multiLineCode);
        Console.WriteLine($"   Result: {result3}");
        
        // Test 4: Code with special characters (should trigger Raw-Paste mode)
        Console.WriteLine("🧪 Test 4: Code with special characters");
        var specialCode = "print('Testing special chars: \\x01 \\x04'); 'special_test'";
        var result4 = await connection.ExecuteAsync(specialCode);
        Console.WriteLine($"   Result: {result4}");
        
        await connection.DisconnectAsync();
        Console.WriteLine("✅ Device disconnected successfully");
    }
}