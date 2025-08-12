// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

using Belay.Core.Caching;
using Belay.Core.Communication;
using Belay.Core.Discovery;
using Belay.Core.Execution;
using Microsoft.Extensions.Logging;

/// <summary>
/// Main entry point for MicroPython device communication.
/// Provides a high-level interface for connecting to and interacting with MicroPython devices.
/// </summary>
/// <remarks>
/// <para>
/// This refactored Device class eliminates complex session management in favor of
/// simple DeviceState tracking, providing direct device communication with improved
/// performance and reliability.
/// </para>
/// <para>
/// Key improvements over the session-based approach:
/// <list type="bullet">
/// <item><description>Eliminates race conditions from concurrent session creation</description></item>
/// <item><description>Reduces initialization time from ~2000ms to &lt;100ms</description></item>
/// <item><description>Provides direct executor access without session indirection</description></item>
/// <item><description>Aligns with single-threaded MicroPython device reality</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Basic Usage</strong></para>
/// <code>
/// using var device = Device.FromConnectionString("subprocess:micropython");
/// await device.ConnectAsync();
/// 
/// // Execute code directly
/// var result = await device.ExecuteAsync&lt;int&gt;("2 + 3");
/// 
/// // Check device capabilities
/// Console.WriteLine($"Platform: {device.State.Capabilities?.Platform}");
/// Console.WriteLine($"Features: {device.State.Capabilities?.SupportedFeatures}");
/// </code>
/// </example>
public class Device : IDisposable {
    private readonly IDeviceCommunication communication;
    private readonly ILogger<Device> logger;
    private readonly IMethodDeploymentCache methodCache;
    private readonly IExecutionContextService executionContextService;
    private readonly Lazy<SimplifiedTaskExecutor> taskExecutor;
    private readonly Lazy<SimplifiedSetupExecutor> setupExecutor;
    private readonly Lazy<SimplifiedThreadExecutor> threadExecutor;
    private readonly Lazy<SimplifiedTeardownExecutor> teardownExecutor;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Device"/> class.
    /// </summary>
    /// <param name="communication">The device communication implementation.</param>
    /// <param name="logger">Optional logger for device operations.</param>
    public Device(IDeviceCommunication communication, ILogger<Device>? logger = null)
        : this(communication, logger, null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Device"/> class.
    /// </summary>
    /// <param name="communication">The device communication implementation.</param>
    /// <param name="logger">Optional logger for device operations.</param>
    /// <param name="loggerFactory">Optional logger factory for executor logging.</param>
    public Device(IDeviceCommunication communication, ILogger<Device>? logger = null, ILoggerFactory? loggerFactory = null)
        : this(communication, logger, loggerFactory, null, null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Device"/> class with dependency injection support.
    /// </summary>
    /// <param name="communication">The device communication implementation.</param>
    /// <param name="logger">Logger for device operations.</param>
    /// <param name="loggerFactory">Optional logger factory for executor logging.</param>
    /// <param name="methodCache">Optional method deployment cache for performance optimization.</param>
    /// <param name="executionContextService">Optional execution context service for secure method detection.</param>
    public Device(IDeviceCommunication communication, ILogger<Device>? logger = null, ILoggerFactory? loggerFactory = null, IMethodDeploymentCache? methodCache = null, IExecutionContextService? executionContextService = null) {
        this.communication = communication ?? throw new ArgumentNullException(nameof(communication));
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<Device>.Instance;

        // Forward events from communication layer
        this.communication.OutputReceived += (sender, args) => this.OutputReceived?.Invoke(this, args);
        this.communication.StateChanged += (sender, args) => {
            // Update device state when connection state changes
            this.State.ConnectionState = args.NewState;
            this.StateChanged?.Invoke(this, args);
        };

        var executorLoggerFactory = loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

        // Use injected cache or create a default one
        this.methodCache = methodCache ?? new MethodDeploymentCache(
            new MethodCacheConfiguration(),
            executorLoggerFactory.CreateLogger<MethodDeploymentCache>());

        // Use injected execution context service or create a default one
        this.executionContextService = executionContextService ?? new ExecutionContextService();

        // Initialize device state with current connection state
        this.State.ConnectionState = this.communication.State;

        // Initialize simplified executors without session management dependencies
        var transactionManager = new Belay.Core.Transactions.TransactionManager(executorLoggerFactory.CreateLogger<Belay.Core.Transactions.TransactionManager>());
        this.taskExecutor = new Lazy<SimplifiedTaskExecutor>(() => new SimplifiedTaskExecutor(this, executorLoggerFactory.CreateLogger<SimplifiedTaskExecutor>(), cache: this.methodCache, executionContextService: this.executionContextService, transactionManager: transactionManager));
        this.setupExecutor = new Lazy<SimplifiedSetupExecutor>(() => new SimplifiedSetupExecutor(this, executorLoggerFactory.CreateLogger<SimplifiedSetupExecutor>(), executionContextService: this.executionContextService));
        this.threadExecutor = new Lazy<SimplifiedThreadExecutor>(() => new SimplifiedThreadExecutor(this, executorLoggerFactory.CreateLogger<SimplifiedThreadExecutor>(), executionContextService: this.executionContextService));
        this.teardownExecutor = new Lazy<SimplifiedTeardownExecutor>(() => new SimplifiedTeardownExecutor(this, executorLoggerFactory.CreateLogger<SimplifiedTeardownExecutor>(), executionContextService: this.executionContextService));
    }

    /// <summary>
    /// Event raised when output is received from the device.
    /// </summary>
    public event EventHandler<DeviceOutputEventArgs>? OutputReceived;

    /// <summary>
    /// Event raised when device connection state changes.
    /// </summary>
    public event EventHandler<DeviceStateChangeEventArgs>? StateChanged;

    /// <summary>
    /// Gets the current connection state of the device.
    /// </summary>
    /// <remarks>
    /// This property is maintained for backward compatibility. Use the State property
    /// for accessing the full device state including capabilities and operation tracking.
    /// </remarks>
    public DeviceConnectionState ConnectionState => this.communication.State;

    /// <summary>
    /// Gets the device state including capabilities, current operations, and connection status.
    /// </summary>
    /// <value>
    /// The current device state containing capability information, operation tracking,
    /// and connection status. This replaces the complex session management system.
    /// </value>
    /// <example>
    /// <code>
    /// // Check if device supports GPIO
    /// if (device.State.Capabilities?.SupportsFeature(SimpleDeviceFeatureSet.GPIO) == true)
    /// {
    ///     // Use GPIO functionality
    /// }
    /// 
    /// // Monitor current operation
    /// Console.WriteLine($"Current operation: {device.State.CurrentOperation}");
    /// Console.WriteLine($"Last operation: {device.State.LastOperationTime}");
    /// </code>
    /// </example>
    public DeviceState State { get; } = new DeviceState();

    /// <summary>
    /// Gets the communication interface for this device.
    /// Internal use for executors and session management.
    /// </summary>
    internal IDeviceCommunication Communication => this.communication;

    /// <summary>
    /// Gets the simplified task executor for methods decorated with [Task] attribute.
    /// </summary>
    /// <remarks>
    /// This simplified executor provides direct device communication without session management
    /// overhead while maintaining all [Task] attribute policies including caching, timeouts,
    /// and exclusive execution.
    /// </remarks>
    public SimplifiedTaskExecutor Task => this.taskExecutor.Value;

    /// <summary>
    /// Gets the simplified setup executor for methods decorated with [Setup] attribute.
    /// </summary>
    /// <remarks>
    /// This simplified executor provides direct device communication for setup operations
    /// with enhanced initialization support and capability-aware optimizations.
    /// </remarks>
    public SimplifiedSetupExecutor Setup => this.setupExecutor.Value;

    /// <summary>
    /// Gets the simplified thread executor for methods decorated with [Thread] attribute.
    /// </summary>
    /// <remarks>
    /// This simplified executor provides direct device communication for thread operations
    /// with capability validation and thread management without session overhead.
    /// </remarks>
    public SimplifiedThreadExecutor Thread => this.threadExecutor.Value;

    /// <summary>
    /// Gets the simplified teardown executor for methods decorated with [Teardown] attribute.
    /// </summary>
    /// <remarks>
    /// This simplified executor provides direct device communication for teardown operations
    /// with graceful error handling and emergency cleanup capabilities.
    /// </remarks>
    public SimplifiedTeardownExecutor Teardown => this.teardownExecutor.Value;


    /// <summary>
    /// Connect to the MicroPython device and perform capability detection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// This method connects to the device and immediately performs capability detection
    /// using the optimized batched approach, completing in &lt;100ms compared to the
    /// previous ~2000ms sequential detection.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// using var device = Device.FromConnectionString("subprocess:micropython");
    /// await device.ConnectAsync();
    /// 
    /// // Capabilities are now available
    /// Console.WriteLine($"Platform: {device.State.Capabilities?.Platform}");
    /// Console.WriteLine($"Memory: {device.State.Capabilities?.AvailableMemory} bytes");
    /// </code>
    /// </example>
    public async Task ConnectAsync(CancellationToken cancellationToken = default) {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(Device));
        }

        this.logger.LogDebug("Connecting to device");
        this.State.SetCurrentOperation("Connect");

        try {
            if (this.communication is SerialDeviceCommunication serial) {
                await serial.ConnectAsync(cancellationToken);
            }
            else if (this.communication is SubprocessDeviceCommunication subprocess) {
                await subprocess.StartAsync(cancellationToken);
            }
            else {
                throw new NotSupportedException($"Communication type {this.communication.GetType().Name} is not supported");
            }

            // Update state after successful connection
            this.State.ConnectionState = this.communication.State;

            this.logger.LogInformation("Successfully connected to device, performing capability detection");

            // Perform fast capability detection using batched approach
            try {
                this.State.SetCurrentOperation("CapabilityDetection");
                this.State.Capabilities = await SimplifiedCapabilityDetection.DetectAsync(
                    this.communication, this.logger, cancellationToken);
                this.logger.LogDebug("Capability detection completed: {Capabilities}", this.State.Capabilities);
            }
            catch (Exception ex) {
                this.logger.LogWarning(ex, "Capability detection failed, device will work with limited functionality");
                // Create minimal capabilities to indicate detection was attempted
                this.State.Capabilities = new SimpleDeviceCapabilities { DetectionComplete = true };
            }

            this.logger.LogInformation("Device connection and initialization completed successfully");
        }
        finally {
            this.State.CompleteOperation();
        }
    }

