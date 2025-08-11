// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Communication;

/// <summary>
/// Represents the output received from a device.
/// </summary>
public class DeviceOutputEventArgs(string output, bool isError = false) : EventArgs {
    /// <summary>
    /// Gets the output text received from the device.
    /// </summary>
    /// <value>The raw string output from the device execution or communication.</value>
    public string Output { get; } = output;

    /// <summary>
    /// Gets a value indicating whether this output represents an error.
    /// </summary>
    /// <value>True if the output is from stderr or represents an error condition; otherwise, false.</value>
    public bool IsError { get; } = isError;

    /// <summary>
    /// Gets the timestamp when this output was received.
    /// </summary>
    /// <value>The UTC timestamp of when the output was captured.</value>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a change in device connection state.
/// </summary>
public class DeviceStateChangeEventArgs(DeviceConnectionState oldState, DeviceConnectionState newState,
    string? reason = null, Exception? exception = null) : EventArgs {
    /// <summary>
    /// Gets the previous connection state before the change.
    /// </summary>
    /// <value>The device connection state before the transition occurred.</value>
    public DeviceConnectionState OldState { get; } = oldState;

    /// <summary>
    /// Gets the new connection state after the change.
    /// </summary>
    /// <value>The device connection state after the transition occurred.</value>
    public DeviceConnectionState NewState { get; } = newState;

    /// <summary>
    /// Gets the reason for the state change, if available.
    /// </summary>
    /// <value>A human-readable description of why the state changed, or null if no specific reason is available.</value>
    public string? Reason { get; } = reason;

    /// <summary>
    /// Gets the exception that caused the state change, if applicable.
    /// </summary>
    /// <value>The exception that triggered the state change, or null if the change was not due to an error.</value>
    public Exception? Exception { get; } = exception;

    /// <summary>
    /// Gets the timestamp when the state change occurred.
    /// </summary>
    /// <value>The UTC timestamp of when the state transition was detected.</value>
    public DateTime Timestamp { get; } = DateTime.UtcNow;
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
/// <remarks>
/// <para>
/// This interface defines the standard contract for communicating with MicroPython
/// devices through various transport mechanisms (serial, subprocess, network, etc.).
/// All implementations handle the Raw REPL protocol for reliable code execution
/// and file transfer operations.
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Basic Usage</strong></para>
/// <code>
/// using IDeviceCommunication device = new SerialDeviceCommunication("COM3");
///
/// // Execute simple code
/// string result = await device.ExecuteAsync("print('Hello from device!')");
/// Console.WriteLine($"Device output: {result}");
///
/// // Execute with typed return
/// float temperature = await device.ExecuteAsync&lt;float&gt;(@"
///     import machine
///     adc = machine.ADC(machine.Pin(26))
///     reading = adc.read_u16()
///     temperature = 27 - (reading * 3.3 / 65535 - 0.706) / 0.001721
///     temperature
/// ");
///
/// // File operations
/// await device.PutFileAsync("config.json", "/config.json");
/// byte[] data = await device.GetFileAsync("/sensor_data.csv");
/// </code>
/// <para><strong>Event Handling</strong></para>
/// <code>
/// device.OutputReceived += (sender, args) => {
///     if (args.IsError) {
///         Console.WriteLine($"Error: {args.Output}");
///     } else {
///         Console.WriteLine($"Output: {args.Output}");
///     }
/// };
///
/// device.StateChanged += (sender, args) => {
///     Console.WriteLine($"State: {args.OldState} → {args.NewState}");
///     if (args.Exception != null) {
///         Console.WriteLine($"Error: {args.Exception.Message}");
///     }
/// };
/// </code>
/// </example>
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
