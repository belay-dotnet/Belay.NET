// Copyright (c) 2024 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for more information.
/// <summary>
/// Represents the output received from a device.
/// </summary>
/// <inheritdoc/>
public class DeviceOutputEventArgs(string output, bool isError = false) : EventArgs {
    /// <inheritdoc/>
    public string Output { get; }

    /// <inheritdoc/>
    public bool IsError { get; }

    /// <inheritdoc/>
    public DateTime Timestamp { get; }
}

/// <summary>
/// Represents a change in device connection state.
/// </summary>
/// <inheritdoc/>
public class DeviceStateChangeEventArgs(DeviceConnectionState oldState, DeviceConnectionState newState,
    string? reason = null, Exception? exception = null) : EventArgs {
    /// <inheritdoc/>
    public DeviceConnectionState OldState { get; }

    /// <inheritdoc/>
    public DeviceConnectionState NewState { get; }

    /// <inheritdoc/>
    public string? Reason { get; }

    /// <inheritdoc/>
    public Exception? Exception { get; }

    /// <inheritdoc/>
    public DateTime Timestamp { get; }
}

/// <summary>
/// Represents the connection state of a device.
/// </summary>
public enum DeviceConnectionState {
    /// <summary>Device is not connected.</summary>
    Disconnected,

    /// <summary>Attempting to connect to device.</summary>
    Connecting,

    /// <summary>Successfully connected and ready.</summary>
    Connected,

    /// <summary>Currently executing code on device.</summary>
    Executing,

    /// <summary>Connection error state.</summary>
    Error,

    /// <summary>Attempting to reconnect after connection loss.</summary>
    Reconnecting,
}

/// <summary>
/// Core interface for device communication implementations.
/// </summary>
public interface IDeviceCommunication : IDisposable {
    /// <summary>
    /// Gets current connection state of the device.
    /// </summary>
    DeviceConnectionState State { get; }

    /// <summary>
    /// Execute Python code on the device and return the result as a string.
    /// </summary>
    /// <param name="code">Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of code execution as string.</returns>
    Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute Python code on the device and return the result as typed object.
    /// </summary>
    /// <typeparam name="T">Expected return type.</typeparam>
    /// <param name="code">Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of code execution as typed object.</returns>
    Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfer a file from local system to device.
    /// </summary>
    /// <param name="localPath">Local file path.</param>
    /// <param name="remotePath">Remote file path on device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task PutFileAsync(string localPath, string remotePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a file from device to local system.
    /// </summary>
    /// <param name="remotePath">Remote file path on device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File contents as byte array.</returns>
    Task<byte[]> GetFileAsync(string remotePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when output is received from the device
    /// </summary>
    event EventHandler<DeviceOutputEventArgs>? OutputReceived;

    /// <summary>
    /// Event raised when device connection state changes
    /// </summary>
    event EventHandler<DeviceStateChangeEventArgs>? StateChanged;
}
