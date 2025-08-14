// Minimal device test without sophisticated protocol
using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class MinimalDeviceTest
{
    static async Task Main()
    {
        Console.WriteLine("🔧 Minimal Device Test (Direct SerialPort)");
        Console.WriteLine("==========================================");
        
        var devices = new[]
        {
            ("ESP32C6", "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94"),
            ("STM32WB55", "/dev/usb/tty-Board_in_FS_mode-a8100d7bd7092d6e")
        };

        foreach (var (name, path) in devices)
        {
            Console.WriteLine($"\n🎯 Testing {name}: {path}");
            Console.WriteLine("=" + new string('=', 30 + name.Length));
            
            try
            {
                using var port = new SerialPort(path, 115200, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 5000,
                    WriteTimeout = 5000,
                    NewLine = "\r\n"
                };
                
                Console.WriteLine("  Step 1: Opening serial port...");
                port.Open();
                Console.WriteLine("  ✅ Serial port opened");

                Console.WriteLine("  Step 2: Sending simple interrupt and newline...");
                // Simple approach - just send interrupt and newline
                port.Write("\x03\r\n");
                await Task.Delay(500);
                
                // Drain any existing output
                port.DiscardInBuffer();
                
                Console.WriteLine("  Step 3: Testing simple Raw REPL...");
                // Enter raw mode
                port.Write("\x01");
                await Task.Delay(200);
                
                // Read the raw mode response
                var rawResponse = "";
                var startTime = DateTime.Now;
                while ((DateTime.Now - startTime).TotalSeconds < 3)
                {
                    if (port.BytesToRead > 0)
                    {
                        rawResponse += port.ReadExisting();
                        if (rawResponse.Contains(">"))
                            break;
                    }
                    await Task.Delay(50);
                }
                
                Console.WriteLine($"  Raw mode response: '{EscapeString(rawResponse)}'");
                
                if (rawResponse.Contains(">"))
                {
                    Console.WriteLine("  ✅ Raw REPL mode entered successfully");
                    
                    // Send simple code
                    port.Write("print('Hello from " + name + "')");
                    port.Write("\x04"); // Execute
                    
                    // Read execution result
                    var result = "";
                    startTime = DateTime.Now;
                    while ((DateTime.Now - startTime).TotalSeconds < 5)
                    {
                        if (port.BytesToRead > 0)
                        {
                            result += port.ReadExisting();
                            if (result.Contains("\x04>"))
                                break;
                        }
                        await Task.Delay(50);
                    }
                    
                    Console.WriteLine($"  Execution result: '{EscapeString(result)}'");
                    
                    // Exit raw mode
                    port.Write("\x02");
                    await Task.Delay(200);
                    
                    Console.WriteLine($"  ✅ {name} test PASSED!");
                }
                else
                {
                    Console.WriteLine($"  ⚠️ {name} did not enter raw mode properly");
                }
                
                port.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ {name} test FAILED: {ex.Message}");
                Console.WriteLine($"     Type: {ex.GetType().Name}");
            }
        }

        Console.WriteLine("\n📊 Minimal device test complete");
    }
    
    static string EscapeString(string input)
    {
        return input
            .Replace("\x01", "\\x01")
            .Replace("\x02", "\\x02")  
            .Replace("\x03", "\\x03")
            .Replace("\x04", "\\x04")
            .Replace("\x05", "\\x05")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}