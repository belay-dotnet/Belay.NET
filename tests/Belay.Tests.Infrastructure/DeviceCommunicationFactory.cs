// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using Belay.Core;
using Microsoft.Extensions.Logging;

namespace Belay.Tests.Infrastructure;

/// <summary>
/// Factory for creating test device communication instances.
/// </summary>
public class DeviceCommunicationFactory {
    /// <summary>
    /// Creates a mock subprocess device for testing.
    /// </summary>
    /// <param name="micropythonPath">Path to MicroPython executable.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="additionalArgs">Additional command line arguments.</param>
    /// <returns>Mock device communication instance.</returns>
    public DeviceConnection CreateSubprocessDevice(
        string micropythonPath,
        ILogger logger,
        string[]? additionalArgs = null) {
        var connectionString = additionalArgs?.Length > 0 
            ? $"{micropythonPath} {string.Join(" ", additionalArgs)}"
            : micropythonPath;
        return new DeviceConnection(DeviceConnection.ConnectionType.Subprocess, connectionString, logger as ILogger<DeviceConnection> ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DeviceConnection>.Instance);
    }
}

/// <summary>
/// Mock device connection for testing.
/// </summary>
internal class MockDeviceConnection : IDeviceConnection {
    private readonly ILogger _logger;
    private bool _isConnected = false;

    public MockDeviceConnection(ILogger logger) {
        _logger = logger;
    }

    public bool IsConnected => _isConnected;
    public string DeviceInfo => "Mock MicroPython Device";
    public string ConnectionString => "mock://test";

    public Task Connect(CancellationToken cancellationToken = default) {
        _isConnected = true;
        return Task.CompletedTask;
    }

    public Task Disconnect() {
        _isConnected = false;
        return Task.CompletedTask;
    }

    public Task<T> ExecutePython<T>(string code, CancellationToken cancellationToken = default) {
        if (!_isConnected)
            throw new DeviceException("Device not connected");

        // Simple mock responses for common test cases
        var result = code.Trim() switch {
            "1 + 2" => "3",
            "42" => "42",
            "3.14" => "3.14",
            "True" => "true",
            "'test'" => "test",
            _ => $"Mock result for: {code.Substring(0, Math.Min(50, code.Length))}"
        };

        return Task.FromResult(ResultParser.ParseResult<T>(result));
    }

    public Task<string> ExecutePython(string code, CancellationToken cancellationToken = default) {
        return ExecutePython<string>(code, cancellationToken);
    }

    public Task WriteFile(string devicePath, byte[] data, CancellationToken cancellationToken = default) {
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadFile(string devicePath, CancellationToken cancellationToken = default) {
        return Task.FromResult(new byte[] { 1, 2, 3, 4, 5 });
    }

    public Task DeleteFile(string devicePath, CancellationToken cancellationToken = default) {
        return Task.CompletedTask;
    }

    public Task<string[]> ListFiles(string devicePath = "/", CancellationToken cancellationToken = default) {
        return Task.FromResult(new[] { "main.py", "lib", "boot.py" });
    }

    public void Dispose() {
        _isConnected = false;
    }
}