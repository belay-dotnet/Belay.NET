// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Examples;

using Belay.Core;
using Belay.Core.Execution;
using Microsoft.Extensions.Logging;

/// <summary>
/// Example demonstrating how to use the attribute-driven IEnvironmentMonitor
/// with method interception for seamless C# to MicroPython communication.
/// </summary>
/// <remarks>
/// <para>
/// This example shows the power of the attribute system - the interface methods
/// are automatically converted to Python code execution without any manual
/// ExecuteAsync calls. The method interception infrastructure handles everything.
/// </para>
/// </remarks>
public static class EnvironmentMonitorExample {
    /// <summary>
    /// Demonstrates basic usage of the attribute-driven environment monitor.
    /// </summary>
    /// <param name="connectionString">Device connection string (e.g., "serial:COM3").</param>
    /// <param name="loggerFactory">Optional logger factory for debugging.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task BasicUsageExampleAsync(string connectionString, ILoggerFactory? loggerFactory = null) {
        // Create device and connect
        using var device = Device.FromConnectionString(connectionString, loggerFactory);
        await device.ConnectAsync();

        // Create the environment monitor using method interception
        // This automatically converts interface method calls to Python execution
        var monitor = device.CreateProxy<IEnvironmentMonitor>();

        try {
            // Setup methods are automatically called in order during connection
            // You can also call them manually if needed
            Console.WriteLine("=== Setting up environment monitor ===");
            await monitor.InitializeHardwareAsync();
            await monitor.LoadCalibrationAsync();
            await monitor.InitializeMonitoringStateAsync();

            // Task methods execute Python code automatically
            Console.WriteLine("\n=== Taking sensor readings ===");
            var reading = await monitor.GetCurrentReadingAsync();
            Console.WriteLine($"Current conditions: {reading}");

            // Get diagnostic information
            var diagnostics = await monitor.GetDiagnosticsAsync();
            Console.WriteLine($"System health: {diagnostics["systemInfo"]}");

            // Demonstrate calibration
            Console.WriteLine("\n=== Calibrating sensors ===");
            await monitor.CalibrateSensorsAsync(referenceTemp: 22.5f, referenceHumidity: 45.0f);

            // Take another reading after calibration
            reading = await monitor.GetCurrentReadingAsync();
            Console.WriteLine($"Post-calibration reading: {reading}");

            Console.WriteLine("\n=== Environment monitor demo completed ===");
        }
        finally {
            // Teardown methods are automatically called during disconnection
            // But you can also call them manually for explicit cleanup
            await monitor.StopBackgroundThreadsAsync();
            await monitor.SaveDataAsync();
            await monitor.CleanupHardwareAsync();
        }

