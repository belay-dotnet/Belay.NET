// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Examples {
    using System;
    using System.Threading.Tasks;
    using Belay.Attributes;
    using Belay.Core;
    using Belay.Core.Communication;
    using Belay.Core.Execution;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Demonstrates the enhanced executor capabilities with device proxy and advanced attribute processing.
    /// This example shows how the enhanced executor provides seamless C# to MicroPython method execution
    /// with automatic attribute handling, pipeline processing, and method interception.
    /// </summary>
    public class EnhancedExecutorDemo {
        public static async Task Main(string[] args) {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder => 
                builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            var logger = loggerFactory.CreateLogger<EnhancedExecutorDemo>();

            logger.LogInformation("Starting Enhanced Executor Demo");

            // Connect to device (adjust port as needed)
            var devicePath = args.Length > 0 ? args[0] : "/dev/ttyUSB0";
            using var communication = new SerialDeviceCommunication(devicePath);
            using var device = new Device(communication, logger, loggerFactory);

            try {
                await device.ConnectAsync();
                logger.LogInformation("Connected to device successfully");

                // Demonstrate different execution approaches
                await DemonstrateBasicExecutor(device, logger);
                await DemonstrateEnhancedExecutor(device, logger);
                await DemonstrateDeviceProxy(device, logger);
                await DemonstrateExecutionStatistics(device, logger);

            } catch (Exception ex) {
                logger.LogError(ex, "Demo failed");
            } finally {
                await device.DisconnectAsync();
                logger.LogInformation("Disconnected from device");
            }
        }

        private static async Task DemonstrateBasicExecutor(Device device, ILogger logger) {
            logger.LogInformation("=== Basic Executor Demo ===");

            // Traditional approach using Task executor directly
            var result = await device.Task.ApplyPoliciesAndExecuteAsync<int>("2 + 2");
            logger.LogInformation("Basic execution result: {Result}", result);

            // Execute with timeout and caching
            await device.Task.ApplyPoliciesAndExecuteAsync("print('Hello from basic executor')");
        }

        private static async Task DemonstrateEnhancedExecutor(Device device, ILogger logger) {
            logger.LogInformation("=== Enhanced Executor Demo ===");

            // Get enhanced executor
            var enhancedExecutor = device.GetEnhancedExecutor(logger);

            // Execute through enhanced pipeline
            var calculator = new MathCalculator();
            var addMethod = typeof(MathCalculator).GetMethod(nameof(MathCalculator.Add))!;
            
            var result = await enhancedExecutor.ExecuteAsync<int>(addMethod, calculator, new object[] { 10, 20 });
            logger.LogInformation("Enhanced execution result: {Result}", result);

            // Show execution statistics
            var stats = enhancedExecutor.GetExecutionStatistics();
            logger.LogInformation("Intercepted methods: {Count}, Specialized executors: {ExecutorCount}", 
                stats.InterceptedMethodCount, stats.SpecializedExecutorCount);
        }

        private static async Task DemonstrateDeviceProxy(Device device, ILogger logger) {
            logger.LogInformation("=== Device Proxy Demo ===");

            // Create a device proxy for seamless method execution
            var sensorInterface = device.CreateProxy<ISensorDevice>(logger);

            // These method calls are automatically routed through the enhanced executor
            await sensorInterface.InitializeSensors();
            
            var temperature = await sensorInterface.ReadTemperature();
            logger.LogInformation("Temperature reading: {Temperature}°C", temperature);

            var humidity = await sensorInterface.ReadHumidity();
            logger.LogInformation("Humidity reading: {Humidity}%", humidity);

            var readings = await sensorInterface.GetAllReadings();
            logger.LogInformation("All readings: {Readings}", readings);

            await sensorInterface.Cleanup();
        }

        private static async Task DemonstrateExecutionStatistics(Device device, ILogger logger) {
            logger.LogInformation("=== Execution Statistics Demo ===");

            var stats = device.GetEnhancedExecutionStatistics();
            if (stats != null) {
                logger.LogInformation("Enhanced Execution Statistics:");
                logger.LogInformation("  - Intercepted Methods: {Count}", stats.InterceptedMethodCount);
                logger.LogInformation("  - Specialized Executors: {Count}", stats.SpecializedExecutorCount);
                logger.LogInformation("  - Pipeline Stages: {Count}", stats.PipelineStageCount);
                logger.LogInformation("  - Cache Entries: {Count}", stats.DeploymentCacheStatistics.CurrentEntryCount);
            }

            // Clear cache for demonstration
            device.ClearEnhancedExecutionCache();
            logger.LogInformation("Execution cache cleared");
        }
    }

    /// <summary>
    /// Example calculator class demonstrating Task attributes with the enhanced executor.
    /// </summary>
    public class MathCalculator {
        [Task(TimeoutMs = 5000, Cache = true)]
        public static string Add(int a, int b) => $"print({a} + {b})";

        [Task(TimeoutMs = 3000, Exclusive = true)]
        public static string Multiply(int a, int b) => $"print({a} * {b})";

        [Task(Cache = true)]
        public static string GetPi() => "import math; print(math.pi)";
    }

    /// <summary>
    /// Example sensor device interface for demonstrating device proxy capabilities.
    /// The proxy automatically routes these method calls through the enhanced executor.
    /// </summary>
    public interface ISensorDevice {
        [Setup]
        Task InitializeSensors();

        [Task(TimeoutMs = 2000)]
        Task<float> ReadTemperature();

        [Task(TimeoutMs = 2000)]
        Task<float> ReadHumidity();

        [Task(TimeoutMs = 5000, Cache = true)]
        Task<string> GetAllReadings();

        [Teardown]
        Task Cleanup();
    }

    /// <summary>
    /// Example implementation of sensor device methods.
    /// These methods return Python code that will be executed on the device.
    /// </summary>
    public class SensorDeviceImplementation : ISensorDevice {
        public Task InitializeSensors() {
            // This would normally return Python code to initialize sensors
            return Task.FromResult("# Sensor initialization\nprint('Sensors initialized')");
        }

        public Task<float> ReadTemperature() {
            // This would return Python code to read temperature
            return Task.FromResult(25.5f); // Simulated reading
        }

        public Task<float> ReadHumidity() {
            // This would return Python code to read humidity
            return Task.FromResult(60.0f); // Simulated reading
        }

        public Task<string> GetAllReadings() {
            return Task.FromResult(@"
# Read all sensor values
temp = 25.5
humidity = 60.0
print(f'Temperature: {temp}C, Humidity: {humidity}%')
");
        }

        public Task Cleanup() {
            return Task.FromResult("print('Sensors cleaned up')");
        }
    }

    /// <summary>
    /// Advanced example demonstrating thread attributes with enhanced execution.
    /// </summary>
    public interface IThreadedDevice {
        [Thread(AutoRestart = true)]
        Task StartBackgroundMonitoring();

        [Task(Exclusive = true)]
        Task StopAllThreads();
    }

    /// <summary>
    /// Example of advanced device control with multiple attribute types.
    /// </summary>
    public interface IAdvancedDevice {
        [Setup(Order = 1)]
        Task InitializeHardware();

        [Setup(Order = 2)]
        Task ConfigureSensors();

        [Thread(Name = "sensor_monitor")]
        Task StartSensorMonitoring();

        [Task(TimeoutMs = 10000, Exclusive = true, Cache = false)]
        Task<string> RunDiagnostics();

        [Task(Cache = true)]
        Task<string> GetDeviceInfo();

        [Teardown(Order = 1)]
        Task StopMonitoring();

        [Teardown(Order = 2)]
        Task ShutdownHardware();
    }
}