// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Extensions.Factories;

using Belay.Core;
using Belay.Core.Communication;
using Belay.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Default implementation of device factory.
/// </summary>
internal class DeviceFactory : IDeviceFactory {
    private readonly ICommunicatorFactory _communicatorFactory;
    private readonly ILogger<Device> _logger;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceFactory"/> class.
    /// </summary>
    /// <param name="communicatorFactory">The communicator factory.</param>
    /// <param name="logger">The logger for device instances.</param>
    /// <param name="loggerFactory">The logger factory for executor loggers.</param>
    public DeviceFactory(
        ICommunicatorFactory communicatorFactory,
        ILogger<Device> logger,
        ILoggerFactory loggerFactory) {
        this._communicatorFactory = communicatorFactory;
        this._logger = logger;
        this._loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public Device CreateDevice(IDeviceCommunication communicator) {
        return new Device(communicator, this._logger, this._loggerFactory);
    }

    /// <inheritdoc/>
    public Device CreateSerialDevice(string portName, int? baudRate = null) {
        var communicator = this._communicatorFactory.CreateSerialCommunicator(portName, baudRate);
        return this.CreateDevice(communicator);
    }

    /// <inheritdoc/>
    public Device CreateSubprocessDevice(string executablePath, string[]? arguments = null) {
        var communicator = this._communicatorFactory.CreateSubprocessCommunicator(executablePath, arguments);
        return this.CreateDevice(communicator);
    }
}

/// <summary>
/// Default implementation of communicator factory.
/// </summary>
internal class CommunicatorFactory : ICommunicatorFactory {
    private readonly ILogger<SerialDeviceCommunication> _serialLogger;
    private readonly ILogger<SubprocessDeviceCommunication> _subprocessLogger;
    private readonly IOptions<BelayConfiguration> _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommunicatorFactory"/> class.
    /// </summary>
    /// <param name="serialLogger">Logger for serial communicators.</param>
    /// <param name="subprocessLogger">Logger for subprocess communicators.</param>
    /// <param name="configuration">The configuration options.</param>
    public CommunicatorFactory(
        ILogger<SerialDeviceCommunication> serialLogger,
        ILogger<SubprocessDeviceCommunication> subprocessLogger,
        IOptions<BelayConfiguration> configuration) {
        this._serialLogger = serialLogger;
        this._subprocessLogger = subprocessLogger;
        this._configuration = configuration;
    }

    /// <inheritdoc/>
    public IDeviceCommunication CreateSerialCommunicator(string portName, int? baudRate = null) {
        var effectiveBaudRate = baudRate ?? this._configuration.Value.Communication.Serial.DefaultBaudRate;
        var timeout = this._configuration.Value.Communication.Serial.ReadTimeoutMs;

        return new SerialDeviceCommunication(portName, effectiveBaudRate, timeout, this._serialLogger);
    }

    /// <inheritdoc/>
    public IDeviceCommunication CreateSubprocessCommunicator(string executablePath, string[]? arguments = null) {
        return new SubprocessDeviceCommunication(executablePath, arguments ?? Array.Empty<string>(), this._subprocessLogger);
    }
}

/// <summary>
/// Default implementation of executor factory.
/// Provides access to simplified executors from the Device instance.
/// </summary>
internal class ExecutorFactory : IExecutorFactory {
    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutorFactory"/> class.
    /// </summary>
    public ExecutorFactory() {
    }

    /// <inheritdoc/>
    public Belay.Core.Execution.IExecutor GetTaskExecutor(Device device) {
        // Return the simplified task executor from the device instance
        return device.Task;
    }

    /// <inheritdoc/>
    public Belay.Core.Execution.IExecutor GetSetupExecutor(Device device) {
        // Return the simplified setup executor from the device instance
        return device.Setup;
    }

    /// <inheritdoc/>
    public Belay.Core.Execution.IExecutor GetTeardownExecutor(Device device) {
        // Return the simplified teardown executor from the device instance
        return device.Teardown;
    }

    /// <inheritdoc/>
    public Belay.Core.Execution.IExecutor GetThreadExecutor(Device device) {
        // Return the simplified thread executor from the device instance
        return device.Thread;
    }
}
