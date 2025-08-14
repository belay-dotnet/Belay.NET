// Test using the exact working RawReplProtocol implementation from tagged commit
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Belay.Core;

class WorkingRawReplTest
{
    // Copy the exact constants from working version
    public const byte ENTER_RAW = 0x01;
    public const byte EXIT_RAW = 0x02;
    public const byte INTERRUPT = 0x03;
    public const byte EXECUTE = 0x04;
    public const byte RAW_PASTE = 0x05;

    private const string RAW_PROMPT = "raw REPL; CTRL-B to exit\r\n>";
    private const string NORMAL_PROMPT = ">";

    static async Task Main()
    {
        Console.WriteLine("🔧 Working Raw REPL Protocol Test");
        Console.WriteLine("==================================");
        
        var devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        try
        {
            var connection = new DeviceConnection(
                DeviceConnection.ConnectionType.Serial, 
                devicePath, 
                NullLogger<DeviceConnection>.Instance);
                
            Console.WriteLine("Connecting...");
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await connection.ConnectAsync(connectCts.Token);
            Console.WriteLine("✅ Connected");
            
            // Get the stream using reflection (same as debugging approach)
            var connectionType = typeof(DeviceConnection);
            var serialPortField = connectionType.GetField("serialPort", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var serialPort = (System.IO.Ports.SerialPort)serialPortField.GetValue(connection);
            
            if (serialPort == null)
            {
                Console.WriteLine("❌ No serial port available");
                return;
            }
            
            // Test using exact working implementation
            Console.WriteLine("Testing with exact working RawReplProtocol.ExecuteCode...");
            using var execCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var result = await ExecuteCode(serialPort.BaseStream, "print(2 + 2)", execCts.Token);
            Console.WriteLine($"✅ Result: '{result}'");
            
            if (result.Contains("4"))
            {
                Console.WriteLine("🎉 Working protocol test PASSED!");
            }
            else
            {
                Console.WriteLine($"⚠️ Unexpected result, but communication worked. Got: '{result}'");
            }
            
            await connection.DisconnectAsync();
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"   Stack: {ex.StackTrace}");
        }
    }
    
    // Copy the exact working ExecuteCode method
    public static async Task<string> ExecuteCode(Stream stream, string pythonCode, CancellationToken cancellationToken = default)
    {
        try
        {
            // Enter raw mode
            await stream.WriteAsync(new byte[] { ENTER_RAW }, cancellationToken);
            await WaitForPrompt(stream, NORMAL_PROMPT, cancellationToken);

            // Send code
            var codeBytes = Encoding.UTF8.GetBytes(pythonCode);
            await stream.WriteAsync(codeBytes, cancellationToken);

            // Execute
            await stream.WriteAsync(new byte[] { EXECUTE }, cancellationToken);

            // Read result
            var result = await ReadUntilPrompt(stream, cancellationToken);

            // Exit raw mode
            await stream.WriteAsync(new byte[] { EXIT_RAW }, cancellationToken);

            return result;
        }
        catch (Exception ex) when (!(ex is DeviceException))
        {
            throw new DeviceException($"Raw REPL execution failed: {ex.Message}", ex)
            {
                ExecutedCode = pythonCode
            };
        }
    }
    
    // Copy the exact working WaitForPrompt method
    private static async Task WaitForPrompt(Stream stream, string expectedPrompt, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var received = new StringBuilder();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
                throw new DeviceException("Device disconnected while waiting for prompt");

            received.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
            
            if (received.ToString().Contains(expectedPrompt))
                return;
        }
        
        throw new OperationCanceledException("Timeout waiting for device prompt");
    }

    // Copy the exact working ReadUntilPrompt method with debugging
    private static async Task<string> ReadUntilPrompt(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var result = new StringBuilder();
        
        Console.WriteLine("  DEBUG: ReadUntilPrompt starting...");
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                Console.WriteLine("  DEBUG: No more bytes to read, breaking");
                break;
            }

            var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            result.Append(text);
            
            Console.WriteLine($"  DEBUG: Read {bytesRead} bytes: '{EscapeString(text)}'");
            Console.WriteLine($"  DEBUG: Total so far: '{EscapeString(result.ToString())}'");
            
            // Check for prompt indicating end of output
            if (text.Contains("\x04>"))
            {
                Console.WriteLine("  DEBUG: Found \\x04> prompt, breaking");
                break;
            }
        }
        
        var output = result.ToString();
        Console.WriteLine($"  DEBUG: Raw output before cleanup: '{EscapeString(output)}'");
        
        // Clean up the output (remove control characters and prompts)
        // Actual format is: OK<result>\r\n\x04\x04>
        if (output.StartsWith("OK"))
        {
            Console.WriteLine("  DEBUG: Output starts with OK, removing prefix");
            output = output[2..]; // Remove "OK"
        }
        
        // Remove the trailing \r\n\x04\x04> sequence
        var endIndex = output.IndexOf("\r\n\x04\x04>");
        if (endIndex >= 0)
        {
            Console.WriteLine($"  DEBUG: Found \\r\\n\\x04\\x04> at index {endIndex}");
            output = output[..endIndex];
        }
        else
        {
            Console.WriteLine("  DEBUG: \\r\\n\\x04\\x04> not found, trying just \\x04\\x04>");
            endIndex = output.LastIndexOf("\x04\x04>");
            if (endIndex >= 0)
            {
                output = output[..endIndex];
            }
        }
        
        var finalResult = output.Trim();
        Console.WriteLine($"  DEBUG: Final result: '{EscapeString(finalResult)}'");
        
        return finalResult;
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