using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Belay.Core;

public static class TestRpiPico
{
    public static async Task Main()
    {
        var testDevices = new[] {
            "/dev/usb/tty-Board_in_FS_mode-e6614c311b7e6f35",     // RPI Pico
            "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94", // ESP32C6
            "/dev/usb/tty-STM32_STLink-066FFF303430484257255318"          // STM32WB55
        };
        
        Console.WriteLine("Testing Raw REPL with MicroPython devices");
        
        foreach (var devicePath in testDevices)
        {
            Console.WriteLine($"\nTesting: {devicePath}");
            
            // Check if device exists
            if (!System.IO.File.Exists(devicePath))
            {
                Console.WriteLine("  Device not found, skipping");
                continue;
            }
            
            try
            {
                var connection = new DeviceConnection(
                    DeviceConnection.ConnectionType.Serial, 
                    devicePath, 
                    NullLogger<DeviceConnection>.Instance);
                    
                await connection.ConnectAsync();
                Console.WriteLine("  Connected successfully");
                
                var result = await connection.ExecuteAsync("2 + 2");
                Console.WriteLine($"  Math result: '{result.Trim()}'");
                
                if (result.Contains("4"))
                {
                    Console.WriteLine("  ✅ Raw REPL protocol working!");
                    
                    // Try a more complex test
                    var complexResult = await connection.ExecuteAsync("print('Hello MicroPython'); x = 10 * 5; x");
                    Console.WriteLine($"  Complex result: '{complexResult.Trim()}'");
                    
                    await connection.DisconnectAsync();
                    Console.WriteLine("  Hardware validation PASSED for this device");
                    return; // Success with at least one device
                }
                else
                {
                    Console.WriteLine("  ❌ Raw REPL protocol not working correctly");
                    await connection.DisconnectAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }
        
        Console.WriteLine("\n❌ No working MicroPython devices found");
    }
}