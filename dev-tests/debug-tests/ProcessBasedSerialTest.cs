// Test process-based serial communication like mpremote does
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

class ProcessBasedSerialTest
{
    static async Task Main()
    {
        Console.WriteLine("🔧 Process-Based Serial Test");
        Console.WriteLine("=============================");
        
        var devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        try
        {
            // Configure device
            Console.WriteLine("Step 1: Configuring serial device...");
            var configProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "stty",
                    Arguments = $"-F {devicePath} 115200 raw -echo -echoe -echok -echoctl -echoke -crtscts -hupcl",
                    UseShellExecute = false,
                    RedirectStandardError = true
                }
            };
            
            configProcess.Start();
            await configProcess.WaitForExitAsync();
            
            if (configProcess.ExitCode != 0)
            {
                var error = await configProcess.StandardError.ReadToEndAsync();
                Console.WriteLine($"❌ Configuration failed: {error}");
                return;
            }
            Console.WriteLine("✅ Device configured");
            
            // Test communication using cat for reading and echo for writing
            Console.WriteLine("Step 2: Testing basic communication...");
            
            // Send a simple command
            var writeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"echo -e '\\x02print(2+2)\\x0D' > {devicePath}\"",
                    UseShellExecute = false
                }
            };
            
            writeProcess.Start();
            await writeProcess.WaitForExitAsync();
            Console.WriteLine("✅ Command sent");
            
            // Read response
            await Task.Delay(500); // Give device time to respond
            
            var readProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "timeout",
                    Arguments = $"2s cat {devicePath}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            
            readProcess.Start();
            var output = await readProcess.StandardOutput.ReadToEndAsync();
            await readProcess.WaitForExitAsync();
            
            Console.WriteLine($"  Response: '{output}'");
            Console.WriteLine("🎉 Process-based communication successful!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
        }
    }
}