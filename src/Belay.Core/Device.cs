// Copyright 2025 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0

namespace Belay.Core;

using Belay.Core.Communication;
using Belay.Core.Discovery;
using Microsoft.Extensions.Logging;

/// <summary>
/// Main entry point for MicroPython device communication.
/// Provides a high-level interface for connecting to and interacting with MicroPython devices.
/// </summary>
public class Device : IDisposable
{
    private readonly IDeviceCommunication communication;
    private readonly ILogger<Device> logger;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Device"/> class.
    /// </summary>
    /// <param name="communication">The device communication implementation.</param>
    /// <param name="logger">Optional logger for device operations.</param>
    public Device(IDeviceCommunication communication, ILogger<Device>? logger = null)
    {
        this.communication = communication ?? throw new ArgumentNullException(nameof(communication));
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<Device>.Instance;
        
        // Forward events from communication layer
        this.communication.OutputReceived += (sender, args) => this.OutputReceived?.Invoke(this, args);
        this.communication.StateChanged += (sender, args) => this.StateChanged?.Invoke(this, args);
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
    /// Connect to the MicroPython device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(Device));
        }

        this.logger.LogDebug("Connecting to device");
        
        if (this.communication is SerialDeviceCommunication serial)
        {
            await serial.ConnectAsync(cancellationToken);
        }
        else if (this.communication is SubprocessDeviceCommunication subprocess)
        {
            await subprocess.StartAsync(cancellationToken);
        }
        else
        {
            throw new NotSupportedException($"Communication type {this.communication.GetType().Name} is not supported");
        }

        this.logger.LogInformation("Successfully connected to device");
    }

    /// <summary>
    /// Disconnect from the MicroPython device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            return;
        }

        this.logger.LogDebug("Disconnecting from device");

        if (this.communication is SerialDeviceCommunication serial)
        {
            await serial.DisconnectAsync(cancellationToken);
        }
        else if (this.communication is SubprocessDeviceCommunication subprocess)
        {
            await subprocess.StopAsync(cancellationToken);
        }

        this.logger.LogInformation("Disconnected from device");
    }

    /// <summary>
    /// Execute Python code on the device and return the result.
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
    public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(Device));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code cannot be null or empty", nameof(code));
        }

        this.logger.LogDebug("Executing code: {Code}", code.Trim());
        return await this.communication.ExecuteAsync(code, cancellationToken);
    }

    /// <summary>
    /// Execute Python code on the device and return the result as a typed object.
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
    public async Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default)
    {
        return await this.communication.ExecuteAsync<T>(code, cancellationToken);
    }

    /// <summary>
    /// Transfer a file from the local system to the device.
    /// </summary>
    /// <param name="localPath">Path to the local file.</param>
    /// <param name="remotePath">Path where the file should be stored on the device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task PutFileAsync(string localPath, string remotePath, CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
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
    public async Task<byte[]> GetFileAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
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
    public static Device FromConnectionString(string connectionString, ILoggerFactory? loggerFactory = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        }

        string[] parts = connectionString.Split(':', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid connection string format: {connectionString}. Expected format: 'type:parameter'");
        }

        string type = parts[0].ToLowerInvariant();
        string parameter = parts[1];

        IDeviceCommunication communication = type switch
        {
            "serial" => new SerialDeviceCommunication(parameter, logger: loggerFactory?.CreateLogger<SerialDeviceCommunication>()),
            "subprocess" => new SubprocessDeviceCommunication(parameter, logger: loggerFactory?.CreateLogger<SubprocessDeviceCommunication>()),
            _ => throw new ArgumentException($"Unsupported connection type: {type}")
        };

        return new Device(communication, loggerFactory?.CreateLogger<Device>());
    }

    /// <summary>
    /// Discover available MicroPython devices on the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of discovered device information.</returns>
    public static async Task<DeviceInfo[]> DiscoverDevicesAsync(CancellationToken cancellationToken = default)
    {
        return await SerialDeviceDiscovery.DiscoverMicroPythonDevicesAsync(cancellationToken);
    }

    /// <summary>
    /// Create a Device instance for the first discovered MicroPython device.
    /// </summary>
    /// <param name="loggerFactory">Optional logger factory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Device instance for the first discovered device, or null if none found.</returns>
    public static async Task<Device?> DiscoverFirstAsync(ILoggerFactory? loggerFactory = null, CancellationToken cancellationToken = default)
    {
        DeviceInfo[] devices = await DiscoverDevicesAsync(cancellationToken);
        if (devices.Length == 0)
        {
            return null;
        }

        return FromConnectionString(devices[0].ConnectionString, loggerFactory);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.communication?.Dispose();
        this.disposed = true;
    }
}