        await device.DisconnectAsync();
    }

    /// <summary>
    /// Demonstrates continuous monitoring with background threads.
    /// </summary>
    /// <param name="connectionString">Device connection string.</param>
    /// <param name="monitoringDurationSeconds">How long to monitor for.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task ContinuousMonitoringExampleAsync(
        string connectionString,
        int monitoringDurationSeconds = 60,
        ILoggerFactory? loggerFactory = null) {
        using var device = Device.FromConnectionString(connectionString, loggerFactory);
        await device.ConnectAsync();

        var monitor = device.CreateProxy<IEnvironmentMonitor>();

        try {
            Console.WriteLine("=== Starting continuous monitoring ===");

            // Setup the monitor
            await monitor.InitializeHardwareAsync();
            await monitor.LoadCalibrationAsync();
            await monitor.InitializeMonitoringStateAsync();

            // Start background monitoring threads
            await monitor.StartContinuousMonitoringAsync(intervalMs: 2000); // Every 2 seconds
            await monitor.StartHealthWatchdogAsync(); // Health monitoring

            Console.WriteLine($"Monitoring for {monitoringDurationSeconds} seconds...");
            Console.WriteLine("Watch for sensor readings and alerts in the output.");

            // Let monitoring run for the specified duration
            await Task.Delay(TimeSpan.FromSeconds(monitoringDurationSeconds));

            // Take a final reading
            var finalReading = await monitor.GetCurrentReadingAsync();
            Console.WriteLine($"\nFinal reading: {finalReading}");

            // Get final diagnostics
            var diagnostics = await monitor.GetDiagnosticsAsync();
            var sensorStatus = (Dictionary<string, object>)diagnostics["sensorStatus"];
            Console.WriteLine($"Total readings: {sensorStatus["readingCount"]}");
            Console.WriteLine($"Total errors: {sensorStatus["errorCount"]}");

            // Stop monitoring
            await monitor.StopMonitoringAsync();

            Console.WriteLine("=== Continuous monitoring completed ===");
        }
        catch (Exception ex) {
            Console.WriteLine($"Monitoring error: {ex.Message}");
            await monitor.StopMonitoringAsync();
            throw;
        }

        await device.DisconnectAsync();
    }

    /// <summary>
    /// Demonstrates error handling and recovery with the attribute system.
    /// </summary>
    /// <param name="connectionString">Device connection string.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task ErrorHandlingExampleAsync(string connectionString, ILoggerFactory? loggerFactory = null) {
        using var device = Device.FromConnectionString(connectionString, loggerFactory);
        await device.ConnectAsync();

        var monitor = device.CreateProxy<IEnvironmentMonitor>();

        try {
            Console.WriteLine("=== Error handling demonstration ===");

            // Setup with error handling
            await monitor.InitializeHardwareAsync();
            await monitor.LoadCalibrationAsync();
            await monitor.InitializeMonitoringStateAsync();

            // Attempt operations with retry logic
            EnvironmentReading? reading = null;
            int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++) {
                try {
                    Console.WriteLine($"Attempt {attempt}/{maxRetries} to read sensors...");
                    reading = await monitor.GetCurrentReadingAsync();
                    Console.WriteLine($"Success! Reading: {reading}");
                    break;
                }
                catch (Exception ex) {
                    Console.WriteLine($"Attempt {attempt} failed: {ex.Message}");

                    if (attempt == maxRetries) {
                        Console.WriteLine("All attempts failed. Checking diagnostics...");

                        try {
                            var diagnostics = await monitor.GetDiagnosticsAsync();
                            var sensorStatus = (Dictionary<string, object>)diagnostics["sensorStatus"];
                            Console.WriteLine($"Error count: {sensorStatus["errorCount"]}");

                            if (sensorStatus.ContainsKey("healthCheckError")) {
                                Console.WriteLine($"Health check error: {sensorStatus["healthCheckError"]}");
                            }
                        }
                        catch {
                            Console.WriteLine("Could not retrieve diagnostics.");
                        }

                        throw;
                    }

                    // Wait before retry
                    await Task.Delay(1000);
                }
            }

            Console.WriteLine("=== Error handling demonstration completed ===");
        }
        finally {
            // Cleanup - teardown methods with IgnoreErrors=true won't fail the disconnect
            await monitor.StopBackgroundThreadsAsync();
            await monitor.SaveDataAsync(); // This has IgnoreErrors=true
            await monitor.CleanupHardwareAsync();
        }

        await device.DisconnectAsync();
    }

    /// <summary>
    /// Main entry point for the environment monitor examples.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllExamplesAsync() {
        // For testing in Core project, use NullLoggerFactory
        // In real applications, use Microsoft.Extensions.Logging
        ILoggerFactory? loggerFactory = null;

        // Use subprocess for testing (no hardware required)
        var connectionString = "subprocess:micropython"; // Assumes micropython is in PATH

        // Alternative: Use serial connection if you have hardware
        // var connectionString = "serial:COM3"; // Windows
        // var connectionString = "serial:/dev/ttyACM0"; // Linux
        try {
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║     Belay.NET Attribute System Demo     ║");
            Console.WriteLine("║    Environment Monitor with Method       ║");
            Console.WriteLine("║           Interception                   ║");
            Console.WriteLine("╚══════════════════════════════════════════╝\n");

            // Run basic usage example
            Console.WriteLine("1. Basic Usage Example");
            Console.WriteLine("─".PadRight(50, '─'));
            await BasicUsageExampleAsync(connectionString, loggerFactory);
            Console.WriteLine("\nPress any key to continue to continuous monitoring...");
            Console.ReadKey();

            // Run continuous monitoring example (shorter duration for demo)
            Console.WriteLine("\n\n2. Continuous Monitoring Example");
            Console.WriteLine("─".PadRight(50, '─'));
            await ContinuousMonitoringExampleAsync(connectionString, monitoringDurationSeconds: 15, loggerFactory);
            Console.WriteLine("\nPress any key to continue to error handling...");
            Console.ReadKey();

            // Run error handling example
            Console.WriteLine("\n\n3. Error Handling Example");
            Console.WriteLine("─".PadRight(50, '─'));
            await ErrorHandlingExampleAsync(connectionString, loggerFactory);

            Console.WriteLine("\n╔══════════════════════════════════════════╗");
            Console.WriteLine("║         All Examples Completed!         ║");
            Console.WriteLine("║                                          ║");
            Console.WriteLine("║  The attribute system automatically     ║");
            Console.WriteLine("║  converted C# method calls to Python    ║");
            Console.WriteLine("║  execution - no manual ExecuteAsync!    ║");
            Console.WriteLine("╚══════════════════════════════════════════╝");
        }
        catch (Exception ex) {
            Console.WriteLine($"\n❌ Example failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
