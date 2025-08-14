// Minimal test to verify Raw REPL protocol works
using System;
using System.Threading.Tasks;
using Belay.Core;

class MinimalTest
{
    static async Task Main()
    {
        try
        {
            using var serial = new LinuxSerialConnection("/dev/usb/tty-Board_in_FS_mode-e6614c311b7e6f35");
            await serial.OpenAsync();
            
            // Raw REPL test
            Console.WriteLine("Entering Raw REPL...");
            await serial.WriteAsync("\x01");
            await Task.Delay(500);
            
            var prompt = await serial.ReadExistingAsync();
            Console.WriteLine($"Prompt: '{prompt}'");
            
            if (prompt.Contains(">"))
            {
                Console.WriteLine("Sending 2+2...");
                await serial.WriteAsync("2+2");
                await serial.WriteAsync("\x04");
                await Task.Delay(1000);
                
                var result = await serial.ReadExistingAsync();
                Console.WriteLine($"Result: '{result}'");
                
                await serial.WriteAsync("\x02");
            }
            
            serial.Close();
            Console.WriteLine("Test complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}