    /// <summary>
    /// Disconnect from the MicroPython device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default) {
        if (this.disposed) {
            return;
        }

        this.logger.LogDebug("Disconnecting from device");

        // Execute teardown cleanup before disconnecting
        try {
            // Execute emergency cleanup for simplified teardown
            await this.ExecuteAsync("# Teardown cleanup\nimport gc; gc.collect()", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) {
            this.logger.LogError(ex, "Error executing teardown cleanup during disconnect");
        }

        if (this.communication is SerialDeviceCommunication serial) {
            await serial.DisconnectAsync(cancellationToken);
        }
        else if (this.communication is SubprocessDeviceCommunication subprocess) {
            await subprocess.StopAsync(cancellationToken);
        }

        this.logger.LogInformation("Disconnected from device");
    }

    /// <summary>
    /// Execute Python code on the device and return the result.
    /// If called from a method with Belay attributes, applies attribute-specific policies.
    /// </summary>
    /// <param name="code">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result as a string.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the device has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown when code is null or empty.</exception>
    /// <exception cref="DeviceConnectionException">Thrown when the device is not connected.</exception>
    /// <exception cref="DeviceExecutionException">Thrown when code execution fails on the device.</exception>
    /// <example>
    /// <code>
    /// var device = Device.FromConnectionString("serial:COM3");
    /// await device.ConnectAsync();
    /// string result = await device.ExecuteAsync("print('Hello World')");
    /// // result contains the output from the device
    /// </code>
    /// </example>
    public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default) {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(Device));
        }

        if (string.IsNullOrWhiteSpace(code)) {
            throw new ArgumentException("Code cannot be null or empty", nameof(code));
        }

        this.logger.LogDebug("Executing code: {Code}", code.Trim());

        // Track operation in device state
        this.State.SetCurrentOperation("ExecuteCode");
        
        try {
            // Check if we have an execution context with an attributed method and route through appropriate executor
            var executionContext = this.executionContextService.Current;
            if (executionContext?.TaskAttribute != null) {
                return await this.taskExecutor.Value.ApplyPoliciesAndExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
            }

            if (executionContext?.SetupAttribute != null) {
                return await this.setupExecutor.Value.ApplyPoliciesAndExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
            }

            if (executionContext?.ThreadAttribute != null) {
                return await this.threadExecutor.Value.ApplyPoliciesAndExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
            }

            if (executionContext?.TeardownAttribute != null) {
                return await this.teardownExecutor.Value.ApplyPoliciesAndExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
            }

            // Direct execution without policies
            return await this.communication.ExecuteAsync(code, cancellationToken).ConfigureAwait(false);
        }
        finally {
            this.State.CompleteOperation();
        }
    }

    /// <summary>
    /// Execute Python code on the device and return the result as a typed object.
    /// If called from a method with Belay attributes, applies attribute-specific policies.
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <param name="code">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result cast to the specified type.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the device has been disposed.</exception>
    /// <exception cref="ArgumentException">Thrown when code is null or empty.</exception>
    /// <exception cref="DeviceConnectionException">Thrown when the device is not connected.</exception>
    /// <exception cref="DeviceExecutionException">Thrown when code execution fails on the device.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result cannot be converted to type T.</exception>
    /// <example>
    /// <code>
    /// var device = Device.FromConnectionString("serial:COM3");
    /// await device.ConnectAsync();
    ///
    /// // Execute code that returns a number
    /// int result = await device.ExecuteAsync&lt;int&gt;("2 + 3");
    ///
    /// // Execute code that returns a complex object
    /// var data = await device.ExecuteAsync&lt;Dictionary&lt;string, object&gt;&gt;("{'temperature': 25.5, 'humidity': 60}");
    /// </code>
    /// </example>
    public async Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default) {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(Device));
        }

        if (string.IsNullOrWhiteSpace(code)) {
            throw new ArgumentException("Code cannot be null or empty", nameof(code));
        }

        // Track operation in device state
        this.State.SetCurrentOperation("ExecuteCode");
        
        try {
            // Check if we have an execution context with an attributed method and route through appropriate executor
            var executionContext = this.executionContextService.Current;
            if (executionContext?.TaskAttribute != null) {
                return await this.taskExecutor.Value.ApplyPoliciesAndExecuteAsync<T>(code, cancellationToken).ConfigureAwait(false);
            }

            if (executionContext?.SetupAttribute != null) {
                return await this.setupExecutor.Value.ApplyPoliciesAndExecuteAsync<T>(code, cancellationToken).ConfigureAwait(false);
            }

            if (executionContext?.ThreadAttribute != null) {
                return await this.threadExecutor.Value.ApplyPoliciesAndExecuteAsync<T>(code, cancellationToken).ConfigureAwait(false);
            }

            if (executionContext?.TeardownAttribute != null) {
                return await this.teardownExecutor.Value.ApplyPoliciesAndExecuteAsync<T>(code, cancellationToken).ConfigureAwait(false);
            }

            // Direct execution without policies
            return await this.communication.ExecuteAsync<T>(code, cancellationToken).ConfigureAwait(false);
        }
        finally {
            this.State.CompleteOperation();
        }
    }

    /// <summary>
    /// Transfer a file from the local system to the device.
    /// </summary>
    /// <param name="localPath">Path to the local file.</param>
    /// <param name="remotePath">Path where the file should be stored on the device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task PutFileAsync(string localPath, string remotePath, CancellationToken cancellationToken = default) {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(Device));
        }

        this.logger.LogDebug("Transferring file {LocalPath} to device at {RemotePath}", localPath, remotePath);
        
        this.State.SetCurrentOperation("FileTransfer");
        try {
            await this.communication.PutFileAsync(localPath, remotePath, cancellationToken);
            this.logger.LogInformation("Successfully transferred file to device");
        }
        finally {
            this.State.CompleteOperation();
        }
    }

    /// <summary>
    /// Retrieve a file from the device to the local system.
    /// </summary>
    /// <param name="remotePath">Path to the file on the device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file contents as a byte array.</returns>
    public async Task<byte[]> GetFileAsync(string remotePath, CancellationToken cancellationToken = default) {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(Device));
        }

        this.logger.LogDebug("Retrieving file {RemotePath} from device", remotePath);
        
        this.State.SetCurrentOperation("FileRetrieval");
        try {
            byte[] content = await this.communication.GetFileAsync(remotePath, cancellationToken);
            this.logger.LogInformation("Successfully retrieved file from device ({Size} bytes)", content.Length);
            return content;
        }
        finally {
            this.State.CompleteOperation();
        }
    }

    /// <summary>
    /// Create a Device instance from a connection string.
    /// </summary>
    /// <param name="connectionString">Connection string (e.g., "serial:COM3", "subprocess:micropython").</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <returns>A configured Device instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the connection string is invalid or unsupported.</exception>
    /// <example>
    /// <code>
    /// // Connect to a device via serial port
    /// var device = Device.FromConnectionString("serial:COM3");
    ///
    /// // Connect to MicroPython subprocess for testing
    /// var testDevice = Device.FromConnectionString("subprocess:micropython");
    ///
    /// // With logging
    /// var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    /// var deviceWithLogging = Device.FromConnectionString("serial:/dev/ttyACM0", loggerFactory);
    /// </code>
    /// </example>
    public static Device FromConnectionString(string connectionString, ILoggerFactory? loggerFactory = null) {
        if (string.IsNullOrWhiteSpace(connectionString)) {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        string[] parts = connectionString.Split(':', 2);
        if (parts.Length != 2) {
            throw new ArgumentException($"Invalid connection string format: {connectionString}. Expected format: 'type:parameter'");
        }

        string type = parts[0].ToLowerInvariant();
        string parameter = parts[1];

        IDeviceCommunication communication = type switch {
            "serial" => new SerialDeviceCommunication(parameter, logger: loggerFactory?.CreateLogger<SerialDeviceCommunication>()),
            "subprocess" => new SubprocessDeviceCommunication(parameter, logger: loggerFactory?.CreateLogger<SubprocessDeviceCommunication>()),
            _ => throw new ArgumentException($"Unsupported connection type: {type}"),
        };

        return new Device(communication, loggerFactory?.CreateLogger<Device>(), loggerFactory);
    }

    /// <summary>
    /// Discover available MicroPython devices on the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of discovered device information.</returns>
    public static async Task<Discovery.DeviceInfo[]> DiscoverDevicesAsync(CancellationToken cancellationToken = default) {
        return await SerialDeviceDiscovery.DiscoverMicroPythonDevicesAsync(cancellationToken);
    }

    /// <summary>
    /// Create a Device instance for the first discovered MicroPython device.
    /// </summary>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Device instance for the first discovered device, or null if none found.</returns>
    public static async Task<Device?> DiscoverFirstAsync(ILoggerFactory? loggerFactory = null, CancellationToken cancellationToken = default) {
        Discovery.DeviceInfo[] devices = await DiscoverDevicesAsync(cancellationToken);
        if (devices.Length == 0) {
            return null;
        }

        return FromConnectionString(devices[0].ConnectionString, loggerFactory);
    }

    /// <summary>
    /// Executes a method with automatic executor selection based on attributes.
    /// This is the main entry point for the attribute-based programming model and uses secure execution context.
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <param name="method">The method to execute.</param>
    /// <param name="instance">The instance to invoke the method on (null for static methods).</param>
    /// <param name="parameters">The parameters to pass to the method.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
    /// <returns>The result of the method execution.</returns>
    public async Task<T> ExecuteMethodAsync<T>(System.Reflection.MethodInfo method, object? instance = null, object?[]? parameters = null, CancellationToken cancellationToken = default) {
        if (method == null) {
            throw new ArgumentNullException(nameof(method));
        }

        if (this.disposed) {
            throw new ObjectDisposedException(nameof(Device));
        }

        // Create execution context for secure attribute detection (replacement for stack frame inspection)
        var context = new MethodExecutionContext(method, instance, parameters);
        using var contextScope = this.executionContextService.SetContext(context);

        // Find the appropriate executor for this method
        var executor = this.GetExecutorForMethod(method);
        if (executor == null) {
            throw new InvalidOperationException($"No suitable executor found for method '{method.Name}'. Ensure the method has a supported attribute ([Task], [Setup], [Thread], or [Teardown]).");
        }

        this.logger.LogDebug("Executing method {MethodName} using {ExecutorType} with secure execution context", method.Name, executor.GetType().Name);

        return await executor.ExecuteAsync<T>(method, instance, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a method without returning a value with automatic executor selection based on attributes.
    /// </summary>
    /// <param name="method">The method to execute.</param>
    /// <param name="instance">The instance to invoke the method on (null for static methods).</param>
    /// <param name="parameters">The parameters to pass to the method.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExecuteMethodAsync(System.Reflection.MethodInfo method, object? instance = null, object?[]? parameters = null, CancellationToken cancellationToken = default) {
        await this.ExecuteMethodAsync<object>(method, instance, parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the appropriate executor for a method based on its attributes.
    /// </summary>
    /// <param name="method">The method to get an executor for.</param>
    /// <returns>The executor that can handle the method, or null if none found.</returns>
    private Execution.IExecutor? GetExecutorForMethod(System.Reflection.MethodInfo method) {
        // Check executors in order of priority
        if (this.Task.CanHandle(method)) {
            return this.Task;
        }

        if (this.Setup.CanHandle(method)) {
            return this.Setup;
        }

        if (this.Thread.CanHandle(method)) {
            return this.Thread;
        }

        if (this.Teardown.CanHandle(method)) {
            return this.Teardown;
        }

        return null;
    }

    /// <summary>
    /// Gets device identification information for cache key generation.
    /// </summary>
    /// <returns>A tuple containing device identifier and firmware version.</returns>
    internal (string DeviceId, string FirmwareVersion) GetDeviceIdentification() {
        var deviceId = this.communication switch {
            SerialDeviceCommunication serial => $"serial:{serial.PortName}",
            SubprocessDeviceCommunication subprocess => "subprocess:micropython",
            _ => $"unknown:{this.communication.GetType().Name}",
        };

        // TODO: Get actual firmware version from device using sys.implementation or uos.uname()
        // For now, use a placeholder that will be enhanced when device info is available
        var firmwareVersion = "unknown";

        return (deviceId, firmwareVersion);
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (this.disposed) {
            return;
        }

        // Dispose simplified executors if they have been initialized
        if (this.taskExecutor.IsValueCreated)
        {
            this.taskExecutor.Value?.Dispose();
        }
        
        this.methodCache?.Dispose();
        this.communication?.Dispose();
        this.disposed = true;
    }
}
