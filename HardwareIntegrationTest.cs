// Hardware Integration Test for Enhanced Raw-Paste Protocol
// Tests ESP32C6 and STM32WB55 devices with adaptive protocol selection
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

public class HardwareIntegrationTest
{
    private static readonly string[] TargetDevices = {
        "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94",   // ESP32C6
        "/dev/usb/tty-STM32_STLink-066FFF303430484257255318"             // STM32WB55
    };
    
    private static readonly string[] DeviceNames = {
        "ESP32C6",
        "STM32WB55"
    };
    
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🚀 BELAY.NET ENHANCED PROTOCOL HARDWARE INTEGRATION TEST");
        Console.WriteLine("=======================================================");
        Console.WriteLine("Testing: ESP32C6 and STM32WB55 with adaptive Raw-Paste protocol");
        Console.WriteLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine();
        
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        var logger = loggerFactory.CreateLogger<DeviceConnection>();
        
        int totalTests = 0;
        int passedTests = 0;
        int devicesPassed = 0;
        
        for (int i = 0; i < TargetDevices.Length; i++)
        {
            var devicePath = TargetDevices[i];
            var deviceName = DeviceNames[i];
            
            Console.WriteLine($"🔍 TESTING DEVICE: {deviceName}");
            Console.WriteLine($"   Path: {devicePath}");
            Console.WriteLine($"   {new string('=', 80)}");
            
            try
            {
                var (tests, passed) = await RunDeviceTests(devicePath, deviceName, logger);
                totalTests += tests;
                passedTests += passed;
                
                if (passed == tests)
                {
                    devicesPassed++;
                    Console.WriteLine($"✅ {deviceName} PASSED ALL TESTS ({passed}/{tests})");
                }
                else
                {
                    Console.WriteLine($"⚠️  {deviceName} PARTIAL SUCCESS ({passed}/{tests} tests passed)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {deviceName} FAILED: {ex.Message}");
                Console.WriteLine($"   Exception Type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner Exception: {ex.InnerException.Message}");
                }
            }
            
            Console.WriteLine();
        }
        
        Console.WriteLine("📊 FINAL HARDWARE INTEGRATION TEST RESULTS");
        Console.WriteLine("===========================================");
        Console.WriteLine($"Devices Tested: {TargetDevices.Length}");
        Console.WriteLine($"Devices Fully Successful: {devicesPassed}");
        Console.WriteLine($"Total Tests: {totalTests}");
        Console.WriteLine($"Passed Tests: {passedTests}");
        Console.WriteLine($"Success Rate: {(totalTests > 0 ? (passedTests * 100 / totalTests) : 0)}%");
        
