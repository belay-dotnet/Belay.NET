using Belay.Core;
using Microsoft.Extensions.Logging;
using System.IO.Ports;

class DiagnosticProgram 
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Diagnostic test for MicroPython protocol...");
        
        string portName = "/dev/usb/tty-STM32_STLink-066FFF303430484257255318";
        
        try
        {
            // Test raw serial communication first
            Console.WriteLine("=== Testing Raw Serial Communication ===");
            using var port = new SerialPort(portName, 115200);
            port.Open();
            
            // Send interrupt first to clear any state
            port.Write(new byte[] { 0x03 }, 0, 1); // Ctrl-C
            await Task.Delay(100);
            
            // Try to get to normal REPL
            port.Write(new byte[] { 0x02 }, 0, 1); // Ctrl-B (exit raw mode)
            await Task.Delay(500);
            
            // Read any available data
            if (port.BytesToRead > 0)
            {
                byte[] buffer = new byte[port.BytesToRead];
                port.Read(buffer, 0, buffer.Length);
                Console.WriteLine($"Initial response: {System.Text.Encoding.UTF8.GetString(buffer)}");
            }
            
            // Send a simple command
            port.WriteLine("print('test')");
            await Task.Delay(1000);
            
            if (port.BytesToRead > 0)
            {
                byte[] buffer = new byte[port.BytesToRead];
                port.Read(buffer, 0, buffer.Length);
                string response = System.Text.Encoding.UTF8.GetString(buffer);
                Console.WriteLine($"Print test response: {response}");
            }
            
            port.Close();
            
            Console.WriteLine("\n=== Testing DeviceConnection ===");
            // Test with DeviceConnection and more verbose logging
            using var loggerFactory = LoggerFactory.Create(builder => 
                builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
            var logger = loggerFactory.CreateLogger<DeviceConnection>();
            
            using var device = new DeviceConnection(
                DeviceConnection.ConnectionType.Serial,
                portName,
                logger);
            
            Console.WriteLine("Connecting to device...");
            await device.ConnectAsync();
            Console.WriteLine("✅ Connection successful!");
            
            // Try a very simple execution
            var result = await device.ExecuteAsync("1+1");
            Console.WriteLine($"✅ Simple execution: {result}");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"   Stack: {ex.StackTrace}");
        }
    }
}