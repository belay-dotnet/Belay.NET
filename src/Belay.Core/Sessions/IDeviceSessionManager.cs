// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
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
        /// Creates a new session for device operations.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A new device session.</returns>
        Task<IDeviceSession> CreateSessionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current session or creates a new one if none exists.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The current or a new device session.</returns>
        Task<IDeviceSession> GetOrCreateSessionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Ends the specified session and cleans up its resources.
        /// </summary>
        /// <param name="sessionId">The identifier of the session to end.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        Task EndSessionAsync(string sessionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an operation within a session context, creating one if necessary.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="operation">The operation to execute within the session.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The result of the operation.</returns>
        Task<T> ExecuteInSessionAsync<T>(
            Func<IDeviceSession, Task<T>> operation,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an operation within a session context, creating one if necessary.
        /// </summary>
        /// <param name="operation">The operation to execute within the session.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        Task ExecuteInSessionAsync(
            Func<IDeviceSession, Task> operation,
            CancellationToken cancellationToken = default);
    }
}
