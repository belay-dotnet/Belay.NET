// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Belay.Core.Protocol;
using Belay.Core.Communication;
using Microsoft.Extensions.Logging;

namespace Belay.Tests.AdaptiveRepl
{
    public static class AdaptiveReplTest
    {
        public static async Task Main(string[] args)
        {
            // Set up logging
            using var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddConsole().SetMinimumLevel(LogLevel.Information));

            var logger = loggerFactory.CreateLogger<Program>();

            try
            {
                logger.LogInformation("Starting adaptive REPL protocol test...");

                // Set up subprocess communication with MicroPython Unix port
                var micropythonPath = "../micropython/ports/unix/build-standard/micropython";

                if (!System.IO.File.Exists(micropythonPath))
                {
                    logger.LogError("MicroPython Unix port not found at {Path}", micropythonPath);
                    return;
                }

                logger.LogInformation("Using MicroPython at {Path}", micropythonPath);

                // Create communication
                var commLogger = loggerFactory.CreateLogger<SubprocessDeviceCommunication>();
                var communication = new SubprocessDeviceCommunication(micropythonPath, logger: commLogger);

                // Test adaptive protocol configuration
                var config = new RawReplConfiguration
                {
                    EnableVerboseLogging = true,
                    EnableAdaptiveTiming = true,
                    EnableAdaptiveFlowControl = true,
                    EnableRawPasteAutoDetection = true,
                    MaxRetryAttempts = 2,
                    BaseResponseTimeout = TimeSpan.FromMilliseconds(1000),
                };

                // Test the adaptive configuration system
                logger.LogInformation("Testing adaptive REPL configuration system...");

                // Display configuration capabilities
                logger.LogInformation("Adaptive Protocol Configuration:");
                logger.LogInformation("  Base response timeout: {Timeout}ms", config.BaseResponseTimeout.TotalMilliseconds);
                logger.LogInformation("  Max response timeout: {MaxTimeout}ms", config.MaxResponseTimeout.TotalMilliseconds);
                logger.LogInformation("  Preferred window size: {WindowSize}", config.PreferredWindowSize?.ToString() ?? "Auto-detect");
                logger.LogInformation("  Max retry attempts: {Retries}", config.MaxRetryAttempts);
                logger.LogInformation("  Retry delay: {Delay}ms", config.RetryDelay.TotalMilliseconds);
                logger.LogInformation("  Startup delay: {Startup}ms", config.StartupDelay.TotalMilliseconds);
                logger.LogInformation("  Enable auto-detection: {AutoDetect}", config.EnableRawPasteAutoDetection);
                logger.LogInformation("  Enable adaptive timing: {AdaptiveTiming}", config.EnableAdaptiveTiming);
                logger.LogInformation("  Enable adaptive flow control: {AdaptiveFlow}", config.EnableAdaptiveFlowControl);

                // Test fallback configuration
                logger.LogInformation("Testing fallback configuration generation...");
                var fallbackConfig = config.CreateFallbackConfiguration();
                logger.LogInformation("Fallback Configuration:");
                logger.LogInformation("  Initialization timeout: {Timeout}ms", fallbackConfig.InitializationTimeout.TotalMilliseconds);
                logger.LogInformation("  Base response timeout: {Timeout}ms", fallbackConfig.BaseResponseTimeout.TotalMilliseconds);
                logger.LogInformation("  Preferred window size: {WindowSize}", fallbackConfig.PreferredWindowSize);
                logger.LogInformation("  Max retry attempts: {Retries}", fallbackConfig.MaxRetryAttempts);
                logger.LogInformation("  Retry delay: {Delay}ms", fallbackConfig.RetryDelay.TotalMilliseconds);
                logger.LogInformation("  Raw-paste auto-detection: {AutoDetect}", fallbackConfig.EnableRawPasteAutoDetection);

                // Test device capabilities structure
                logger.LogInformation("Testing device capabilities detection structure...");
                var mockCapabilities = new DeviceReplCapabilities
                {
                    SupportsRawPasteMode = true,
                    PreferredWindowSize = 64,
                    MaxWindowSize = 512,
                    AverageResponseTime = TimeSpan.FromMilliseconds(150),
                    RequiresExtendedStartup = false,
                    RequiresExtendedInterruptDelay = false,
                    DetectedPlatform = "linux",
                    MicroPythonVersion = "MicroPython v1.23.0 on 2024-06-02; linux [GCC 11.4.0] version",
                    HasReliableFlowControl = true,
                    SupportsLargeCodeTransfers = true,
                };

                logger.LogInformation("Mock Device Capabilities:");
                logger.LogInformation("  Platform: {Platform}", mockCapabilities.DetectedPlatform);
                logger.LogInformation("  Version: {Version}", mockCapabilities.MicroPythonVersion);
                logger.LogInformation("  Raw-paste mode: {RawPaste}", mockCapabilities.SupportsRawPasteMode);
                logger.LogInformation("  Window size: {WindowSize}", mockCapabilities.PreferredWindowSize);
                logger.LogInformation("  Average response time: {ResponseTime}ms", mockCapabilities.AverageResponseTime.TotalMilliseconds);
                logger.LogInformation("  Reliable flow control: {FlowControl}", mockCapabilities.HasReliableFlowControl);
                logger.LogInformation("  Supports large transfers: {LargeTransfers}", mockCapabilities.SupportsLargeCodeTransfers);

                // Test protocol metrics tracking
                logger.LogInformation("Testing protocol metrics tracking...");
                var metrics = new ReplProtocolMetrics();

                // Simulate some operations
                metrics.SuccessfulOperations = 15;
                metrics.FailedOperations = 2;
                metrics.RetryAttempts = 3;
                metrics.AdaptiveAdjustments = 5;
                metrics.AverageOperationTime = TimeSpan.FromMilliseconds(245);

                logger.LogInformation("Protocol Metrics:");
                logger.LogInformation("  Successful operations: {Successful}", metrics.SuccessfulOperations);
                logger.LogInformation("  Failed operations: {Failed}", metrics.FailedOperations);
                logger.LogInformation("  Success rate: {SuccessRate:F1}%", metrics.SuccessRate);
                logger.LogInformation("  Retry attempts: {Retries}", metrics.RetryAttempts);
                logger.LogInformation("  Adaptive adjustments: {Adjustments}", metrics.AdaptiveAdjustments);
                logger.LogInformation("  Average operation time: {AvgTime}ms", metrics.AverageOperationTime.TotalMilliseconds);

                // Test actual code execution with existing protocol
                logger.LogInformation("Testing code execution with existing protocol...");
                await communication.StartAsync();

                var result1 = await communication.ExecuteAsync("2 + 3");
                logger.LogInformation("Simple math result: '{Result}'", result1.Trim());

                var result2 = await communication.ExecuteAsync("import sys; sys.platform");
                logger.LogInformation("Platform detection: '{Result}'", result2.Trim());

                await communication.StopAsync();

                logger.LogInformation("✅ Adaptive REPL protocol test completed successfully!");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Adaptive REPL protocol test failed");
                throw;
            }
        }

        private class Program
        {
        }
    }
}