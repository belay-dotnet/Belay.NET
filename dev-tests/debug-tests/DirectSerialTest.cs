// Direct Serial Communication Test
using System;
using System.Threading;
using System.Threading.Tasks;
using Belay.Core;

public class DirectSerialTest
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🔧 DIRECT SERIAL COMMUNICATION TEST");
        Console.WriteLine("===================================");
        
        var devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        try
        {
            using var serial = new LinuxSerialConnection(devicePath);
            
            Console.WriteLine($"Opening connection to: {devicePath}");
            await serial.OpenAsync();
            Console.WriteLine("✅ Connection opened");
            
            // Test 1: Send Ctrl-C to interrupt any running code
            Console.WriteLine("Sending Ctrl-C to interrupt...");
            await serial.WriteAsync("\x03");
            await Task.Delay(500);
            
            // Test 2: Send newline to get prompt
            Console.WriteLine("Sending newline...");
            await serial.WriteAsync("\r\n");
            await Task.Delay(500);
            
            // Test 3: Read any response
            Console.WriteLine("Reading response...");
            var response = await serial.ReadExistingAsync();
            Console.WriteLine($"Response: '{response}'");
            Console.WriteLine($"Response (hex): {BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(response)).Replace("-", " ")}");
            
            // Test 4: Send a simple print command
            Console.WriteLine("Sending: print('hello')");
            await serial.WriteAsync("print('hello')\r\n");
            await Task.Delay(1000);
            
            var response2 = await serial.ReadExistingAsync();
            Console.WriteLine($"Response: '{response2}'");
            Console.WriteLine($"Response (hex): {BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(response2)).Replace("-", " ")}");
            
            serial.Close();
            Console.WriteLine("✅ Connection closed");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"Exception type: {ex.GetType().Name}");
            return 1;
        }
    }
}