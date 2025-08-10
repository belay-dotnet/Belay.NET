using System;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

Console.WriteLine("=== Raw Protocol Analysis ===");

if (args.Length == 0)
{
    Console.WriteLine("Usage: RawProtocolAnalysis <device_path>");
    return;
}

var devicePath = args[0];
Console.WriteLine($"Analyzing: {devicePath}");

try
{
    using var serialPort = new SerialPort(devicePath, 115200);
    serialPort.Open();
    
    Console.WriteLine("✓ Serial port opened");
    
    // Helper function to send data and read response
    async Task SendAndReadAsync(string command, string description)
    {
        Console.WriteLine($"\n=== {description} ===");
        Console.WriteLine($"Sending: {command.Replace("\r", "\\r").Replace("\n", "\\n")}");
        
        // Send command
        var data = Encoding.UTF8.GetBytes(command);
        serialPort.Write(data, 0, data.Length);
        
        // Wait and read response
        await Task.Delay(1000);
        
        var response = new StringBuilder();
        while (serialPort.BytesToRead > 0)
        {
            var buffer = new byte[serialPort.BytesToRead];
            serialPort.Read(buffer, 0, buffer.Length);
            response.Append(Encoding.UTF8.GetString(buffer));
        }
        
        var responseStr = response.ToString();
        Console.WriteLine($"Response: '{responseStr}'");
        Console.WriteLine($"Length: {responseStr.Length}");
        Console.WriteLine($"Bytes: [{string.Join(", ", responseStr.Select(c => ((int)c).ToString()))}]");
        Console.WriteLine($"Hex: {string.Join(" ", responseStr.Select(c => $"0x{((int)c):X2}"))}");
        Console.WriteLine($"Escaped: {responseStr.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")}");
    }
    
    // Test sequence mimicking AdaptiveRawReplProtocol
    await SendAndReadAsync("\r\n", "Initial cleanup");
    await SendAndReadAsync("\x03", "Send Ctrl-C (interrupt)");
    await Task.Delay(200);
    
    // Drain any remaining output
    while (serialPort.BytesToRead > 0)
    {
        var buffer = new byte[serialPort.BytesToRead];
        serialPort.Read(buffer, 0, buffer.Length);
        await Task.Delay(100);
    }
    
    await SendAndReadAsync("\x01", "Enter Raw REPL (Ctrl-A)");
    await SendAndReadAsync("print('test1')\r\n", "Send simple print command");
    await SendAndReadAsync("\x04", "Execute (Ctrl-D)");
    
    await Task.Delay(500);
    
    // Try another command
    await SendAndReadAsync("2+2\r\n", "Send math expression");
    await SendAndReadAsync("\x04", "Execute (Ctrl-D)");
    
    await Task.Delay(500);
    
    await SendAndReadAsync("\x02", "Exit Raw REPL (Ctrl-B)");
    
    serialPort.Close();
    Console.WriteLine("\n✓ Analysis completed");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}