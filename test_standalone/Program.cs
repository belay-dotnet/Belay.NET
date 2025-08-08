using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Belay.Extensions;
using Belay.Extensions.Factories;
using Belay.Core;

class Program 
{
    static async Task Main(string[] args) 
    {
        Console.WriteLine("🚀 ESP32 DI Integration Test Starting...");
        Console.WriteLine($"   Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"   Target Device: /dev/ttyACM1");

        try 
        {
            // Test 1: DI Container Setup
            Console.WriteLine("\n=== Test 1: DI Container Setup ===");
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
            
            // Configure Belay.NET with our new DI infrastructure
            services.AddBelay(config => {
                config.Device.DefaultConnectionTimeoutMs = 10000;
                config.Communication.Serial.DefaultBaudRate = 115200;
                config.Session.MaxConcurrentSessions = 5;
            });

            var serviceProvider = services.BuildServiceProvider();
            
            // Verify factories are registered
            var deviceFactory = serviceProvider.GetBelayDeviceFactory();
            var executorFactory = serviceProvider.GetBelayExecutorFactory();
            
            Console.WriteLine("✅ DI Container configured successfully");
            Console.WriteLine($"   DeviceFactory: {deviceFactory.GetType().Name}");
            Console.WriteLine($"   ExecutorFactory: {executorFactory.GetType().Name}");

            // Test 2: Device Creation via Factory
            Console.WriteLine("\n=== Test 2: Device Creation via Factory ===");
            using var device = deviceFactory.CreateSerialDevice("/dev/ttyACM1");
            Console.WriteLine($"✅ Device created via factory: {device.GetType().Name}");

            // Test 3: ESP32 Connection and Basic Communication
            Console.WriteLine("\n=== Test 3: ESP32 Connection and Communication ===");
            await device.ConnectAsync();
            Console.WriteLine("✅ Connected to ESP32");

            // Test simple command execution
            var result1 = await device.ExecuteAsync<int>("2 + 3");
            Console.WriteLine($"✅ Basic math: 2 + 3 = {result1}");
            
            // Test string operation
            var result2 = await device.ExecuteAsync<string>("'ESP32 ' + 'DI Test'");
            Console.WriteLine($"✅ String concat: {result2}");

            // Test device identification
            var platformInfo = await device.ExecuteAsync<string>("import sys; sys.platform");
            Console.WriteLine($"✅ Platform: {platformInfo}");

            await device.DisconnectAsync();
            Console.WriteLine("✅ Disconnected from ESP32");

            // Final Summary
            Console.WriteLine("\n=== INTEGRATION TEST SUMMARY ===");
            Console.WriteLine($"🎉 ALL TESTS PASSED!");
            Console.WriteLine($"✅ DI Infrastructure working with ESP32");
            Console.WriteLine($"✅ Device: /dev/ttyACM1 ({platformInfo})");
            Console.WriteLine($"✅ Tests completed at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n💥 TEST FAILED: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}