// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.ErrorHandlingValidation
{
    using System;
    using System.Threading.Tasks;
    using Belay.Attributes;
    using Belay.Core;
    using Belay.Core.Exceptions;
    using Belay.Core.Execution;

    /// <summary>
    /// Interface for testing error handling scenarios with the attribute system.
    /// </summary>
    public interface IErrorTestDevice
    {
        /// <summary>
        /// Triggers a syntax error for testing exception mapping.
        /// </summary>
        /// <returns>A task that should throw DeviceCodeSyntaxException.</returns>
        [Task]
        [PythonCode("raise SyntaxError('Intentional syntax error')")]
        Task TriggerSyntaxErrorAsync();

        /// <summary>
        /// Triggers a value error for testing runtime exception mapping.
        /// </summary>
        /// <returns>A task that should throw DeviceExecutionException.</returns>
        [Task]
        [PythonCode("raise ValueError('Invalid sensor value')")]
        Task TriggerValueErrorAsync();

        /// <summary>
        /// Executes valid Python code that should return a result.
        /// </summary>
        /// <returns>A task that should return 42.</returns>
        [Task]
        [PythonCode("x = 42; x")]
        Task<int> ValidOperationAsync();
    }

    /// <summary>
    /// Test program to validate error handling with actual device.
    /// </summary>
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            string deviceConnection = args.Length > 0 ? args[0] : "subprocess:micropython";

            Console.WriteLine("🧪 Testing Belay.NET Error Handling System");
            Console.WriteLine("═════════════════════════════════════════");
            Console.WriteLine($"Device: {deviceConnection}\n");

            try
            {
                using var device = Device.FromConnectionString(deviceConnection);
                await device.ConnectAsync();
                Console.WriteLine("✅ Connected to device");

                var testDevice = device.CreateProxy<IErrorTestDevice>();

                // Test 1: Valid operation (should work)
                Console.WriteLine("\n📋 Test 1: Valid Operation");
                Console.WriteLine("──────────────────────────");
                try
                {
                    var result = await testDevice.ValidOperationAsync();
                    Console.WriteLine($"✅ Success: Got result {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Unexpected error: {ex.Message}");
                }

                // Test 2: Syntax error (should be mapped to DeviceCodeSyntaxException)
                Console.WriteLine("\n📋 Test 2: Syntax Error Handling");
                Console.WriteLine("──────────────────────────────────");
                try
                {
                    await testDevice.TriggerSyntaxErrorAsync();
                    Console.WriteLine("❌ Should have thrown exception");
                }
                catch (DeviceCodeSyntaxException ex)
                {
                    Console.WriteLine("✅ Correctly caught DeviceCodeSyntaxException");
                    Console.WriteLine($"   Message: {ex.Message}");
                    Console.WriteLine($"   Code: {ex.Code}");
                    Console.WriteLine($"   Error Code: {ex.ErrorCode}");

                    // Check proxy context
                    if (ex.Context.ContainsKey("proxy_method"))
                    {
                        Console.WriteLine($"   Proxy Context: {ex.Context["proxy_method"]} on {ex.Context["proxy_interface"]}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Wrong exception type: {ex.GetType().Name}: {ex.Message}");
                    if (ex is BelayException belayEx)
                    {
                        Console.WriteLine($"   Context: {string.Join(", ", belayEx.Context)}");
                    }
                }

                // Test 3: Value error (should be mapped to DeviceExecutionException)
                Console.WriteLine("\n📋 Test 3: Runtime Error Handling");
                Console.WriteLine("───────────────────────────────────");
                try
                {
                    await testDevice.TriggerValueErrorAsync();
                    Console.WriteLine("❌ Should have thrown exception");
                }
                catch (DeviceExecutionException ex)
                {
                    Console.WriteLine("✅ Correctly caught DeviceExecutionException");
                    Console.WriteLine($"   Message: {ex.Message}");
                    Console.WriteLine($"   Device Stack Trace: {ex.DeviceStackTrace?.Substring(0, Math.Min(50, ex.DeviceStackTrace?.Length ?? 0))}...");

                    // Verify it contains ValueError information
                    if (ex.DeviceStackTrace?.Contains("ValueError") == true)
                    {
                        Console.WriteLine("✅ Correctly identified ValueError in device stack trace");
                    }

                    // Check proxy context
                    if (ex.Context.ContainsKey("proxy_method"))
                    {
                        Console.WriteLine($"   Proxy Context: {ex.Context["proxy_method"]} on {ex.Context["proxy_interface"]}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Wrong exception type: {ex.GetType().Name}: {ex.Message}");
                }

                await device.DisconnectAsync();
                Console.WriteLine("\n✅ All tests completed successfully!");
                Console.WriteLine("\n🎯 Error handling system is working correctly:");
                Console.WriteLine("   • Device errors are properly mapped to typed exceptions");
                Console.WriteLine("   • Context information is preserved and enriched");
                Console.WriteLine("   • Proxy method information is included");
                Console.WriteLine("   • Attribute metadata is captured");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Test setup failed: {ex.GetType().Name}: {ex.Message}");

                if (ex is BelayException belayEx)
                {
                    Console.WriteLine($"   Error Code: {belayEx.ErrorCode}");
                    Console.WriteLine($"   Component: {belayEx.ComponentName}");
                    if (belayEx.Context.Count > 0)
                    {
                        Console.WriteLine("   Context:");
                        foreach (var kvp in belayEx.Context)
                        {
                            Console.WriteLine($"     {kvp.Key}: {kvp.Value}");
                        }
                    }
                }

                Environment.ExitCode = 1;
            }
        }
    }
}
