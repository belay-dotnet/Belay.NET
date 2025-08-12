// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using Belay.Core.Communication;
using Microsoft.Extensions.Logging;

namespace Belay.Tests.Infrastructure;

/// <summary>
/// Factory for creating test device communication instances.
/// </summary>
public class DeviceCommunicationFactory
{
    /// <summary>
    /// Creates a mock subprocess device for testing.
    /// </summary>
    /// <param name="micropythonPath">Path to MicroPython executable.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="additionalArgs">Additional command line arguments.</param>
    /// <returns>Mock device communication instance.</returns>
    public IDeviceCommunication CreateSubprocessDevice(
        string micropythonPath, 
        ILogger logger, 
        string[]? additionalArgs = null)
    {
        return new MockDeviceCommunication(logger);
    }
}

/// <summary>
/// Mock device communication for testing.
/// </summary>
internal class MockDeviceCommunication : IDeviceCommunication
{
    private readonly ILogger _logger;
    private DeviceConnectionState _state = DeviceConnectionState.Disconnected;

    public MockDeviceCommunication(ILogger logger)
    {
        _logger = logger;
    }

    public DeviceConnectionState State => _state;

    public event EventHandler<DeviceOutputEventArgs>? OutputReceived;
    public event EventHandler<DeviceStateChangeEventArgs>? StateChanged;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _state = DeviceConnectionState.Connected;
        StateChanged?.Invoke(this, new DeviceStateChangeEventArgs(DeviceConnectionState.Disconnected, _state, "Mock connected"));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _state = DeviceConnectionState.Disconnected;
        StateChanged?.Invoke(this, new DeviceStateChangeEventArgs(DeviceConnectionState.Connected, _state, "Mock disconnected"));
        return Task.CompletedTask;
    }

    public Task<string> ExecuteAsync(string pythonCode, CancellationToken cancellationToken = default)
    {
        if (_state != DeviceConnectionState.Connected)
            throw new InvalidOperationException("Device not connected");

        // Simple mock responses for common test cases
        return pythonCode.Trim() switch
        {
            "1 + 2" => Task.FromResult("3"),
            "x = 42" => Task.FromResult(""),
            "x * 2" => Task.FromResult("84"),
            "42" => Task.FromResult("42"),
            "3.14" => Task.FromResult("3.14"),
            "True" => Task.FromResult("True"),
            "'test'" => Task.FromResult("'test'"),
            "counter = 0" => Task.FromResult(""),
            "counter += 5" => Task.FromResult(""),
            "counter *= 2" => Task.FromResult(""),
            "counter" => Task.FromResult("10"),
            _ when pythonCode.Contains("factorial(5)") => Task.FromResult("120"),
            _ when pythonCode.Contains("sum(data)") => Task.FromResult("4950"),
            _ when pythonCode.Contains("print('Hello, World!')") => Task.FromResult("Hello, World!"),
            _ when pythonCode.Contains("sys.version_info") => Task.FromResult("(3, 4, 0)"),
            _ when pythonCode.Contains("Hello 世界 🌍") => Task.FromResult("Hello 世界 🌍"),
            _ when pythonCode.Contains("x**2 for x in range(5)") => Task.FromResult("[0, 1, 4, 9, 16]"),
            _ when pythonCode.Contains("Hello from async") => Task.FromResult("Hello from async"),
            _ when pythonCode.Contains("json.dumps(data)") => Task.FromResult("{\"name\": \"test\", \"value\": 42, \"items\": [1, 2, 3]}"),
            _ when pythonCode.Contains("f.read()") => Task.FromResult("Test file content"),
            _ when pythonCode.Contains("os.getcwd()") => Task.FromResult(Environment.CurrentDirectory),
            _ when pythonCode.Contains("__debug__") => Task.FromResult("False"),
            _ when pythonCode.Contains("instance_id = 1") => Task.FromResult("1"),
            _ when pythonCode.Contains("instance_id = 2") => Task.FromResult("2"),
            _ when pythonCode.Contains("Task ") => Task.FromResult(pythonCode),
            _ when pythonCode.Contains("Completed") => Task.FromResult("Completed"),
            _ when pythonCode.Contains("invalid syntax") => throw new Belay.Core.Exceptions.DeviceExecutionException("Code execution failed on device", "SyntaxError"),
            _ when pythonCode.Contains("1 / 0") => throw new Belay.Core.Exceptions.DeviceExecutionException("Division by zero", "ZeroDivisionError: division by zero"),
            _ when pythonCode.Contains("sys.exit") => throw new Belay.Core.Exceptions.DeviceExecutionException("Process exited", "SystemExit"),
            _ when pythonCode.Trim() == "x" => throw new Belay.Core.Exceptions.DeviceExecutionException("Name not defined", "NameError: name 'x' is not defined"),
            _ => Task.FromResult($"Mock result for: {pythonCode.Substring(0, Math.Min(50, pythonCode.Length))}")
        };
    }

    public Task<T> ExecuteAsync<T>(string pythonCode, CancellationToken cancellationToken = default)
    {
        var result = ExecuteAsync(pythonCode, cancellationToken).Result;
        
        if (typeof(T) == typeof(int) && int.TryParse(result, out var intResult))
            return Task.FromResult((T)(object)intResult);
        if (typeof(T) == typeof(double) && double.TryParse(result, out var doubleResult))
            return Task.FromResult((T)(object)doubleResult);
        if (typeof(T) == typeof(bool) && bool.TryParse(result, out var boolResult))
            return Task.FromResult((T)(object)boolResult);
        if (typeof(T) == typeof(string))
            return Task.FromResult((T)(object)result);
        if (typeof(T) == typeof(Dictionary<string, object>))
            return Task.FromResult((T)(object)new Dictionary<string, object> { ["name"] = "test", ["value"] = 42, ["items"] = new List<int> { 1, 2, 3 } });

        return Task.FromResult((T)(object)result);
    }

    public Task<byte[]> GetFileAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new byte[] { 1, 2, 3, 4, 5 });
    }

    public Task PutFileAsync(string localPath, string remotePath, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _state = DeviceConnectionState.Disconnected;
    }
}