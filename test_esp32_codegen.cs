// Test Python code generation without device execution
using System.Reflection;
using Belay.Attributes;
using Belay.Core;
using Belay.Core.Communication;
using Belay.Core.Execution;
using Belay.Core.Sessions;
using Microsoft.Extensions.Logging;

Console.WriteLine("Testing Python code generation (no device execution)...");

try {
    // Create a mock session manager and logger for testing
    var mockSessionManager = new MockDeviceSessionManager();
    var logger = new TestLogger();
    
    // Create a TaskExecutor directly to test code generation
    var mockCommunication = new MockDeviceCommunication();
    var device = new Device(mockCommunication, logger: null);
    var executor = new TaskExecutor(device, mockSessionManager, logger);

    // Test methods from our test class
    var methods = typeof(TestMethods).GetMethods(BindingFlags.Public | BindingFlags.Static);
    
    foreach (var method in methods) {
        if (method.HasAttribute<TaskAttribute>()) {
            Console.WriteLine($"\n=== Testing method: {method.Name} ===");
            
            // Test CanHandle
            bool canHandle = executor.CanHandle(method);
            Console.WriteLine($"Can handle: {canHandle}");
            
            if (canHandle) {
                try {
                    // Use reflection to access the protected GeneratePythonMethodCall method
                    var generateMethod = typeof(BaseExecutor).GetMethod("GeneratePythonMethodCall", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    object?[] parameters = method.Name switch {
                        nameof(TestMethods.GetTemperaturePythonCode) => new object?[] { 1 },
                        nameof(TestMethods.SimpleTaskMethod) => new object?[] { 42 },
                        nameof(TestMethods.ComplexParameterMethod) => new object?[] {
                            "test",
                            new int[] { 1, 2, 3 },
                            new Dictionary<string, object> { { "key", "value" } },
                            true
                        },
                        _ => null
                    };
                    
                    var pythonCode = (string)generateMethod!.Invoke(executor, new object?[] { method, null, parameters })!;
                    Console.WriteLine($"Generated Python code: {pythonCode}");
                    
                    // Test parameter conversion
                    if (parameters != null) {
                        var paramListMethod = typeof(BaseExecutor).GetMethod("GenerateParameterList", 
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        var paramList = (string)paramListMethod!.Invoke(executor, new object[] { parameters })!;
                        Console.WriteLine($"Parameter list: [{paramList}]");
                    }
                    
                } catch (Exception ex) {
                    Console.WriteLine($"Code generation failed: {ex.Message}");
                    if (ex.InnerException != null) {
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    }
                }
            }
        }
    }

    Console.WriteLine("\n✓ Python code generation testing completed!");

} catch (Exception ex) {
    Console.WriteLine($"Test failed: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

public class TestMethods {
    [Task(Cache = true)]
    public static string GetTemperaturePythonCode(int sensorId) {
        // This method returns Python code to execute (Strategy 2)
        return $"print('Reading temperature from sensor {sensorId}'); 25.5";
    }

    [Task]
    public static float SimpleTaskMethod(int value) {
        // This method would be deployed to device (Strategy 3)  
        return value * 2.5f;
    }

    [Task]
    public static string ComplexParameterMethod(string name, int[] values, Dictionary<string, object> config, bool enabled) {
        // Test complex parameter marshaling
        return $"Processed {name} with {values.Length} values";
    }
}

public class MockDeviceCommunication : IDeviceCommunication {
    public DeviceConnectionState State { get; private set; } = DeviceConnectionState.Connected;
    
    public event EventHandler<DeviceOutputEventArgs>? OutputReceived { add { } remove { } }
    public event EventHandler<DeviceStateChangeEventArgs>? StateChanged { add { } remove { } }

    public Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default) {
        return Task.FromResult(default(T)!);
    }

    public Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default) {
        return Task.FromResult("mock_result");
    }

    public Task PutFileAsync(string localPath, string remotePath, CancellationToken cancellationToken = default) {
        return Task.CompletedTask;
    }

    public Task<byte[]> GetFileAsync(string remotePath, CancellationToken cancellationToken = default) {
        return Task.FromResult(System.Text.Encoding.UTF8.GetBytes("mock_content"));
    }

    public void Dispose() { }
}

public class MockDeviceSessionManager : IDeviceSessionManager {
    public Task<T> ExecuteInSessionAsync<T>(IDeviceCommunication communication, Func<IDeviceSession, Task<T>> operation, CancellationToken cancellationToken = default) {
        var mockSession = new MockDeviceSession();
        return operation(mockSession);
    }

    public Task ExecuteInSessionAsync(IDeviceCommunication communication, Func<IDeviceSession, Task> operation, CancellationToken cancellationToken = default) {
        var mockSession = new MockDeviceSession();
        return operation(mockSession);
    }

    public ValueTask DisposeAsync() {
        return ValueTask.CompletedTask;
    }
}

public class MockDeviceSession : IDeviceSession {
    public string SessionId => "mock-session";
    public DeviceSessionState State => DeviceSessionState.Active;
    public DateTime CreatedAt => DateTime.UtcNow;
    public TimeSpan Duration => TimeSpan.Zero;

    public ValueTask DisposeAsync() {
        return ValueTask.CompletedTask;
    }
}

public class TestLogger : ILogger<TaskExecutor> {
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel) {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
    }
}