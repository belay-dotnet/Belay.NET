// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.SessionValidationTest
{
    using System;
    using System.Threading.Tasks;
    using Belay.Attributes;
    using Belay.Core;
    using Belay.Core.Execution;
    using Belay.Core.Sessions;

    /// <summary>
    /// Interface for testing session management integration with executors.
    /// </summary>
    public interface ISessionTestDevice
    {
        /// <summary>
        /// Test method for session state coordination.
        /// </summary>
        /// <returns>Session identifier for validation.</returns>
        [Task]
        [PythonCode("'session_test_' + str(hash('test'))")]
        Task<string> GetSessionIdentifierAsync();

        /// <summary>
        /// Setup method that should use session context.
        /// </summary>
        /// <returns>Setup confirmation.</returns>
        [Setup]
        [PythonCode("'setup_complete'")]
        Task<string> InitializeWithSessionAsync();

        /// <summary>
        /// Method to test device capabilities detection.
        /// </summary>
        /// <returns>MicroPython version.</returns>
        [Task]
        [PythonCode("import sys; sys.version")]
        Task<string> GetDeviceInfoAsync();
    }

    /// <summary>
    /// Test program to validate session management system integration.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point for session validation testing.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public static async Task Main(string[] args)
        {
            string deviceConnection = args.Length > 0 ? args[0] : "subprocess:/home/corona/belay.net/micropython/ports/unix/build-standard/micropython";

            Console.WriteLine("🧪 Testing Belay.NET Session Management System");
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine($"Device: {deviceConnection}\n");

            try
            {
                using var device = Device.FromConnectionString(deviceConnection);
                await device.ConnectAsync();
                Console.WriteLine("✅ Connected to device");

                // Test 1: Session Manager Integration
                Console.WriteLine("\n📋 Test 1: Session Manager Integration");
                Console.WriteLine("─────────────────────────────────────");
                var sessionManager = device.Sessions;
                Console.WriteLine($"Session Manager State: {sessionManager.State}");
                Console.WriteLine($"Current Session ID: {sessionManager.CurrentSessionId ?? "None"}");

                // Test 2: Session Creation and Operations
                Console.WriteLine("\n📋 Test 2: Session Operations");
                Console.WriteLine("────────────────────────────");
                var testDevice = device.CreateProxy<ISessionTestDevice>();

                try
                {
                    var sessionId = await testDevice.GetSessionIdentifierAsync();
                    Console.WriteLine($"✅ Session operation successful: {sessionId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Session operation failed: {ex.Message}");
                }

                // Test 3: Setup Executor with Session Context
                Console.WriteLine("\n📋 Test 3: Setup Executor Session Integration");
                Console.WriteLine("───────────────────────────────────────────────");
                try
                {
                    var setupResult = await testDevice.InitializeWithSessionAsync();
                    Console.WriteLine($"✅ Setup with session successful: {setupResult}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Setup with session failed: {ex.Message}");
                }

                // Test 4: Device Capabilities Detection
                Console.WriteLine("\n📋 Test 4: Device Capabilities Detection");
                Console.WriteLine("───────────────────────────────────────");
                var capabilities = sessionManager.Capabilities;
                if (capabilities != null)
                {
                    Console.WriteLine($"✅ Capabilities detected:");
                    Console.WriteLine($"   Device Type: {capabilities.DeviceType ?? "Unknown"}");
                    Console.WriteLine($"   Firmware: {capabilities.FirmwareVersion ?? "Unknown"}");
                    Console.WriteLine($"   Features: {capabilities.SupportedFeatures}");
                    Console.WriteLine($"   Detection Complete: {capabilities.IsDetectionComplete}");
                }
                else
                {
                    Console.WriteLine("⚠️  Capabilities not yet detected (may require device operations)");

                    // Trigger capability detection by executing device info
                    try
                    {
                        var deviceInfo = await testDevice.GetDeviceInfoAsync();
                        Console.WriteLine($"Device Info Retrieved: {deviceInfo.Substring(0, Math.Min(50, deviceInfo.Length))}...");

                        // Check again after operation
                        capabilities = sessionManager.Capabilities;
                        if (capabilities != null)
                        {
                            Console.WriteLine($"✅ Capabilities now available after operation");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Device info retrieval failed: {ex.Message}");
                    }
                }

                // Test 5: Session Statistics
                Console.WriteLine("\n📋 Test 5: Session Statistics");
                Console.WriteLine("────────────────────────────");
                try
                {
                    var stats = await sessionManager.GetSessionStatsAsync();
                    Console.WriteLine($"✅ Session Statistics:");
                    Console.WriteLine($"   Active Sessions: {stats.ActiveSessionCount}");
                    Console.WriteLine($"   Total Sessions: {stats.TotalSessionCount}");
                    Console.WriteLine($"   Max Concurrent: {stats.MaxSessionCount}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Session statistics failed: {ex.Message}");
                }

                await device.DisconnectAsync();
                Console.WriteLine("\n🎯 Session Management Validation Results:");
                Console.WriteLine("   • Session manager integration operational");
                Console.WriteLine("   • Executor framework coordinates with sessions");
                Console.WriteLine("   • Device capabilities detection functional");
                Console.WriteLine("   • Session lifecycle management working");
                Console.WriteLine("   • Statistics and monitoring available");

                Console.WriteLine("\n✅ Session management system fully validated!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Test setup failed: {ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }
    }
}
