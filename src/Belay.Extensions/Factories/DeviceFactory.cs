// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Extensions.Factories;

using Belay.Core;
using Belay.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Default implementation of device factory for simplified architecture.
/// </summary>
internal class DeviceFactory : IDeviceFactory {
    private readonly ILogger<SimplifiedDevice> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceFactory"/> class.
    /// </summary>
    /// <param name="logger">The logger for device instances.</param>
    public DeviceFactory(ILogger<SimplifiedDevice> logger) {
        _logger = logger;
    }

    /// <inheritdoc/>
    public SimplifiedDevice CreateDevice(DeviceConnection connection) {
        return new SimplifiedDevice(connection, _logger);
    }

    /// <inheritdoc/>
    public SimplifiedDevice CreateSerialDevice(string portName, int? baudRate = null) {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DeviceConnection>.Instance;
        var connection = new DeviceConnection(DeviceConnection.ConnectionType.Serial, portName, logger);
        return CreateDevice(connection);
    }

    /// <inheritdoc/>
    public SimplifiedDevice CreateSubprocessDevice(string executablePath, string[]? arguments = null) {
        var connectionString = arguments?.Length > 0
            ? $"{executablePath} {string.Join(" ", arguments)}"
            : executablePath;
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DeviceConnection>.Instance;
        var connection = new DeviceConnection(DeviceConnection.ConnectionType.Subprocess, connectionString, logger);
        return CreateDevice(connection);
    }
}

/// <summary>
/// Default implementation of communicator factory (deprecated - kept for compatibility).
/// </summary>
internal class CommunicatorFactory : ICommunicatorFactory {
    /// <summary>
    /// Initializes a new instance of the <see cref="CommunicatorFactory"/> class.
    /// </summary>
    public CommunicatorFactory() {
    }

    /// <inheritdoc/>
    public DeviceConnection CreateSerialCommunicator(string portName, int? baudRate = null) {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DeviceConnection>.Instance;
        return new DeviceConnection(DeviceConnection.ConnectionType.Serial, portName, logger);
    }

    /// <inheritdoc/>
    public DeviceConnection CreateSubprocessCommunicator(string executablePath, string[]? arguments = null) {
        var connectionString = arguments?.Length > 0
            ? $"{executablePath} {string.Join(" ", arguments)}"
            : executablePath;
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<DeviceConnection>.Instance;
        return new DeviceConnection(DeviceConnection.ConnectionType.Subprocess, connectionString, logger);
    }
}

/// <summary>
/// Default implementation of executor factory for simplified architecture.
/// </summary>
internal class ExecutorFactory : IExecutorFactory {
    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutorFactory"/> class.
    /// </summary>
    public ExecutorFactory() {
    }

    /// <inheritdoc/>
    public DirectExecutor GetTaskExecutor(SimplifiedDevice device) {
        // All executors are now unified into DirectExecutor
        return new DirectExecutor(device);
    }

    /// <inheritdoc/>
    public DirectExecutor GetSetupExecutor(SimplifiedDevice device) {
        // All executors are now unified into DirectExecutor
        return new DirectExecutor(device);
    }

    /// <inheritdoc/>
    public DirectExecutor GetTeardownExecutor(SimplifiedDevice device) {
        // All executors are now unified into DirectExecutor
        return new DirectExecutor(device);
    }

    /// <inheritdoc/>
    public DirectExecutor GetThreadExecutor(SimplifiedDevice device) {
        // All executors are now unified into DirectExecutor
        return new DirectExecutor(device);
    }
}
