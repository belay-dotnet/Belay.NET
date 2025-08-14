// Test if System.IO.Ports.SerialPort works on Linux
using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

class SerialPortTest
{
    static async Task Main()
    {
        Console.WriteLine("🔧 System.IO.Ports.SerialPort Linux Test");
        Console.WriteLine("=========================================");
        
        var devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        try
        {
            Console.WriteLine($"Step 1: Creating SerialPort for {devicePath}");
            var serialPort = new SerialPort(devicePath, 115200, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 2000,
                WriteTimeout = 2000,
                NewLine = "\r\n"
            };
            Console.WriteLine("✅ SerialPort created");
            
            Console.WriteLine("Step 2: Opening SerialPort...");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            // Try to open with timeout
            var openTask = Task.Run(() => serialPort.Open());
            var timeoutTask = Task.Delay(5000, cts.Token);
            
            var completedTask = await Task.WhenAny(openTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                Console.WriteLine("❌ SerialPort.Open() timed out after 5 seconds");
                Console.WriteLine("💡 System.IO.Ports.SerialPort may not work on this Linux system");
                return;
            }
            
            await openTask; // Check for exceptions
            Console.WriteLine("✅ SerialPort opened successfully!");
            
            Console.WriteLine("Step 3: Testing basic communication...");
            
            // Device ready sequence like the working implementation
            await SendControlCharacterAsync(serialPort, 0x03); // Ctrl-C
            await Task.Delay(100);
            await SendControlCharacterAsync(serialPort, 0x04); // Ctrl-D  
            await Task.Delay(500);
            
            // Test simple command
            var command = "print(2+2)\r\n";
            await serialPort.BaseStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(command));
            await serialPort.BaseStream.FlushAsync();
            
            Console.WriteLine("  Command sent, reading response...");
            await Task.Delay(300);
            
            // Read response
            var buffer = new byte[1024];
            var bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);
            var response = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
            
            Console.WriteLine($"  Response: '{response}'");
            
            serialPort.Close();
            Console.WriteLine("🎉 System.IO.Ports.SerialPort works on Linux!");
            
        }
        catch (PlatformNotSupportedException ex)
        {
            Console.WriteLine($"❌ Platform not supported: {ex.Message}");
            Console.WriteLine("💡 System.IO.Ports.SerialPort is not available on this platform");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"❌ Access denied: {ex.Message}");
            Console.WriteLine("💡 Check device permissions or if device is in use");
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
    
    static async Task SendControlCharacterAsync(SerialPort port, byte controlChar)
    {
        byte[] buffer = [controlChar];
        await port.BaseStream.WriteAsync(buffer);
        await port.BaseStream.FlushAsync();
    }
}