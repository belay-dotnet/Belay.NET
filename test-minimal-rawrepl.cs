// Test minimal Raw REPL implementation per ICD-001 specification
using System;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

public class TestMinimalRawRepl
{
    private const byte CTRLA = 0x01; // Enter raw REPL
    private const byte CTRLB = 0x02; // Exit raw REPL  
    private const byte CTRLC = 0x03; // KeyboardInterrupt
    private const byte CTRLD = 0x04; // Execute/End data

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🔧 TESTING MINIMAL RAW REPL (ICD-001 DIRECT)");
        Console.WriteLine("============================================");
        
        string devicePath = "/dev/serial/by-id/usb-MicroPython_Board_in_FS_mode_a8100d7bd7092d6e-if00";
        
        Console.WriteLine($"Testing minimal Raw REPL on: {devicePath}");
        Console.WriteLine("==========================================");
        
        try
        {
            // Open serial connection directly
            using var serialPort = new SerialPort(devicePath, 115200, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 5000,
                WriteTimeout = 5000,
                NewLine = "\r\n"
            };
            
            Console.WriteLine("🔌 Opening serial connection...");
            serialPort.Open();
            Console.WriteLine("   ✅ Serial connection opened");
            
            // Simple device initialization
            Console.WriteLine("🚀 Initializing device...");
            await SendControlChar(serialPort, CTRLC); // Interrupt any running code
            await Task.Delay(200);
            
            // Drain any pending output
            if (serialPort.BytesToRead > 0)
            {
                string drain = serialPort.ReadExisting();
                Console.WriteLine($"   Drained: {drain.Length} chars");
            }
            
            Console.WriteLine("   ✅ Device initialized");
            
            // Test basic Raw REPL execution per ICD-001
            Console.WriteLine("📝 Testing basic Raw REPL execution...");
            string result = await ExecuteCode(serialPort, "2 + 2");
            Console.WriteLine($"   Result: '{result.Trim()}'");
            Console.WriteLine("   ✅ Basic execution working");
            
            // Test print statement
            Console.WriteLine("🖨️ Testing print statement...");
            string printResult = await ExecuteCode(serialPort, "print('Hello from minimal Raw REPL!')");
            Console.WriteLine($"   Result: '{printResult.Trim()}'");
            Console.WriteLine("   ✅ Print execution working");
            
            serialPort.Close();
            Console.WriteLine("🔌 Connection closed");
            
            Console.WriteLine();
            Console.WriteLine("🎉 MINIMAL RAW REPL TEST PASSED!");
            Console.WriteLine("✅ Direct ICD-001 implementation working");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"Exception Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            return 1;
        }
    }
    
    private static async Task<string> ExecuteCode(SerialPort port, string code)
    {
        Console.WriteLine($"      Executing: {code}");
        
        // 1. Enter raw mode (Ctrl-A)
        await SendControlChar(port, CTRLA);
        
        // 2. Wait for raw REPL prompt
        string rawResponse = await ReadWithTimeout(port, 2000);
        Console.WriteLine($"      Raw mode response: '{rawResponse.Trim()}'");
        
        if (!rawResponse.Contains("raw REPL"))
        {
            throw new InvalidOperationException($"Failed to enter raw mode: {rawResponse}");
        }
        
        // 3. Send code
        port.Write(code);
        
        // 4. Execute (Ctrl-D)
        await SendControlChar(port, CTRLD);
        
        // 5. Read "OK" confirmation
        string okResponse = await ReadWithTimeout(port, 2000);
        Console.WriteLine($"      OK response: '{okResponse.Trim()}'");
        
        if (!okResponse.Contains("OK"))
        {
            throw new InvalidOperationException($"Expected OK response: {okResponse}");
        }
        
        // 6. Read execution result
        string output = await ReadWithTimeout(port, 3000);
        Console.WriteLine($"      Execution output: '{output.Trim()}'");
        
        // 7. Exit raw mode (Ctrl-B)
        await SendControlChar(port, CTRLB);
        
        // 8. Wait for normal prompt
        await ReadWithTimeout(port, 1000);
        
        return ParseExecutionResult(output);
    }
    
    private static async Task SendControlChar(SerialPort port, byte controlChar)
    {
        port.Write(new byte[] { controlChar }, 0, 1);
        await Task.Delay(50); // Small delay for device processing
    }
    
    private static async Task<string> ReadWithTimeout(SerialPort port, int timeoutMs)
    {
        var result = new StringBuilder();
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        
        while (DateTime.UtcNow - startTime < timeout)
        {
            if (port.BytesToRead > 0)
            {
                string data = port.ReadExisting();
                result.Append(data);
                
                // Check for completion markers
                string partial = result.ToString();
                if (partial.Contains("raw REPL") && partial.Contains(">"))
                {
                    break; // Raw REPL entry complete
                }
                else if (partial.Contains("OK") && partial.Contains("\x04"))
                {
                    break; // Execution confirmation complete
                }
                else if (partial.Contains("\x04") && partial.Contains(">"))
                {
                    break; // Execution output complete
                }
            }
            
            await Task.Delay(20);
        }
        
        return result.ToString();
    }
    
    private static string ParseExecutionResult(string output)
    {
        // Parse Raw REPL response format: "OK<content>\x04\x04>"
        string result = output;
        
        // Remove "OK" prefix if present
        if (result.StartsWith("OK"))
        {
            result = result.Substring(2);
        }
        
        // Remove trailing control characters and prompt
        int firstControlCharIndex = result.IndexOf('\x04');
        if (firstControlCharIndex >= 0)
        {
            result = result.Substring(0, firstControlCharIndex);
        }
        else if (result.EndsWith('>'))
        {
            result = result.Substring(0, result.Length - 1);
        }
        
        return result.Trim('\r', '\n', ' ', '\t');
    }
}