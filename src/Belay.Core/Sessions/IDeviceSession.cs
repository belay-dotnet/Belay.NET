// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    /// <summary>
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
        /// Gets the file system context for session-aware file operations.
        /// </summary>
        IFileSystemContext FileSystemContext { get; }

        /// <summary>
        /// Gets a value indicating whether this session is still active.
        /// </summary>
        bool IsActive { get; }
    }
}
