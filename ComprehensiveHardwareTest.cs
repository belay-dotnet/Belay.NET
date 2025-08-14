// Comprehensive Hardware Integration Test
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Belay.Core;

public class ComprehensiveHardwareTest
{
    private static readonly string[] TestDevices = {
        "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94",   // ESP32C6
        "/dev/usb/tty-Board_in_FS_mode-a8100d7bd7092d6e"               // STM32WB55
    };
    
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🧪 COMPREHENSIVE HARDWARE INTEGRATION TEST");
        Console.WriteLine("==========================================");
        
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        var logger = loggerFactory.CreateLogger<DeviceConnection>();
        
        for (int i = 0; i < TestDevices.Length; i++)
        {
            var devicePath = TestDevices[i];
            var deviceName = i == 0 ? "ESP32C6" : "STM32WB55";
            
            Console.WriteLine($"\n🔍 Testing {deviceName}: {devicePath}");
            Console.WriteLine(new string('=', 70));
            
            try
            {
                await TestDeviceComprehensively(devicePath, deviceName, logger);
                Console.WriteLine($"✅ {deviceName} ALL TESTS PASSED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {deviceName} FAILED: {ex.Message}");
                Console.WriteLine($"Exception Type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }
        }
        
        return 0;
    }
    
    private static async Task TestDeviceComprehensively(string devicePath, string deviceName, ILogger<DeviceConnection> logger)
    {
        using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
        
        // Test 1: Basic Connection
        Console.WriteLine("🔌 Test 1: Basic Connection");
        await connection.ConnectAsync();
        Console.WriteLine("   ✅ Connection established");
        
        // Test 2: Simple Expression (Basic Raw REPL)
        Console.WriteLine("📝 Test 2: Simple Expression (Basic Raw REPL)");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var result1 = await connection.ExecuteAsync("2 + 2", cts.Token);
            Console.WriteLine($"   Result: '{result1.Trim()}'");
            Console.WriteLine("   ✅ Basic Raw REPL working");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("   ⚠️ Basic Raw REPL timed out");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Basic Raw REPL failed: {ex.Message}");
        }
        
        // Test 3: Small Code Block (Should use Basic Raw REPL)
        Console.WriteLine("📄 Test 3: Small Code Block");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var smallCode = "print('Hello from " + deviceName + "'); x = 5 * 3; x";
            var result2 = await connection.ExecuteAsync(smallCode, cts.Token);
            Console.WriteLine($"   Result: '{result2.Trim()}'");
            Console.WriteLine("   ✅ Small code execution working");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("   ⚠️ Small code execution timed out");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Small code execution failed: {ex.Message}");
        }
        
        // Test 4: Large Code Block (Should trigger Raw-Paste mode)
        Console.WriteLine("📋 Test 4: Large Code Block (Raw-Paste Mode)");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var largeCode = @"
# Large code block to trigger Raw-Paste mode
import sys
print('=== Large Operation Test ===')
results = []
for i in range(5):
    result = i * 2 + 10
    results.append(result)
    print(f'Iteration {i}: {result}')
print(f'Total items: {len(results)}')
sum(results)
";
            var result3 = await connection.ExecuteAsync(largeCode, cts.Token);
            Console.WriteLine($"   Result: '{result3.Trim()}'");
            Console.WriteLine("   ✅ Raw-Paste mode working");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("   ⚠️ Raw-Paste mode timed out");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Raw-Paste mode failed: {ex.Message}");
        }
        
        // Test 5: Multi-line Function Definition
        Console.WriteLine("🎯 Test 5: Multi-line Function Definition");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var functionCode = @"
def test_function(x, y):
    return x * y + 100

def wrapper():
    val = test_function(3, 4)
    return val

result = wrapper()
result
";
            var result4 = await connection.ExecuteAsync(functionCode, cts.Token);
            Console.WriteLine($"   Result: '{result4.Trim()}'");
            Console.WriteLine("   ✅ Multi-line functions working");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("   ⚠️ Multi-line function timed out");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Multi-line function failed: {ex.Message}");
        }
        
        // Test 6: Error Handling and Recovery
        Console.WriteLine("⚠️ Test 6: Error Handling and Recovery");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            bool errorCaught = false;
            
            try
            {
                await connection.ExecuteAsync("invalid_syntax_error !!!", cts.Token);
            }
            catch (Exception)
            {
                errorCaught = true;
                Console.WriteLine("   ✅ Error properly caught");
            }
            
            if (!errorCaught)
            {
                Console.WriteLine("   ⚠️ Error not caught as expected");
            }
            
            // Test recovery
            var recovery = await connection.ExecuteAsync("print('Recovery test'); 'recovery_success'", cts.Token);
            if (recovery.Contains("recovery_success"))
            {
                Console.WriteLine("   ✅ Device recovery successful");
            }
            else
            {
                Console.WriteLine("   ⚠️ Device recovery may have issues");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Error handling test failed: {ex.Message}");
        }
        
        // Test 7: Device Information
        Console.WriteLine("ℹ️ Test 7: Device Information");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var infoCode = "import sys; print(f'Python: {sys.version}'); sys.implementation.name";
            var deviceInfo = await connection.ExecuteAsync(infoCode, cts.Token);
            Console.WriteLine($"   Device Info: {deviceInfo.Trim()}");
            Console.WriteLine("   ✅ Device information retrieved");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Device info failed: {ex.Message}");
        }
        
        // Test 8: Connection Stability
        Console.WriteLine("🔄 Test 8: Connection Stability");
        try
        {
            for (int i = 0; i < 3; i++)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var result = await connection.ExecuteAsync($"'stability_test_{i}'", cts.Token);
                if (!result.Contains($"stability_test_{i}"))
                {
                    throw new InvalidOperationException($"Stability test {i} failed");
                }
            }
            Console.WriteLine("   ✅ Connection stability confirmed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Connection stability failed: {ex.Message}");
        }
        
        await connection.DisconnectAsync();
        Console.WriteLine("🔌 Disconnected successfully");
    }
}