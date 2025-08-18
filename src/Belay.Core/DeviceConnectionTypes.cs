// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

/// <summary>
/// Represents the output received from a device.
/// </summary>
public class DeviceOutputEventArgs(string output, bool isError = false) : EventArgs {
    /// <summary>
    /// Gets the output text received from the device.
    /// </summary>
    /// <value>The raw string output from the device execution or communication.</value>
    public string Output { get; } = output ?? throw new ArgumentNullException(nameof(output));

    /// <summary>
    /// Gets a value indicating whether this output represents an error.
    /// </summary>
    /// <value>True if the output is from stderr or represents an error condition; otherwise, false.</value>
    public bool IsError { get; } = isError

    /// <summary>
    /// Gets the timestamp when this output was received.
    /// </summary>
    /// <value>The UTC timestamp of when the output was captured.</value>
    public DateTime Timestamp { get; }
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
    public Exception? Exception { get; } = exception

    /// <summary>
    /// Gets the timestamp when the state change occurred.
    /// </summary>
    /// <value>The UTC timestamp of when the state transition was detected.</value>
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
