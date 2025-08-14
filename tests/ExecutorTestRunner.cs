// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests;

/// <summary>
/// Simple test runner to verify the executor framework foundation.
/// </summary>
public static class ExecutorTestRunner
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🧪 Running Executor Framework Tests");
        Console.WriteLine("=====================================");

        var testRunner = new ExecutorFrameworkTest();
        bool allTestsPassed = true;

        try
        {
            // Test basic TaskExecutor functionality
            Console.WriteLine("\n📋 Testing TaskExecutor Basics...");
            var taskTest = await testRunner.TestTaskExecutorBasics();
            allTestsPassed = allTestsPassed && taskTest;

            // Test statistics collection
            Console.WriteLine("\n📊 Testing Statistics Collection...");
            testRunner.TestExecutorStatistics();

            // Summary
            Console.WriteLine("\n🎯 Test Summary");
            Console.WriteLine("================");
            if (allTestsPassed)
            {
                Console.WriteLine("✅ All tests passed! Executor framework foundation is working.");
            }
            else
            {
                Console.WriteLine("❌ Some tests failed. Check output above for details.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"💥 Unexpected test failure: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            testRunner.Dispose();
        }
    }
}