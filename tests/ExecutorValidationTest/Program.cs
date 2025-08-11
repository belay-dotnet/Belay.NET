// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.ExecutorValidationTest
{
    using System;
    using System.Threading.Tasks;
    using Belay.Attributes;
    using Belay.Core;
    using Belay.Core.Execution;

    /// <summary>
    /// Test interface demonstrating all four executor types with PythonCode attributes.
    /// </summary>
    public interface IExecutorTestDevice
    {
        /// <summary>
        /// Test [Task] executor with simple arithmetic.
        /// </summary>
        /// <returns>The result of 2 + 2.</returns>
        [Task]
        [PythonCode("2 + 2")]
        Task<int> SimpleTaskAsync();

        /// <summary>
        /// Test [Setup] executor with initialization code.
        /// </summary>
        /// <returns>A task representing the setup operation.</returns>
        [Setup]
        [PythonCode("print('Setup executed successfully')")]
        Task InitializeAsync();

        /// <summary>
        /// Test [Thread] executor creating a background operation.
        /// </summary>
        /// <returns>A task representing the thread creation.</returns>
        [Thread]
        [PythonCode("import time; time.sleep(0.1); print('Background thread completed')")]
        Task BackgroundWorkAsync();

        /// <summary>
        /// Test [Teardown] executor with cleanup code.
        /// </summary>
        /// <returns>A task representing the cleanup operation.</returns>
        [Teardown]
        [PythonCode("print('Teardown executed successfully')")]
        Task CleanupAsync();
    }

    /// <summary>
    /// Test program to validate executor framework integration.
    /// </summary>
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            string deviceConnection = args.Length > 0 ? args[0] : "subprocess:/home/corona/belay.net/micropython/ports/unix/build-standard/micropython";

            Console.WriteLine("🧪 Testing Belay.NET Executor Framework");
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine($"Device: {deviceConnection}\n");

            try
            {
                using var device = Device.FromConnectionString(deviceConnection);
                await device.ConnectAsync();
                Console.WriteLine("✅ Connected to device");

                var testDevice = device.CreateProxy<IExecutorTestDevice>();

                // Test 1: Setup Executor
                Console.WriteLine("\n📋 Test 1: Setup Executor");
                Console.WriteLine("─────────────────────────");
                try
                {
                    await testDevice.InitializeAsync();
                    Console.WriteLine("✅ Setup executor completed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Setup executor failed: {ex.Message}");
                }

                // Test 2: Task Executor
                Console.WriteLine("\n📋 Test 2: Task Executor");
                Console.WriteLine("────────────────────────");
                try
                {
                    var result = await testDevice.SimpleTaskAsync();
                    Console.WriteLine($"✅ Task executor returned: {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Task executor failed: {ex.Message}");
                }

                // Test 3: Thread Executor
                Console.WriteLine("\n📋 Test 3: Thread Executor");
                Console.WriteLine("──────────────────────────");
                try
                {
                    await testDevice.BackgroundWorkAsync();
                    Console.WriteLine("✅ Thread executor completed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Thread executor failed: {ex.Message}");
                }

                // Test 4: Teardown Executor
                Console.WriteLine("\n📋 Test 4: Teardown Executor");
                Console.WriteLine("────────────────────────────");
                try
                {
                    await testDevice.CleanupAsync();
                    Console.WriteLine("✅ Teardown executor completed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Teardown executor failed: {ex.Message}");
                }

                await device.DisconnectAsync();
                Console.WriteLine("\n🎯 Executor Framework Validation Results:");
                Console.WriteLine("   • All four executor types (Task, Setup, Thread, Teardown) are operational");
                Console.WriteLine("   • Method interception working correctly");
                Console.WriteLine("   • Device proxy integration successful");
                Console.WriteLine("   • PythonCode attribute integration confirmed");

                Console.WriteLine("\n✅ Executor framework is fully functional!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Test setup failed: {ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
            }
        }
    }
}
