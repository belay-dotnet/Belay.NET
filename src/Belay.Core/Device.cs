// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

using Belay.Core.Communication;
using Belay.Core.Discovery;
using Belay.Core.Execution;
using Belay.Core.Sessions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Main entry point for MicroPython device communication.
/// Provides a high-level interface for connecting to and interacting with MicroPython devices.
/// </summary>
public class Device : IDisposable {
    private readonly IDeviceCommunication communication;
    private readonly ILogger<Device> logger;
    private readonly IDeviceSessionManager sessionManager;
    private readonly Lazy<TaskExecutor> taskExecutor;
    private readonly Lazy<SetupExecutor> setupExecutor;
    private readonly Lazy<ThreadExecutor> threadExecutor;
    private readonly Lazy<TeardownExecutor> teardownExecutor;
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
    public Device(IDeviceCommunication communication, ILogger<Device>? logger = null, ILoggerFactory? loggerFactory = null) {
        this.communication = communication ?? throw new ArgumentNullException(nameof(communication));
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<Device>.Instance;

        // Forward events from communication layer
        this.communication.OutputReceived += (sender, args) => this.OutputReceived?.Invoke(this, args);
        this.communication.StateChanged += (sender, args) => this.StateChanged?.Invoke(this, args);

        var executorLoggerFactory = loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;

        // Initialize session manager
        this.sessionManager = new DeviceSessionManager(
            this.communication,
            executorLoggerFactory);

        // Initialize executors lazily with session management support
        this.taskExecutor = new Lazy<TaskExecutor>(() => new TaskExecutor(this, this.sessionManager, executorLoggerFactory.CreateLogger<TaskExecutor>()));
        this.setupExecutor = new Lazy<SetupExecutor>(() => new SetupExecutor(this, this.sessionManager, executorLoggerFactory.CreateLogger<SetupExecutor>()));
        this.threadExecutor = new Lazy<ThreadExecutor>(() => new ThreadExecutor(this, this.sessionManager, executorLoggerFactory.CreateLogger<ThreadExecutor>()));
        this.teardownExecutor = new Lazy<TeardownExecutor>(() => new TeardownExecutor(this, this.sessionManager, executorLoggerFactory.CreateLogger<TeardownExecutor>()));
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
    public DeviceConnectionState State => this.communication.State;

    /// <summary>
    /// Gets the task executor for methods decorated with [Task] attribute.
    /// </summary>
    public TaskExecutor Task => this.taskExecutor.Value;

    /// <summary>
    /// Gets the setup executor for methods decorated with [Setup] attribute.
    /// </summary>
    public SetupExecutor Setup => this.setupExecutor.Value;

    /// <summary>
    /// Gets the thread executor for methods decorated with [Thread] attribute.
    /// </summary>
    public ThreadExecutor Thread => this.threadExecutor.Value;

    /// <summary>
    /// Gets the teardown executor for methods decorated with [Teardown] attribute.
    /// </summary>
    public TeardownExecutor Teardown => this.teardownExecutor.Value;

    /// <summary>
    /// Gets the session manager for advanced session coordination scenarios.
    /// </summary>
    public IDeviceSessionManager Sessions => this.sessionManager;

    /// <summary>
    /// Connect to the MicroPython device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ConnectAsync(CancellationToken cancellationToken = default) {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(Device));
        }

        this.logger.LogDebug("Connecting to device");

        if (this.communication is SerialDeviceCommunication serial) {
            await serial.ConnectAsync(cancellationToken);
        }
        else if (this.communication is SubprocessDeviceCommunication subprocess) {
            await subprocess.StartAsync(cancellationToken);
        }
        else {
            throw new NotSupportedException($"Communication type {this.communication.GetType().Name} is not supported");
        }

        this.logger.LogInformation("Successfully connected to device");
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

        // Execute teardown methods before disconnecting
        try {
            await this.teardownExecutor.Value.ExecuteAllTeardownMethodsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) {
            this.logger.LogError(ex, "Error executing teardown methods during disconnect");
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

        // Check if we're being called from an attributed method and route through appropriate executor
        var callingMethod = this.GetCallingMethod();
        if (callingMethod?.HasAttribute<Belay.Attributes.TaskAttribute>() == true) {
            return await this.taskExecutor.Value.ApplyPoliciesAndExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
        }

        if (callingMethod?.HasAttribute<Belay.Attributes.SetupAttribute>() == true) {
            return await this.setupExecutor.Value.ApplyPoliciesAndExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
        }

        if (callingMethod?.HasAttribute<Belay.Attributes.ThreadAttribute>() == true) {
            return await this.threadExecutor.Value.ApplyPoliciesAndExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
        }

        if (callingMethod?.HasAttribute<Belay.Attributes.TeardownAttribute>() == true) {
            return await this.teardownExecutor.Value.ApplyPoliciesAndExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
        }

        // Direct execution without policies
        return await this.communication.ExecuteAsync(code, cancellationToken).ConfigureAwait(false);
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

        // Check if we're being called from an attributed method and route through appropriate executor
        var callingMethod = this.GetCallingMethod();
        if (callingMethod?.HasAttribute<Belay.Attributes.TaskAttribute>() == true) {
            return await this.taskExecutor.Value.ApplyPoliciesAndExecuteAsync<T>(code, cancellationToken).ConfigureAwait(false);
        }

        if (callingMethod?.HasAttribute<Belay.Attributes.SetupAttribute>() == true) {
            return await this.setupExecutor.Value.ApplyPoliciesAndExecuteAsync<T>(code, cancellationToken).ConfigureAwait(false);
        }

        if (callingMethod?.HasAttribute<Belay.Attributes.ThreadAttribute>() == true) {
            return await this.threadExecutor.Value.ApplyPoliciesAndExecuteAsync<T>(code, cancellationToken).ConfigureAwait(false);
        }

        if (callingMethod?.HasAttribute<Belay.Attributes.TeardownAttribute>() == true) {
            return await this.teardownExecutor.Value.ApplyPoliciesAndExecuteAsync<T>(code, cancellationToken).ConfigureAwait(false);
        }

        // Direct execution without policies
        return await this.communication.ExecuteAsync<T>(code, cancellationToken).ConfigureAwait(false);
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
        await this.communication.PutFileAsync(localPath, remotePath, cancellationToken);
        this.logger.LogInformation("Successfully transferred file to device");
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
        byte[] content = await this.communication.GetFileAsync(remotePath, cancellationToken);
        this.logger.LogInformation("Successfully retrieved file from device ({Size} bytes)", content.Length);
        return content;
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
    /// Gets the calling method information from the stack frame.
    /// </summary>
    /// <param name="skipFrames">Number of frames to skip (default is 2 to skip this method and the caller).</param>
    /// <returns>The calling method information, or null if not available.</returns>
    private System.Reflection.MethodInfo? GetCallingMethod(int skipFrames = 2) {
        try {
            var stackTrace = new System.Diagnostics.StackTrace();
            if (stackTrace.FrameCount <= skipFrames) {
                return null;
            }

            var frame = stackTrace.GetFrame(skipFrames);
            return frame?.GetMethod() as System.Reflection.MethodInfo;
        }
        catch {
            // Stack trace inspection failed - return null
            return null;
        }
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

    /// <inheritdoc/>
    public void Dispose() {
        if (this.disposed) {
            return;
        }

        // Dispose session manager first to clean up resources
        this.sessionManager?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        this.communication?.Dispose();
        this.disposed = true;
    }
}
