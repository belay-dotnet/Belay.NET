// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    using System.Collections.Concurrent;
    using Belay.Core.Communication;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Manages device sessions and provides session coordination.
    /// </summary>
    public sealed class DeviceSessionManager : IDeviceSessionManager {
        private readonly IDeviceCommunication communication;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<DeviceSessionManager> logger;
        private readonly ConcurrentDictionary<string, DeviceSession> activeSessions = new();
        private readonly SemaphoreSlim sessionLock = new(1, 1);

        private volatile DeviceSessionState state = DeviceSessionState.Active;
        private volatile string? currentSessionId;
        private volatile bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceSessionManager"/> class.
        /// </summary>
        /// <param name="communication">The device communication instance.</param>
        /// <param name="loggerFactory">The logger factory for creating loggers.</param>
        public DeviceSessionManager(IDeviceCommunication communication, ILoggerFactory loggerFactory) {
            this.communication = communication ?? throw new ArgumentNullException(nameof(communication));
            this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            this.logger = loggerFactory.CreateLogger<DeviceSessionManager>();

            this.logger.LogDebug("Device session manager created");
        }

        /// <inheritdoc />
        public string? CurrentSessionId => this.currentSessionId;

        /// <inheritdoc />
        public DeviceSessionState State => this.state;

        /// <inheritdoc />
        public async Task<IDeviceSession> CreateSessionAsync(CancellationToken cancellationToken = default) {
            this.ThrowIfDisposed();

            if (this.state != DeviceSessionState.Active) {
                throw new InvalidOperationException($"Cannot create session when manager state is {this.state}");
            }

            await this.sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                var sessionId = this.GenerateSessionId();
                var session = new DeviceSession(
                    sessionId,
                    this.communication,
                    this.loggerFactory,
                    await this.GetDeviceInfoAsync(cancellationToken).ConfigureAwait(false));

                this.activeSessions.TryAdd(sessionId, session);
                this.currentSessionId = sessionId;

                this.logger.LogInformation("Created session {SessionId}", sessionId);
                return session;
            }
            finally {
                this.sessionLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IDeviceSession> GetOrCreateSessionAsync(CancellationToken cancellationToken = default) {
            this.ThrowIfDisposed();

            // Check if we have a current active session
            var currentId = this.currentSessionId;
            if (!string.IsNullOrEmpty(currentId) &&
                this.activeSessions.TryGetValue(currentId, out var existingSession) &&
                existingSession.IsActive) {
                return existingSession;
            }

            // Create a new session
            return await this.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task EndSessionAsync(string sessionId, CancellationToken cancellationToken = default) {
            this.ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(sessionId)) {
                return;
            }

            if (!this.activeSessions.TryRemove(sessionId, out var session)) {
                this.logger.LogWarning("Attempted to end non-existent session {SessionId}", sessionId);
                return;
            }

            try {
                await session.DisposeAsync().ConfigureAwait(false);
                this.logger.LogInformation("Ended session {SessionId}", sessionId);
            }
            catch (Exception ex) {
                this.logger.LogError(ex, "Error ending session {SessionId}", sessionId);
                throw;
            }
            finally {
                // Clear current session if it was the one being ended
                if (this.currentSessionId == sessionId) {
                    this.currentSessionId = null;
                }
            }
        }

        /// <inheritdoc />
        public async Task<T> ExecuteInSessionAsync<T>(
            Func<IDeviceSession, Task<T>> operation,
            CancellationToken cancellationToken = default) {
            if (operation == null) {
                throw new ArgumentNullException(nameof(operation));
            }

            this.ThrowIfDisposed();

            var session = await this.GetOrCreateSessionAsync(cancellationToken).ConfigureAwait(false);

            try {
                return await operation(session).ConfigureAwait(false);
            }
            catch (Exception ex) {
                this.logger.LogError(ex, "Error executing operation in session {SessionId}", session.SessionId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ExecuteInSessionAsync(
            Func<IDeviceSession, Task> operation,
            CancellationToken cancellationToken = default) {
            await this.ExecuteInSessionAsync(
                async session => {
                    await operation(session).ConfigureAwait(false);
                    return true; // Return value not used
                }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task<SessionStats> GetSessionStatsAsync(CancellationToken cancellationToken = default) {
            this.ThrowIfDisposed();

            var stats = new SessionStats {
                ActiveSessionCount = this.activeSessions.Count,
                TotalSessionCount = this.activeSessions.Count, // Simplified - would track total in real implementation
                MaxSessionCount = 10, // From configuration - would be configurable
            };

            return Task.FromResult(stats);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync() {
            if (this.disposed) {
                return;
            }

            this.disposed = true;
            this.state = DeviceSessionState.Shutdown;

            this.logger.LogInformation(
                "Shutting down session manager with {ActiveSessions} active sessions",
                this.activeSessions.Count);

            try {
                // End all active sessions
                var sessionIds = this.activeSessions.Keys.ToArray();
                var endTasks = sessionIds.Select(sessionId =>
                    this.EndSessionAsync(sessionId, CancellationToken.None));

                await Task.WhenAll(endTasks).ConfigureAwait(false);
            }
            catch (Exception ex) {
                this.logger.LogError(ex, "Error during session manager disposal");
            }
            finally {
                this.sessionLock.Dispose();
                this.state = DeviceSessionState.Disposed;
                this.logger.LogDebug("Session manager disposed");
            }
        }

        private string GenerateSessionId() {
            // Generate a unique session identifier
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var random = Guid.NewGuid().ToString("N")[..8];
            return $"session_{timestamp}_{random}";
        }

        private Task<IDeviceInfo?> GetDeviceInfoAsync(CancellationToken cancellationToken = default) {
            try {
                // Attempt to gather device information
                // This is a simplified implementation - in practice you might query the device
                if (this.communication.State != DeviceConnectionState.Connected) {
                    return Task.FromResult<IDeviceInfo?>(null);
                }

                // Basic device info that could be expanded with actual device queries
                var deviceInfo = new DeviceInfo {
                    Platform = "micropython", // Would be determined by querying device
                    Version = "unknown",
                    Hardware = null,
                    UniqueId = null,
                    SupportsThreading = true, // Assume MicroPython supports threading
                    SupportsFileSystem = true, // Assume file system support
                    AvailableMemory = null,
                };

                return Task.FromResult<IDeviceInfo?>(deviceInfo);
            }
            catch (Exception ex) {
                this.logger.LogWarning(ex, "Failed to gather device information");
                return Task.FromResult<IDeviceInfo?>(null);
            }
        }

        private void ThrowIfDisposed() {
            if (this.disposed) {
                throw new ObjectDisposedException(nameof(DeviceSessionManager));
            }
        }
    }
}
