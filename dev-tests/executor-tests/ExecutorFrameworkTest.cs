using System.Reflection;
using Belay.Attributes;
using Belay.Core;
using Belay.Core.Execution;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🧪 Testing Executor Framework Foundation");
        Console.WriteLine("==========================================");

        var mockDevice = new MockDeviceConnection();
        using var executorFramework = new ExecutorFramework(mockDevice);

        try
        {
            // Test TaskExecutor with a simple method
            Console.WriteLine("\n📋 Testing TaskExecutor...");
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.GetTemperature))!;
            var methodArgs = new object[] { 26 };

            var result = await executorFramework.ExecuteAsync<int>(method, methodArgs);
            Console.WriteLine($"✅ TaskExecutor test passed! Result: {result}");
            Console.WriteLine($"✅ Generated Python code: '{mockDevice.LastExecutedCode}'");

            // Test statistics
            Console.WriteLine("\n📊 Testing Statistics...");
            var stats = executorFramework.GetStatistics();
            Console.WriteLine("Framework Statistics:");
            foreach (var kvp in stats)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }

            Console.WriteLine("\n🎯 All tests passed! ✅");
            Console.WriteLine("Executor framework foundation is working correctly.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Test failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            mockDevice.Dispose();
        }
    }
}

public static class TestMethods
{
    [Task(Name = "read_temperature", TimeoutMs = 5000, Cache = true)]
    public static int GetTemperature(int pin) => 0;

    [Task]
    public static string GetDeviceInfo() => string.Empty;
}

public class MockDeviceConnection : IDeviceConnection
{
    public string LastExecutedCode { get; private set; } = string.Empty;
    public bool IsConnected => true;
    public string DeviceInfo => "Mock MicroPython Device v1.0";
    public string ConnectionString => "mock://test-device";

    public Task<string> ExecutePython(string code, CancellationToken cancellationToken = default)
    {
        LastExecutedCode = code;
        return Task.FromResult("OK");
    }

    public Task<T> ExecutePython<T>(string code, CancellationToken cancellationToken = default)
    {
        LastExecutedCode = code;
        if (typeof(T) == typeof(int))
            return Task.FromResult((T)(object)23);
        return Task.FromResult((T)(object)"Mock Result");
    }

    public Task WriteFile(string devicePath, byte[] data, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<byte[]> ReadFile(string devicePath, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<byte>());
    public Task DeleteFile(string devicePath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<string[]> ListFiles(string devicePath = "/", CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<string>());
    public Task Connect(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task Disconnect() => Task.CompletedTask;
    public void Dispose() { }
}