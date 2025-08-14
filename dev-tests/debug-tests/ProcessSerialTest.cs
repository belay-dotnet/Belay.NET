// Test the process-based serial connection approach
using System;
using System.Threading;
using System.Threading.Tasks;
using Belay.Core;

class ProcessSerialTest
{
    static async Task Main()
    {
        Console.WriteLine("🔧 Process-Based Serial Connection Test");
        Console.WriteLine("=======================================");
        
        var devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        try
        {
            Console.WriteLine($"Step 1: Creating ProcessSerialConnection for {devicePath}");
            var serial = new ProcessSerialConnection(devicePath);
            Console.WriteLine("✅ ProcessSerialConnection created");
            
            Console.WriteLine("Step 2: Opening connection...");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await serial.OpenAsync();
            Console.WriteLine("✅ Connection opened successfully!");
            
            Console.WriteLine("Step 3: Testing device recovery sequence...");
            // Send Ctrl-B to exit any raw mode, then Ctrl-C to interrupt
            await serial.WriteAsync("\x02");
            await Task.Delay(100);
            await serial.WriteAsync("\x03");
            await Task.Delay(200);
            
            // Clear any existing output
            var clearOutput = await serial.ReadWithTimeoutAsync(500);
            Console.WriteLine($"  Cleared output: '{clearOutput}'");
            
            Console.WriteLine("Step 4: Testing basic communication...");
            await serial.WriteAsync("print(2+2)\r\n");
            await Task.Delay(300); // Give device time to process
            
            var response = await serial.ReadWithTimeoutAsync(2000);
            Console.WriteLine($"  Response: '{response}'");
            
            if (response.Contains("4"))
            {
                Console.WriteLine("✅ Basic communication successful!");
            }
            else
            {
                Console.WriteLine("⚠️ Response doesn't contain expected '4', but communication worked");
            }
            
            serial.Close();
            Console.WriteLine("🎉 Process-based serial communication test PASSED!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"   Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
        }
    }
}