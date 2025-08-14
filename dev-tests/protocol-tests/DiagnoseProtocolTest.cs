// Diagnose sophisticated protocol initialization issue
using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Belay.Core;

class DiagnoseProtocolTest
{
    static async Task Main()
    {
        Console.WriteLine("🔧 Diagnose Sophisticated Protocol Initialization");
        Console.WriteLine("================================================");
        
        var devicePath = "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        
        try
        {
            // Test 1: Basic SerialPort stream operations
            Console.WriteLine("\n1. Testing basic SerialPort stream operations...");
            using var port = new SerialPort(devicePath, 115200, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            
            port.Open();
            Console.WriteLine("   ✅ Serial port opened");
            
            var stream = port.BaseStream;
            
            // Test stream.ReadAsync with timeout
            Console.WriteLine("\n2. Testing stream.ReadAsync with CancellationToken timeout...");
            var buffer = new byte[1024];
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            
            try
            {
                var bytesRead = await stream.ReadAsync(buffer, cts.Token);
                Console.WriteLine($"   ✅ ReadAsync returned {bytesRead} bytes");
                if (bytesRead > 0)
                {
                    var text = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"   Data: '{EscapeString(text)}'");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("   ✅ ReadAsync timed out correctly (no data available)");
            }
            
            // Test 3: Simple write + read
            Console.WriteLine("\n3. Testing simple write + read...");
            await stream.WriteAsync(new byte[] { 0x03, 0x0D, 0x0A }); // Ctrl-C + CRLF
            await stream.FlushAsync();
            await Task.Delay(200);
            
            using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                var bytesRead = await stream.ReadAsync(buffer, cts2.Token);
                Console.WriteLine($"   ✅ After Ctrl-C, got {bytesRead} bytes");
                if (bytesRead > 0)
                {
                    var text = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"   Response: '{EscapeString(text)}'");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("   ⚠️ No response to Ctrl-C after 2 seconds");
            }
            
            port.Close();
            
            // Test 4: AdaptiveRawReplProtocol initialization with detailed logging
            Console.WriteLine("\n4. Testing AdaptiveRawReplProtocol initialization...");
            
            var logger = NullLogger<AdaptiveRawReplProtocol>.Instance;
            
            var port2 = new SerialPort(devicePath, 115200, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 5000,
                WriteTimeout = 5000
            };
            
            port2.Open();
            Console.WriteLine("   ✅ Serial port opened for protocol test");
            
            var config = new RawReplConfiguration { EnableVerboseLogging = true };
            var protocol = new AdaptiveRawReplProtocol(port2.BaseStream, logger, config);
            
            Console.WriteLine("   🔄 Starting InitializeAsync with 10s timeout...");
            using var initCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            try
            {
                await protocol.InitializeAsync(initCts.Token);
                Console.WriteLine("   ✅ AdaptiveRawReplProtocol initialized successfully!");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("   ❌ AdaptiveRawReplProtocol initialization timed out after 10s");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ AdaptiveRawReplProtocol initialization failed: {ex.Message}");
                Console.WriteLine($"      Type: {ex.GetType().Name}");
            }
            
            protocol.Dispose();
            port2.Close();
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Test failed: {ex.Message}");
            Console.WriteLine($"Type: {ex.GetType().Name}");
        }
        
        Console.WriteLine("\n📊 Diagnosis complete");
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