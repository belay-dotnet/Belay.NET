// Copyright (c) 2024 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for more information.
    /// Represents a single session context for device operations.
    /// Sessions provide isolation and resource tracking for operations.
    /// </summary>
    public interface IDeviceSession : IAsyncDisposable {
        /// <summary>
        /// Gets the unique identifier for this session.
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// Gets the timestamp when this session was created.
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// Gets the session state for storing operation-specific data.
        /// </summary>
        ISessionState State { get; }

        /// <summary>
        /// Gets the resource tracker for managing session resources.
        /// </summary>
        IResourceTracker Resources { get; }

        /// <summary>
        /// Gets the executor context for session-aware executor coordination.
        /// </summary>
        IExecutorContext ExecutorContext { get; }

        /// <summary>
        /// Gets the device context for device-specific session information.
        /// </summary>
        IDeviceContext DeviceContext { get; }

        /// <summary>
        /// Gets a value indicating whether this session is still active.
        /// </summary>
        bool IsActive { get; }
    }
}
