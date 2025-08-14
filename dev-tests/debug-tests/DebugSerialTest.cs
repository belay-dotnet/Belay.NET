// Debug test to identify exactly where LinuxSerialConnection hangs
using System;
using System.Threading;
using System.Threading.Tasks;
using Belay.Core;

class DebugSerialTest  
{
    static async Task Main()
    {
        Console.WriteLine("🔍 Debug Serial Connection Test");
        Console.WriteLine("=================================");
        
        var devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        try
        {
            Console.WriteLine($"Step 1: Creating LinuxSerialConnection for {devicePath}");
            var serial = new LinuxSerialConnection(devicePath);
            Console.WriteLine("✅ LinuxSerialConnection created");
            
            Console.WriteLine("Step 2: Calling OpenAsync()...");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            // Run with cancellation to see if it respects timeout
            var openTask = serial.OpenAsync();
            var timeoutTask = Task.Delay(8000, cts.Token);
            
            var completedTask = await Task.WhenAny(openTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                Console.WriteLine("❌ OpenAsync() timed out after 8 seconds");
                Console.WriteLine("💡 This suggests the hang is in ConfigureSerialPortAsync() or FileStream.Open()");
            }
            else
            {
                await openTask; // Check for exceptions
                Console.WriteLine("✅ OpenAsync() completed successfully");
                
                Console.WriteLine("Step 3: Testing basic write...");
                await serial.WriteAsync("print('hello')\r\n");
                Console.WriteLine("✅ WriteAsync completed");
                
                Console.WriteLine("Step 4: Testing basic read...");
                await Task.Delay(100); // Give device time to respond
                var response = await serial.ReadExistingAsync();
                Console.WriteLine($"  Response: '{response}'");
                
                serial.Close();
                Console.WriteLine("✅ All tests completed successfully");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception: {ex.Message}");
            Console.WriteLine($"   Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
            }
        }
    }
}