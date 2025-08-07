using System;
using System.Threading.Tasks;
using Belay.Core.Communication;
using Microsoft.Extensions.Logging;

namespace DebugTest;

class Program
{
    static async Task Main()
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger<SubprocessDeviceCommunication>();
        
        var device = new SubprocessDeviceCommunication(
            "./micropython/ports/unix/build-standard/micropython",
            logger: logger);

        try
        {
            Console.WriteLine("Starting device...");
            await device.StartAsync();
            
            Console.WriteLine("Device started, executing code...");
            var result = await device.ExecuteAsync("1 + 2");
            
            Console.WriteLine($"Result: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }
        finally
        {
            await device.StopAsync();
            device.Dispose();
        }
    }
}