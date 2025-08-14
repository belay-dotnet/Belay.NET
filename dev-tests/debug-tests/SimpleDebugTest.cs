// Simple debug test to understand ESP32C6 behavior
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Belay.Core;

class SimpleDebugTest
{
    static async Task Main()
    {
        Console.WriteLine("🔍 Simple ESP32C6 Debug Test");
        
        var devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        try
        {
            using var serial = new LinuxSerialConnection(devicePath);
            Console.WriteLine("Opening serial connection...");
            await serial.OpenAsync();
            Console.WriteLine("✅ Connected");
            
            // Step 1: Send interrupt to clear state
            Console.WriteLine("Sending interrupt (Ctrl-C)...");
            await serial.WriteAsync("\x03");
            await Task.Delay(500);
            
            var interrupt = await serial.ReadExistingAsync();
            Console.WriteLine($"Interrupt response: '{interrupt}' (hex: {ToHex(interrupt)})");
            
            // Step 2: Enter raw REPL
            Console.WriteLine("Entering raw REPL (Ctrl-A)...");
            await serial.WriteAsync("\x01");
            await Task.Delay(1000);
            
            var rawResp = await serial.ReadExistingAsync();
            Console.WriteLine($"Raw REPL response: '{rawResp}' (hex: {ToHex(rawResp)})");
            
            if (rawResp.Contains("raw REPL"))
            {
                Console.WriteLine("✅ Successfully entered raw REPL mode");
                
                // Step 3: Send simple code
                Console.WriteLine("Sending '1+1'...");
                await serial.WriteAsync("1+1");
                await Task.Delay(100);
                
                // Step 4: Execute with Ctrl-D
                Console.WriteLine("Executing (Ctrl-D)...");
                await serial.WriteAsync("\x04");
                await Task.Delay(1000);
                
                var execResp = await serial.ReadExistingAsync();
                Console.WriteLine($"Execution response: '{execResp}' (hex: {ToHex(execResp)})");
                
                if (execResp.Contains("OK"))
                {
                    Console.WriteLine("✅ Got OK response");
                    
                    // Parse like our actual implementation
                    string result = execResp;
                    if (result.StartsWith("OK"))
                    {
                        result = result.Substring(2);
                    }
                    
                    int firstControlCharIndex = result.IndexOf('\x04');
                    if (firstControlCharIndex >= 0)
                    {
                        result = result.Substring(0, firstControlCharIndex);
                    }
                    
                    result = result.Trim('\r', '\n', ' ', '\t');
                    Console.WriteLine($"Parsed result: '{result}'");
                    
                    // Check if there's more data
                    await Task.Delay(500);
                    var additional = await serial.ReadExistingAsync();
                    if (!string.IsNullOrEmpty(additional))
                    {
                        Console.WriteLine($"Additional data: '{additional}' (hex: {ToHex(additional)})");
                    }
                }
                
                // Exit raw REPL
                Console.WriteLine("Exiting raw REPL (Ctrl-B)...");
                await serial.WriteAsync("\x02");
                await Task.Delay(500);
                
                var exitResp = await serial.ReadExistingAsync();
                Console.WriteLine($"Exit response: '{exitResp}' (hex: {ToHex(exitResp)})");
            }
            else
            {
                Console.WriteLine("❌ Failed to enter raw REPL mode");
            }
            
            serial.Close();
            Console.WriteLine("Test completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    
    static string ToHex(string input)
    {
        if (string.IsNullOrEmpty(input)) return "(empty)";
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        return string.Join(" ", bytes.Select(b => $"{b:X2}"));
    }
}