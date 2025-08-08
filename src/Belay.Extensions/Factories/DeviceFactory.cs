// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Extensions.Factories;

using Belay.Core;
using Belay.Core.Communication;
using Belay.Core.Sessions;
using Belay.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Default implementation of device factory.
/// </summary>
internal class DeviceFactory : IDeviceFactory {
    private readonly ICommunicatorFactory _communicatorFactory;
    private readonly IDeviceSessionManager _sessionManager;
    private readonly ILogger<Device> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceFactory"/> class.
    /// </summary>
    /// <param name="communicatorFactory">The communicator factory.</param>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="logger">The logger for device instances.</param>
    public DeviceFactory(
        ICommunicatorFactory communicatorFactory,
        IDeviceSessionManager sessionManager,
        ILogger<Device> logger) {
        this._communicatorFactory = communicatorFactory;
        this._sessionManager = sessionManager;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public Device CreateDevice(IDeviceCommunication communicator) {
        return new Device(communicator, this._sessionManager, this._logger);
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
/// </summary>
internal class ExecutorFactory : IExecutorFactory {
    private readonly IDeviceSessionManager _sessionManager;
    private readonly ILogger<Belay.Core.Execution.TaskExecutor> _taskLogger;
    private readonly ILogger<Belay.Core.Execution.SetupExecutor> _setupLogger;
    private readonly ILogger<Belay.Core.Execution.TeardownExecutor> _teardownLogger;
    private readonly ILogger<Belay.Core.Execution.ThreadExecutor> _threadLogger;
    private readonly Belay.Core.Exceptions.IErrorMapper? _errorMapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutorFactory"/> class.
    /// </summary>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="taskLogger">Logger for task executors.</param>
    /// <param name="setupLogger">Logger for setup executors.</param>
    /// <param name="teardownLogger">Logger for teardown executors.</param>
    /// <param name="threadLogger">Logger for thread executors.</param>
    /// <param name="errorMapper">Optional error mapper.</param>
    public ExecutorFactory(
        IDeviceSessionManager sessionManager,
        ILogger<Belay.Core.Execution.TaskExecutor> taskLogger,
        ILogger<Belay.Core.Execution.SetupExecutor> setupLogger,
        ILogger<Belay.Core.Execution.TeardownExecutor> teardownLogger,
        ILogger<Belay.Core.Execution.ThreadExecutor> threadLogger,
        Belay.Core.Exceptions.IErrorMapper? errorMapper = null) {
        this._sessionManager = sessionManager;
        this._taskLogger = taskLogger;
        this._setupLogger = setupLogger;
        this._teardownLogger = teardownLogger;
        this._threadLogger = threadLogger;
        this._errorMapper = errorMapper;
    }

    /// <inheritdoc/>
    public Belay.Core.Execution.TaskExecutor CreateTaskExecutor(Device device) {
        return new Belay.Core.Execution.TaskExecutor(device, this._sessionManager, this._taskLogger, this._errorMapper);
    }

    /// <inheritdoc/>
    public Belay.Core.Execution.SetupExecutor CreateSetupExecutor(Device device) {
        return new Belay.Core.Execution.SetupExecutor(device, this._sessionManager, this._setupLogger);
    }

    /// <inheritdoc/>
    public Belay.Core.Execution.TeardownExecutor CreateTeardownExecutor(Device device) {
        return new Belay.Core.Execution.TeardownExecutor(device, this._sessionManager, this._teardownLogger);
    }

    /// <inheritdoc/>
    public Belay.Core.Execution.ThreadExecutor CreateThreadExecutor(Device device) {
        return new Belay.Core.Execution.ThreadExecutor(device, this._sessionManager, this._threadLogger);
    }
}
