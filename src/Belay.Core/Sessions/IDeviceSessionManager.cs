// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    using Belay.Core.Communication;

    /// <summary>
    /// Represents the possible states of a device session manager.
    /// </summary>
    public enum DeviceSessionState {
        /// <summary>
        /// Session manager is inactive.
        /// </summary>
        Inactive,

        /// <summary>
        /// Session manager is active and ready to create sessions.
        /// </summary>
        Active,

        /// <summary>
        /// Session manager is shutting down.
        /// </summary>
        Shutdown,

        /// <summary>
        /// Session manager has been disposed.
        /// </summary>
        Disposed,
    }

    /// <summary>
    /// Event arguments for device session state changes.
    /// </summary>
    public class DeviceSessionStateChangedEventArgs : EventArgs {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceSessionStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="oldState">The previous state.</param>
        /// <param name="newState">The new state.</param>
        public DeviceSessionStateChangedEventArgs(DeviceSessionState oldState, DeviceSessionState newState) {
            this.OldState = oldState;
            this.NewState = newState;
        }

        /// <summary>
        /// Gets the previous state.
        /// </summary>
        public DeviceSessionState OldState { get; }

        /// <summary>
        /// Gets the new state.
        /// </summary>
        public DeviceSessionState NewState { get; }
    }

    /// <summary>
    /// Event arguments for device capabilities changes.
    /// </summary>
    public class DeviceCapabilitiesChangedEventArgs : EventArgs {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceCapabilitiesChangedEventArgs"/> class.
        /// </summary>
        /// <param name="oldCapabilities">The previous capabilities.</param>
        /// <param name="newCapabilities">The new capabilities.</param>
        public DeviceCapabilitiesChangedEventArgs(IDeviceCapabilities? oldCapabilities, IDeviceCapabilities? newCapabilities) {
            this.OldCapabilities = oldCapabilities;
            this.NewCapabilities = newCapabilities;
        }

        /// <summary>
        /// Gets the previous capabilities.
        /// </summary>
        public IDeviceCapabilities? OldCapabilities { get; }

        /// <summary>
        /// Gets the new capabilities.
        /// </summary>
        public IDeviceCapabilities? NewCapabilities { get; }
    }

    /// <summary>
    /// Manages device sessions and provides session coordination.
    /// </summary>
    public interface IDeviceSessionManager : IAsyncDisposable {
        /// <summary>
        /// Gets the current session identifier, if any.
        /// </summary>
        string? CurrentSessionId { get; }

        /// <summary>
        /// Gets the current state of the session manager.
        /// </summary>
        DeviceSessionState State { get; }

        /// <summary>
        /// Gets the device capabilities if they have been detected.
        /// </summary>
        IDeviceCapabilities? Capabilities { get; }

        /// <summary>
        /// Creates a new session for device operations.
        /// </summary>
        /// <param name="communication">The device communication instance for this session.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A new device session.</returns>
        Task<IDeviceSession> CreateSessionAsync(IDeviceCommunication communication, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current session or creates a new one if none exists.
        /// </summary>
        /// <param name="communication">The device communication instance for this session.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The current or a new device session.</returns>
        Task<IDeviceSession> GetOrCreateSessionAsync(IDeviceCommunication communication, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ends the specified session and cleans up its resources.
        /// </summary>
        /// <param name="sessionId">The identifier of the session to end.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task EndSessionAsync(string sessionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an operation within a session context, creating one if necessary.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="communication">The device communication instance for this session.</param>
        /// <param name="operation">The operation to execute within the session.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The result of the operation.</returns>
        Task<T> ExecuteInSessionAsync<T>(
            IDeviceCommunication communication,
            Func<IDeviceSession, Task<T>> operation,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an operation within a session context, creating one if necessary.
        /// </summary>
        /// <param name="communication">The device communication instance for this session.</param>
        /// <param name="operation">The operation to execute within the session.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task ExecuteInSessionAsync(
            IDeviceCommunication communication,
            Func<IDeviceSession, Task> operation,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets statistics about the session manager.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>Session statistics.</returns>
        Task<SessionStats> GetSessionStatsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Statistics about the session manager.
    /// </summary>
    public record SessionStats {
        /// <summary>
        /// Gets the number of active sessions.
        /// </summary>
        public int ActiveSessionCount { get; init; }

        /// <summary>
        /// Gets the total number of sessions created.
        /// </summary>
        public int TotalSessionCount { get; init; }

        /// <summary>
        /// Gets the maximum number of concurrent sessions.
        /// </summary>
        public int MaxSessionCount { get; init; }
    }
}
