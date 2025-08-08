using System.Reflection;
using Belay.Attributes;
using Belay.Core;
using Belay.Core.Communication;

Console.WriteLine("Testing executor framework...");

// Use mock communication for basic functionality test
var mockCommunication = new MockDeviceCommunication();
var device = new Device(mockCommunication, logger: null);

// Test executor capability validation
var taskMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.TestTaskMethod))!;
Console.WriteLine($"Task executor can handle [Task] method: {device.Task.CanHandle(taskMethod)}");

var setupMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.TestSetupMethod))!;
Console.WriteLine($"Setup executor can handle [Setup] method: {device.Setup.CanHandle(setupMethod)}");

var threadMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.TestThreadMethod))!;
Console.WriteLine($"Thread executor can handle [Thread] method: {device.Thread.CanHandle(threadMethod)}");

var teardownMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.TestTeardownMethod))!;
Console.WriteLine($"Teardown executor can handle [Teardown] method: {device.Teardown.CanHandle(teardownMethod)}");

// Test method interception with complex parameters
try {
    var complexParams = new object[] { 
        42,                                    // int
        "hello world",                         // string  
        new int[] { 1, 2, 3 },                // array
        new Dictionary<string, object> {       // dictionary
            { "key1", "value1" }, 
            { "key2", 42 }
        },
        true,                                  // boolean
        3.14                                   // double
    };
    
    var complexTaskMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.ComplexTaskMethod))!;
    var result = await device.ExecuteMethodAsync<object>(complexTaskMethod, null, complexParams);
    Console.WriteLine($"✓ Complex method interception executed successfully");
} catch (Exception ex) {
    Console.WriteLine($"Complex method interception failed: {ex.Message}");
}

// Test simple method interception
try {
    var result = await device.ExecuteMethodAsync<object>(taskMethod, null, new object[] { 42 });
    Console.WriteLine($"✓ Simple method interception executed successfully");
} catch (Exception ex) {
    Console.WriteLine($"Simple method interception failed: {ex.Message}");
}

// Test method without attribute should fail
var noAttrMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.MethodWithoutAttribute))!;
try {
    await device.ExecuteMethodAsync<string>(noAttrMethod);
    Console.WriteLine("ERROR: Method without attribute should have failed!");
} catch (InvalidOperationException) {
    Console.WriteLine("✓ Method without attribute correctly rejected");
}

Console.WriteLine("Executor framework tests completed successfully!");
device.Dispose();

public class TestMethods {
    [Task(Cache = true)]
    public static string TestTaskMethod(int value) {
        return $"Task method called with {value}";
    }

    [Setup]
    public static void TestSetupMethod() {
    }

    [Thread(Name = "test_thread")]
    public static void TestThreadMethod() {
    }

    [Teardown]
    public static void TestTeardownMethod() {
    }

    [Task]
    public static string ComplexTaskMethod(int number, string text, int[] array, Dictionary<string, object> dict, bool flag, double value) {
        return $"Complex method called with {number}, {text}, array length {array.Length}";
    }

    public static string MethodWithoutAttribute() {
        return "No attribute method";
    }
}

public class MockDeviceCommunication : IDeviceCommunication {
    public DeviceConnectionState State { get; private set; } = DeviceConnectionState.Connected;
    
    public event EventHandler<DeviceOutputEventArgs>? OutputReceived { add { } remove { } }
    public event EventHandler<DeviceStateChangeEventArgs>? StateChanged { add { } remove { } }

    public Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default) {
        Console.WriteLine($"Mock executing: {code}");
        return Task.FromResult(default(T)!);
    }

    public Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default) {
        Console.WriteLine($"Mock executing: {code}");
        return Task.FromResult("mock_result");
    }

    public Task PutFileAsync(string localPath, string remotePath, CancellationToken cancellationToken = default) {
        Console.WriteLine($"Mock put file: {localPath} -> {remotePath}");
        return Task.CompletedTask;
    }

    public Task<byte[]> GetFileAsync(string remotePath, CancellationToken cancellationToken = default) {
        Console.WriteLine($"Mock get file: {remotePath}");
        return Task.FromResult(System.Text.Encoding.UTF8.GetBytes("mock_file_content"));
    }

    public void Dispose() { }
}
