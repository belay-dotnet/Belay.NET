// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

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
    private readonly DeviceConnection connection;
    private readonly ILogger<Device> logger;
    private readonly IExecutionContextService executionContextService;
    private readonly Lazy<DirectExecutor> executor;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Device"/> class.
    /// Simple constructor for basic usage.
    /// </summary>
    /// <param name="connection">The device connection implementation.</param>
    public Device(DeviceConnection connection)
        : this(connection, null, null, null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Device"/> class with logger.
    /// </summary>
    /// <param name="connection">The device connection implementation.</param>
    /// <param name="logger">Logger for device operations.</param>
    public Device(DeviceConnection connection, ILogger<Device> logger)
        : this(connection, logger, null, null) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Device"/> class with dependency injection support.
    /// </summary>
    /// <param name="connection">The device connection implementation.</param>
    /// <param name="logger">Logger for device operations.</param>
    /// <param name="loggerFactory">Optional logger factory for executor logging.</param>
    /// <param name="executionContextService">Optional execution context service for secure method detection.</param>
    public Device(DeviceConnection connection, ILogger<Device>? logger, ILoggerFactory? loggerFactory = null, IExecutionContextService? executionContextService = null) {
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<Device>.Instance;

        // Note: Event forwarding removed as the connection layer events were cleaned up
        // Device state is updated directly through connection property access

        var executorLoggerFactory = loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

        // Use injected execution context service or create a simple default one
        this.executionContextService = executionContextService ?? new SimpleExecutionContextService();

        // Initialize device state with current connection state
        this.State.ConnectionState = connection.State;

        // Initialize single direct executor using AttributeHandler
        var deviceConnection = new SimplifiedDevice(connection, executorLoggerFactory.CreateLogger<SimplifiedDevice>());
        this.executor = new Lazy<DirectExecutor>(() => new DirectExecutor(deviceConnection, executorLoggerFactory.CreateLogger<DirectExecutor>()));
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
    public DeviceConnectionState ConnectionState => this.connection.State;

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
    /// Gets the connection interface for this device.
    /// Internal use for executors and session management.
    /// </summary>
    internal DeviceConnection Connection => this.connection;

    /// <summary>
    /// Gets the direct executor that handles all attribute types via AttributeHandler.
    /// </summary>
    /// <remarks>
    /// This unified executor replaces the complex hierarchy with a single implementation
    /// that handles [Task], [Setup], [Thread], and [Teardown] attributes using the
    /// AttributeHandler for policy enforcement and SimpleCache for caching.
    /// </remarks>
    public DirectExecutor Executor => this.executor.Value;

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
            await this.connection.ConnectAsync(cancellationToken);

            // Update state after successful connection
            this.State.ConnectionState = this.connection.State;

            this.logger.LogInformation("Successfully connected to device, performing capability detection");

            // Perform fast capability detection using batched approach
            try {
                this.State.SetCurrentOperation("CapabilityDetection");
                this.State.Capabilities = await SimplifiedCapabilityDetection.DetectAsync(
                    this.connection, this.logger, cancellationToken);
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

        await this.connection.DisconnectAsync(cancellationToken);

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
    /// <exception cref="DeviceException">Thrown when device communication or execution fails.</exception>
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
            // Use unified DirectExecutor for all code execution
            return await this.Executor.ExecutePythonAsync<string>(code, cancellationToken).ConfigureAwait(false);
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
    /// <exception cref="DeviceException">Thrown when device communication or execution fails.</exception>
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
            // Use unified DirectExecutor for all code execution
            return await this.Executor.ExecutePythonAsync<T>(code, cancellationToken).ConfigureAwait(false);
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
            await this.connection.PutFileAsync(localPath, remotePath, cancellationToken);
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
            byte[] content = await this.connection.GetFileAsync(remotePath, cancellationToken);
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

        DeviceConnection.ConnectionType connectionType = type switch {
            "serial" => DeviceConnection.ConnectionType.Serial,
            "subprocess" => DeviceConnection.ConnectionType.Subprocess,
            _ => throw new ArgumentException($"Unsupported connection type: {type}"),
        };

        var deviceConnection = new DeviceConnection(connectionType, parameter, loggerFactory?.CreateLogger<DeviceConnection>());
        return new Device(deviceConnection, loggerFactory?.CreateLogger<Device>(), loggerFactory);
    }

    /// <summary>
    /// Discover available MicroPython devices on the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of connection strings for discovered devices.</returns>
    public static async Task<string[]> DiscoverDevicesAsync(CancellationToken cancellationToken = default) {
        return await DeviceDiscovery.DiscoverDevicesAsync(cancellationToken);
    }

    /// <summary>
    /// Create a Device instance for the first discovered MicroPython device.
    /// </summary>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Device instance for the first discovered device, or null if none found.</returns>
    public static async Task<Device?> DiscoverFirstAsync(ILoggerFactory? loggerFactory = null, CancellationToken cancellationToken = default) {
        var discoveredConnection = await DeviceDiscovery.DiscoverFirstAsync(cancellationToken);
        if (discoveredConnection == null) {
            return null;
        }

        return new Device(discoveredConnection, loggerFactory?.CreateLogger<Device>(), loggerFactory);
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

        // Use unified DirectExecutor for all method execution via AttributeHandler
        this.logger.LogDebug("Executing method {MethodName} using DirectExecutor with secure execution context", method.Name);

        return await this.Executor.ExecuteAsync<T>(method, parameters ?? Array.Empty<object>(), cancellationToken).ConfigureAwait(false);
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
    /// Gets device identification information for cache key generation.
    /// </summary>
    /// <returns>A tuple containing device identifier and firmware version.</returns>
    internal (string DeviceId, string FirmwareVersion) GetDeviceIdentification() {
        var deviceId = this.connection.Type switch {
            DeviceConnection.ConnectionType.Serial => $"serial:{this.connection.ConnectionString}",
            DeviceConnection.ConnectionType.Subprocess => "subprocess:micropython",
            _ => $"unknown:{this.connection.GetType().Name}",
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

        // Dispose unified executor if it has been initialized
        if (this.executor.IsValueCreated) {
            this.executor.Value?.Dispose();
        }

        this.connection?.Dispose();
        this.disposed = true;
    }
}
