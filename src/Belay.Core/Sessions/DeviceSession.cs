// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    using Belay.Core.Communication;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Represents a single session context for device operations.
    /// </summary>
    public sealed class DeviceSession : IDeviceSession {
        private readonly ILogger<DeviceSession> logger;
        private volatile bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceSession"/> class.
        /// </summary>
        /// <param name="sessionId">The unique identifier for this session.</param>
        /// <param name="communication">The device communication instance.</param>
        /// <param name="loggerFactory">The logger factory for creating loggers.</param>
        /// <param name="deviceInfo">Optional device information.</param>
        public DeviceSession(
            string sessionId,
            IDeviceCommunication communication,
            ILoggerFactory loggerFactory,
            IDeviceInfo? deviceInfo = null) {
            if (string.IsNullOrWhiteSpace(sessionId)) {
                throw new ArgumentException("Session ID cannot be null or whitespace", nameof(sessionId));
            }

            if (communication == null) {
                throw new ArgumentNullException(nameof(communication));
            }

            if (loggerFactory == null) {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            this.SessionId = sessionId;
            this.CreatedAt = DateTime.UtcNow;
            this.logger = loggerFactory.CreateLogger<DeviceSession>();

            // Initialize session components
            this.State = new SessionState();
            this.Resources = new ResourceTracker(sessionId, loggerFactory.CreateLogger<ResourceTracker>());
            this.ExecutorContext = new ExecutorContext(sessionId, loggerFactory.CreateLogger<ExecutorContext>());
            this.DeviceContext = new DeviceContext(sessionId, communication, loggerFactory.CreateLogger<DeviceContext>(), deviceInfo);

            this.logger.LogDebug("Created device session {SessionId}", sessionId);
        }

        /// <inheritdoc />
        public string SessionId { get; }

        /// <inheritdoc />
        public DateTime CreatedAt { get; }

        /// <inheritdoc />
        public ISessionState State { get; }

        /// <inheritdoc />
        public IResourceTracker Resources { get; }

        /// <inheritdoc />
        public IExecutorContext ExecutorContext { get; }

        /// <inheritdoc />
        public IDeviceContext DeviceContext { get; }

        /// <inheritdoc />
        public bool IsActive => !this.disposed;

        /// <inheritdoc />
        public async ValueTask DisposeAsync() {
            if (this.disposed) {
                return;
            }

            this.disposed = true;

            this.logger.LogDebug("Disposing device session {SessionId}", this.SessionId);

            try {
                // Clean up resources first
                await this.Resources.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) {
                this.logger.LogError(ex, "Error disposing resources for session {SessionId}", this.SessionId);
            }

            try {
                // Clear session state
                this.State.Clear();
            }
            catch (Exception ex) {
                this.logger.LogError(ex, "Error clearing session state for session {SessionId}", this.SessionId);
            }

            this.logger.LogInformation("Disposed device session {SessionId}", this.SessionId);
        }
    }
}
