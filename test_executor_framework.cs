// Simple test to validate executor framework functionality
using System;
using System.Reflection;
using System.Threading.Tasks;
using Belay.Attributes;
using Belay.Core;
using Belay.Core.Communication;

// Test with subprocess communication (no external hardware needed)
var subprocess = new SubprocessDeviceCommunication("./micropython/ports/unix/build-standard/micropython");
await subprocess.StartAsync();

var device = new Device(subprocess);

Console.WriteLine("Testing executor framework...");

// Test Task executor
var taskMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.TestTaskMethod));
Console.WriteLine($"Task executor can handle [Task] method: {device.Task.CanHandle(taskMethod)}");

// Test Setup executor  
var setupMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.TestSetupMethod));
Console.WriteLine($"Setup executor can handle [Setup] method: {device.Setup.CanHandle(setupMethod)}");

// Test Thread executor
var threadMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.TestThreadMethod));
Console.WriteLine($"Thread executor can handle [Thread] method: {device.Thread.CanHandle(threadMethod)}");

// Test Teardown executor
var teardownMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.TestTeardownMethod));
Console.WriteLine($"Teardown executor can handle [Teardown] method: {device.Teardown.CanHandle(teardownMethod)}");

// Test method interception
try {
    var result = await device.ExecuteMethodAsync<string>(taskMethod, null, new object[] { 42 });
    Console.WriteLine($"Method interception executed successfully. Result type: {result?.GetType()?.Name ?? "null"}");
} catch (Exception ex) {
    Console.WriteLine($"Method interception test failed: {ex.Message}");
}

// Test method without attribute should fail
var noAttrMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.MethodWithoutAttribute));
try {
    await device.ExecuteMethodAsync<string>(noAttrMethod);
    Console.WriteLine("ERROR: Method without attribute should have failed!");
} catch (InvalidOperationException) {
    Console.WriteLine("✓ Method without attribute correctly rejected");
}

Console.WriteLine("Executor framework tests completed!");

device.Dispose();

public class TestMethods {
    [Task(Cache = true)]
    public static string TestTaskMethod(int value) {
        return $"Task method called with {value}";
    }

    [Setup]
    public static void TestSetupMethod() {
        Console.WriteLine("Setup method called");
    }

    [Thread(Name = "test_thread")]
    public static void TestThreadMethod() {
        Console.WriteLine("Thread method called");
    }

    [Teardown]
    public static void TestTeardownMethod() {
        Console.WriteLine("Teardown method called");
    }

    public static string MethodWithoutAttribute() {
        return "No attribute method";
    }
}