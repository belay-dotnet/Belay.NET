// Debug Raw REPL Test to understand protocol timing
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Belay.Core;

public class DebugRawReplTest
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("🔍 DEBUG RAW REPL PROTOCOL TEST");
        Console.WriteLine("===============================");
        
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var logger = loggerFactory.CreateLogger<DeviceConnection>();
        var devicePath = "/dev/usb/tty-Board_in_FS_mode-e6614c311b7e6f35";
        
        try
        {
            using var serial = new LinuxSerialConnection(devicePath);
            
            Console.WriteLine($"Opening connection to: {devicePath}");
            await serial.OpenAsync();
            Console.WriteLine("✅ Connection opened");
            
            // Step 1: Send Ctrl-C to interrupt any running code and get to normal prompt
            Console.WriteLine("\n📤 Step 1: Sending Ctrl-C (interrupt)");
            await serial.WriteAsync("\x03");
            await Task.Delay(500);
            
            var response1 = await serial.ReadExistingAsync();
            Console.WriteLine($"📥 Response: '{response1}'");
            Console.WriteLine($"📥 Hex: {ToHex(response1)}");
            
            // Step 2: Send newline to get normal prompt
            Console.WriteLine("\n📤 Step 2: Sending newline");
            await serial.WriteAsync("\r\n");
            await Task.Delay(500);
            
            var response2 = await serial.ReadExistingAsync();
            Console.WriteLine($"📥 Response: '{response2}'");
            Console.WriteLine($"📥 Hex: {ToHex(response2)}");
            
            // Step 3: Enter raw mode with Ctrl-A
            Console.WriteLine("\n📤 Step 3: Entering raw mode (Ctrl-A)");
            await serial.WriteAsync("\x01");
            await Task.Delay(1000);
            
            var response3 = await serial.ReadExistingAsync();
            Console.WriteLine($"📥 Response: '{response3}'");
            Console.WriteLine($"📥 Hex: {ToHex(response3)}");
            
            if (response3.Contains(">"))
            {
                Console.WriteLine("✅ Raw mode entered successfully!");
                
                // Step 4: Send simple code
                Console.WriteLine("\n📤 Step 4: Sending code: 2 + 2");
                await serial.WriteAsync("2 + 2\r\n");
                await Task.Delay(200);
                
                var response4 = await serial.ReadExistingAsync();
                Console.WriteLine($"📥 Response: '{response4}'");
                Console.WriteLine($"📥 Hex: {ToHex(response4)}");
                
                // Step 5: Execute with Ctrl-D
                Console.WriteLine("\n📤 Step 5: Executing (Ctrl-D)");
                await serial.WriteAsync("\x04");
                await Task.Delay(2000);
                
                var response5 = await serial.ReadExistingAsync();
                Console.WriteLine($"📥 Response: '{response5}'");
                Console.WriteLine($"📥 Hex: {ToHex(response5)}");
                
                // Step 6: Exit raw mode with Ctrl-B
                Console.WriteLine("\n📤 Step 6: Exiting raw mode (Ctrl-B)");
                await serial.WriteAsync("\x02");
                await Task.Delay(500);
                
                var response6 = await serial.ReadExistingAsync();
                Console.WriteLine($"📥 Response: '{response6}'");
                Console.WriteLine($"📥 Hex: {ToHex(response6)}");
            }
            else
            {
                Console.WriteLine("❌ Failed to enter raw mode");
            }
            
            serial.Close();
            Console.WriteLine("\n✅ Debug test completed");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Debug test failed: {ex.Message}");
            return 1;
        }
    }
    
    private static string ToHex(string input)
    {
        if (string.IsNullOrEmpty(input)) return "(empty)";
        
        var bytes = Encoding.UTF8.GetBytes(input);
        var hex = new StringBuilder();
        foreach (byte b in bytes)
        {
            hex.Append($"{b:X2} ");
        }
        return hex.ToString().Trim();
    }
}