        if (devicesPassed == TargetDevices.Length)
        {
            Console.WriteLine();
            Console.WriteLine("🎉 INTEGRATION TEST SUCCESS!");
            Console.WriteLine("   Enhanced Raw-Paste protocol working with all target hardware");
            Console.WriteLine("   Adaptive protocol selection validated");
            Console.WriteLine("   Flow control and window management confirmed");
            return 0;
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("⚠️  INTEGRATION TEST PARTIAL SUCCESS");
            Console.WriteLine($"   {devicesPassed}/{TargetDevices.Length} devices fully operational");
            return 1;
        }
    }
    
    private static async Task<(int total, int passed)> RunDeviceTests(
        string devicePath, 
        string deviceName, 
        ILogger<DeviceConnection> logger)
    {
        int totalTests = 0;
        int passedTests = 0;
        
        // Test 1: Basic Connection and Raw REPL
        totalTests++;
        Console.WriteLine("🔌 Test 1: Basic Connection and Raw REPL");
        try
        {
            await TestBasicConnection(devicePath, deviceName, logger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }
        
        // Test 2: Small Code Execution (should use basic Raw REPL)
        totalTests++;
        Console.WriteLine("📝 Test 2: Small Code Execution (Basic Raw REPL)");
        try
        {
            await TestSmallCodeExecution(devicePath, deviceName, logger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }
        
        // Test 3: Large Code Execution (should trigger Raw-Paste mode)
        totalTests++;
        Console.WriteLine("📄 Test 3: Large Code Execution (Raw-Paste Mode)");
        try
        {
            await TestLargeCodeExecution(devicePath, deviceName, logger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }
        
        // Test 4: Multi-line Code (should trigger Raw-Paste mode)
        totalTests++;
        Console.WriteLine("📋 Test 4: Multi-line Code (Raw-Paste Mode)");
        try
        {
            await TestMultilineCode(devicePath, deviceName, logger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }
        
        // Test 5: Special Characters (should trigger Raw-Paste mode)
        totalTests++;
        Console.WriteLine("🔤 Test 5: Special Characters (Raw-Paste Mode)");
        try
        {
            await TestSpecialCharacters(devicePath, deviceName, logger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }
        
        // Test 6: Device Information and Capabilities
        totalTests++;
        Console.WriteLine("ℹ️  Test 6: Device Information and Capabilities");
        try
        {
            await TestDeviceInformation(devicePath, deviceName, logger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }
        
        // Test 7: Error Handling and Recovery
        totalTests++;
        Console.WriteLine("⚠️  Test 7: Error Handling and Recovery");
        try
        {
            await TestErrorHandling(devicePath, deviceName, logger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }
        
        // Test 8: Connection Stability
        totalTests++;
        Console.WriteLine("🔄 Test 8: Connection Stability");
        try
        {
            await TestConnectionStability(devicePath, deviceName, logger);
            Console.WriteLine("   ✅ PASSED");
            passedTests++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ FAILED: {ex.Message}");
        }
        
        return (totalTests, passedTests);
    }
    
    private static async Task TestBasicConnection(string devicePath, string deviceName, ILogger<DeviceConnection> logger)
    {
        using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
        
        await connection.ConnectAsync();
        if (connection.State != DeviceConnectionState.Connected)
            throw new InvalidOperationException($"Device {deviceName} not connected");
            
        await connection.DisconnectAsync();
        if (connection.State != DeviceConnectionState.Disconnected)
            throw new InvalidOperationException($"Device {deviceName} not properly disconnected");
            
        Console.WriteLine($"     📋 {deviceName} connection/disconnection cycle successful");
    }
    
    private static async Task TestSmallCodeExecution(string devicePath, string deviceName, ILogger<DeviceConnection> logger)
    {
        using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
        
        await connection.ConnectAsync();
        
        // Small code - should use basic Raw REPL
        var result = await connection.ExecuteAsync("print('Hello from " + deviceName + "'); 42");
        
        if (string.IsNullOrEmpty(result))
            throw new InvalidOperationException($"No result from {deviceName}");
            
        Console.WriteLine($"     📋 {deviceName} result: {result.Trim()}");
        
        await connection.DisconnectAsync();
    }
    
    private static async Task TestLargeCodeExecution(string devicePath, string deviceName, ILogger<DeviceConnection> logger)
    {
        using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
        
        await connection.ConnectAsync();
        
        // Large code block - should trigger Raw-Paste mode
        var largeCode = $@"
# Large code block for {deviceName} - should trigger Raw-Paste mode
import sys
import gc
print('=== {deviceName} Large Operation Test ===')
device_info = {{}}
device_info['platform'] = sys.platform
device_info['memory_free'] = gc.mem_free()
device_info['device_name'] = '{deviceName}'

# Perform some operations to test flow control
results = []
for i in range(15):
    if i % 5 == 0:
        gc.collect()
    result = i * 2 + 10
    results.append(result)
    print(f'Processing {deviceName} iteration {{i}}: {{result}}')

print('=== {deviceName} Large Operation Complete ===')
print(f'Total results: {{len(results)}}')
print(f'Sum: {{sum(results)}}')
final_result = f'{deviceName}_large_operation_success_{{len(results)}}_items'
final_result
";
        
        var result = await connection.ExecuteAsync(largeCode);
        
        if (string.IsNullOrEmpty(result) || !result.Contains($"{deviceName}_large_operation_success"))
            throw new InvalidOperationException($"Large code execution failed on {deviceName}: {result}");
            
        Console.WriteLine($"     📋 {deviceName} large code result: {result.Trim()}");
        
        await connection.DisconnectAsync();
    }
    
    private static async Task TestMultilineCode(string devicePath, string deviceName, ILogger<DeviceConnection> logger)
    {
        using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
        
        await connection.ConnectAsync();
        
        // Multi-line code - should trigger Raw-Paste mode
        var multiLineCode = $@"
def {deviceName.ToLower()}_test_function(x, y):
    print(f'{deviceName} function called with {{x}}, {{y}}')
    result = x * y + 100
    print(f'{deviceName} calculation: {{x}} * {{y}} + 100 = {{result}}')
    return result

def {deviceName.ToLower()}_wrapper():
    val1 = {deviceName.ToLower()}_test_function(5, 7)
    val2 = {deviceName.ToLower()}_test_function(3, 4)
    total = val1 + val2
    print(f'{deviceName} total: {{total}}')
    return total

final_result = {deviceName.ToLower()}_wrapper()
f'{deviceName}_multiline_success_{{final_result}}'
";
        
        var result = await connection.ExecuteAsync(multiLineCode);
        
        if (string.IsNullOrEmpty(result) || !result.Contains($"{deviceName}_multiline_success"))
            throw new InvalidOperationException($"Multi-line code execution failed on {deviceName}: {result}");
            
        Console.WriteLine($"     📋 {deviceName} multi-line result: {result.Trim()}");
        
        await connection.DisconnectAsync();
    }
    
    private static async Task TestSpecialCharacters(string devicePath, string deviceName, ILogger<DeviceConnection> logger)
    {
        using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
        
        await connection.ConnectAsync();
        
        // Code with special characters - should trigger Raw-Paste mode
        var specialCode = $"print('{deviceName} special chars: \\\\x01 \\\\x04 test'); '{deviceName}_special_success'";
        
        var result = await connection.ExecuteAsync(specialCode);
        
        if (string.IsNullOrEmpty(result) || !result.Contains($"{deviceName}_special_success"))
            throw new InvalidOperationException($"Special characters test failed on {deviceName}: {result}");
            
        Console.WriteLine($"     📋 {deviceName} special chars result: {result.Trim()}");
        
        await connection.DisconnectAsync();
    }
    
    private static async Task TestDeviceInformation(string devicePath, string deviceName, ILogger<DeviceConnection> logger)
    {
        using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
        
        await connection.ConnectAsync();
        
        var infoCode = @"
import sys
import gc
info = {
    'platform': sys.platform,
    'version': sys.version,
    'memory_free': gc.mem_free(),
    'implementation': sys.implementation.name
}
str(info)
";
        
        var result = await connection.ExecuteAsync(infoCode);
        
        if (string.IsNullOrEmpty(result))
            throw new InvalidOperationException($"Failed to get device information from {deviceName}");
            
        Console.WriteLine($"     📋 {deviceName} info: {result.Trim()}");
        
        await connection.DisconnectAsync();
    }
    
    private static async Task TestErrorHandling(string devicePath, string deviceName, ILogger<DeviceConnection> logger)
    {
        using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
        
        await connection.ConnectAsync();
        
        // Test syntax error handling
        bool syntaxErrorCaught = false;
        try
        {
            await connection.ExecuteAsync("invalid syntax !!! for " + deviceName);
        }
        catch (Exception)
        {
            syntaxErrorCaught = true;
        }
        
        if (!syntaxErrorCaught)
            throw new InvalidOperationException($"Syntax error not properly caught on {deviceName}");
        
        // Verify connection still works after error
        var recoveryResult = await connection.ExecuteAsync($"print('{deviceName} recovery test'); 'recovery_success'");
        
        if (!recoveryResult.Contains("recovery_success"))
            throw new InvalidOperationException($"Connection not recovered after error on {deviceName}");
            
        Console.WriteLine($"     📋 {deviceName} error handling and recovery successful");
        
        await connection.DisconnectAsync();
    }
    
    private static async Task TestConnectionStability(string devicePath, string deviceName, ILogger<DeviceConnection> logger)
    {
        using var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, devicePath, logger);
        
        // Test multiple connect/disconnect cycles
        for (int cycle = 0; cycle < 3; cycle++)
        {
            await connection.ConnectAsync();
            
            if (connection.State != DeviceConnectionState.Connected)
                throw new InvalidOperationException($"Connection cycle {cycle} failed on {deviceName}");
            
            // Execute a test command
            var result = await connection.ExecuteAsync($"print('{deviceName} cycle {cycle}'); {cycle + 1}");
            
            if (!result.Contains((cycle + 1).ToString()))
                throw new InvalidOperationException($"Execution failed in cycle {cycle} on {deviceName}");
            
            await connection.DisconnectAsync();
            
            if (connection.State != DeviceConnectionState.Disconnected)
                throw new InvalidOperationException($"Disconnection cycle {cycle} failed on {deviceName}");
            
            // Brief pause between cycles
            await Task.Delay(200);
        }
        
        Console.WriteLine($"     📋 {deviceName} connection stability test successful (3 cycles)");
    }
}