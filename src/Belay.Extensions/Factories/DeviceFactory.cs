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
    private readonly IOptions<BelayConfiguration> _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceFactory"/> class.
    /// </summary>
    /// <param name="communicatorFactory">The communicator factory.</param>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="logger">The logger for device instances.</param>
    /// <param name="configuration">The configuration options.</param>
    public DeviceFactory(
        ICommunicatorFactory communicatorFactory,
        IDeviceSessionManager sessionManager,
        ILogger<Device> logger,
        IOptions<BelayConfiguration> configuration) {
        _communicatorFactory = communicatorFactory;
        _sessionManager = sessionManager;
        _logger = logger;
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public Device CreateDevice(IDeviceCommunication communicator) {
        return new Device(communicator, _sessionManager, _logger);
    }

    /// <inheritdoc/>
    public Device CreateSerialDevice(string portName, int? baudRate = null) {
        var communicator = _communicatorFactory.CreateSerialCommunicator(portName, baudRate);
        return CreateDevice(communicator);
    }

    /// <inheritdoc/>
    public Device CreateSubprocessDevice(string executablePath, string[]? arguments = null) {
        var communicator = _communicatorFactory.CreateSubprocessCommunicator(executablePath, arguments);
        return CreateDevice(communicator);
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
        _serialLogger = serialLogger;
        _subprocessLogger = subprocessLogger;
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public IDeviceCommunication CreateSerialCommunicator(string portName, int? baudRate = null) {
        var effectiveBaudRate = baudRate ?? _configuration.Value.Communication.Serial.DefaultBaudRate;
        var timeout = _configuration.Value.Communication.Serial.ReadTimeoutMs;

        return new SerialDeviceCommunication(portName, effectiveBaudRate, timeout, _serialLogger);
    }

    /// <inheritdoc/>
    public IDeviceCommunication CreateSubprocessCommunicator(string executablePath, string[]? arguments = null) {
        return new SubprocessDeviceCommunication(executablePath, arguments ?? Array.Empty<string>(), _subprocessLogger);
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
        _sessionManager = sessionManager;
        _taskLogger = taskLogger;
        _setupLogger = setupLogger;
        _teardownLogger = teardownLogger;
        _threadLogger = threadLogger;
        _errorMapper = errorMapper;
    }

    /// <inheritdoc/>
    public Belay.Core.Execution.TaskExecutor CreateTaskExecutor(Device device) {
        return new Belay.Core.Execution.TaskExecutor(device, _sessionManager, _taskLogger, _errorMapper);
    }

    /// <inheritdoc/>
    public Belay.Core.Execution.SetupExecutor CreateSetupExecutor(Device device) {
        return new Belay.Core.Execution.SetupExecutor(device, _sessionManager, _setupLogger);
    }

    /// <inheritdoc/>
    public Belay.Core.Execution.TeardownExecutor CreateTeardownExecutor(Device device) {
        return new Belay.Core.Execution.TeardownExecutor(device, _sessionManager, _teardownLogger);
    }

    /// <inheritdoc/>
    public Belay.Core.Execution.ThreadExecutor CreateThreadExecutor(Device device) {
        return new Belay.Core.Execution.ThreadExecutor(device, _sessionManager, _threadLogger);
    }
}