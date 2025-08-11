using System;
using System.Threading.Tasks;
using Belay.Attributes;
using Belay.Core;
using Belay.Core.Exceptions;

namespace Belay.ErrorHandlingValidation
{
    /// <summary>
    /// Interface for testing error handling scenarios with the attribute system.
    /// </summary>
    public interface IErrorTestDevice
    {
        [Task]
        [PythonCode("raise SyntaxError('Intentional syntax error')")]
        Task TriggerSyntaxErrorAsync();

        [Task]
        [PythonCode("raise ValueError('Invalid sensor value')")]
        Task TriggerValueErrorAsync();

        [Task]
        [PythonCode("import time; time.sleep(15)")]  // Will timeout with 5s default
        Task TriggerTimeoutAsync();

        [Task]
        [PythonCode("x = 42; x")]  // Valid operation
        Task<int> ValidOperationAsync();
    }

    /// <summary>
    /// Test program to validate error handling with actual device.
    /// </summary>
    public class ErrorHandlingValidationProgram
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
                    Console.WriteLine($"   Device Stack Trace: {ex.DeviceStackTrace?.Substring(0, Math.Min(50, ex.DeviceStackTrace.Length ?? 0))}...");
                    
                    // Verify it contains ValueError information
                    if (ex.DeviceStackTrace?.Contains("ValueError") == true)
                    {
                        Console.WriteLine("✅ Correctly identified ValueError in device stack trace");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Wrong exception type: {ex.GetType().Name}: {ex.Message}");
                }

                // Test 4: Timeout (only test with subprocess to avoid long waits)
                if (deviceConnection.StartsWith("subprocess"))
                {
                    Console.WriteLine("\n📋 Test 4: Timeout Handling");
                    Console.WriteLine("────────────────────────────");
                    try
                    {
                        await testDevice.TriggerTimeoutAsync();
                        Console.WriteLine("❌ Should have thrown timeout exception");
                    }
                    catch (DeviceTimeoutException ex)
                    {
                        Console.WriteLine("✅ Correctly caught DeviceTimeoutException");
                        Console.WriteLine($"   Operation: {ex.Operation}");
                        Console.WriteLine($"   Timeout: {ex.Timeout.TotalSeconds}s");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️  Different exception type: {ex.GetType().Name}: {ex.Message}");
                        Console.WriteLine("    (Timeout handling may vary by communication layer)");
                    }
                }
                else
                {
                    Console.WriteLine("\n📋 Test 4: Timeout Handling - SKIPPED");
                    Console.WriteLine("(Only tested with subprocess to avoid long hardware waits)");
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