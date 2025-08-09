// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    /// <summary>
    /// Represents a resource that is managed by a device session.
    /// Session resources are automatically tracked and cleaned up when the session ends.
    /// </summary>
    public interface ISessionResource : IAsyncDisposable {
        /// <summary>
        /// Gets the unique identifier for this resource.
        /// </summary>
        string ResourceId { get; }

        /// <summary>
        /// Gets the type of resource (e.g., "DeployedMethod", "BackgroundThread", "FileHandle").
        /// </summary>
        string ResourceType { get; }

        /// <summary>
        /// Gets the current state of the resource.
        /// </summary>
        ResourceState State { get; }

        /// <summary>
        /// Gets metadata about the resource for tracking and debugging.
        /// </summary>
        IReadOnlyDictionary<string, object> Metadata { get; }

        /// <summary>
        /// Gets the timestamp when this resource was created.
        /// </summary>
        DateTime CreatedAt { get; }

        /// <summary>
        /// Gets the size or cost of this resource in arbitrary units.
        /// Used for resource limit enforcement.
        /// </summary>
        int ResourceCost { get; }

        /// <summary>
        /// Initializes the resource and performs any necessary setup.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous initialization.</returns>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs cleanup of the resource before disposal.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous cleanup.</returns>
        Task CleanupAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Occurs when the resource state changes.
        /// </summary>
        event EventHandler<ResourceStateChangedEventArgs>? StateChanged;
    }

    /// <summary>
    /// Represents the possible states of a session resource.
    /// </summary>
    public enum ResourceState {
        /// <summary>
        /// Resource is being created but not yet ready.
        /// </summary>
        Initializing,

        /// <summary>
        /// Resource is active and ready for use.
        /// </summary>
        Active,

        /// <summary>
        /// Resource is temporarily suspended.
        /// </summary>
        Suspended,

        /// <summary>
        /// Resource is being cleaned up.
        /// </summary>
        Disposing,

        /// <summary>
        /// Resource has been disposed and is no longer usable.
        /// </summary>
        Disposed,

        /// <summary>
        /// Resource is in an error state.
        /// </summary>
        Error,
    }

    /// <summary>
    /// Event arguments for resource state changes.
    /// </summary>
    public class ResourceStateChangedEventArgs : EventArgs {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceStateChangedEventArgs"/> class.
        /// </summary>
        /// <param name="resourceId">The ID of the resource that changed state.</param>
        /// <param name="oldState">The previous state.</param>
        /// <param name="newState">The new state.</param>
        /// <param name="error">Optional error that caused the state change.</param>
        public ResourceStateChangedEventArgs(string resourceId, ResourceState oldState, ResourceState newState, Exception? error = null) {
            this.ResourceId = resourceId ?? throw new ArgumentNullException(nameof(resourceId));
            this.OldState = oldState;
            this.NewState = newState;
            this.Error = error;
        }

        /// <summary>
        /// Gets the ID of the resource that changed state.
        /// </summary>
        public string ResourceId { get; }

        /// <summary>
        /// Gets the previous state.
        /// </summary>
        public ResourceState OldState { get; }

        /// <summary>
        /// Gets the new state.
        /// </summary>
        public ResourceState NewState { get; }

        /// <summary>
        /// Gets the error that caused the state change, if any.
        /// </summary>
        public Exception? Error { get; }
    }

    /// <summary>
    /// Base implementation of a session resource with common functionality.
    /// </summary>
    public abstract class SessionResourceBase : ISessionResource {
        private readonly Dictionary<string, object> metadata = new();
        private volatile ResourceState state = ResourceState.Initializing;
        private volatile bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="SessionResourceBase"/> class.
        /// </summary>
        /// <param name="resourceId">The unique identifier for this resource.</param>
        /// <param name="resourceType">The type of resource.</param>
        /// <param name="resourceCost">The cost of this resource.</param>
        protected SessionResourceBase(string resourceId, string resourceType, int resourceCost = 1) {
            if (string.IsNullOrWhiteSpace(resourceId)) {
                throw new ArgumentException("Resource ID cannot be null or whitespace", nameof(resourceId));
            }

            if (string.IsNullOrWhiteSpace(resourceType)) {
                throw new ArgumentException("Resource type cannot be null or whitespace", nameof(resourceType));
            }

            this.ResourceId = resourceId;
            this.ResourceType = resourceType;
            this.ResourceCost = Math.Max(1, resourceCost);
            this.CreatedAt = DateTime.UtcNow;
        }

        /// <inheritdoc />
        public string ResourceId { get; }

        /// <inheritdoc />
        public string ResourceType { get; }

        /// <inheritdoc />
        public ResourceState State => this.state;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, object> Metadata => this.metadata.AsReadOnly();

        /// <inheritdoc />
        public DateTime CreatedAt { get; }

        /// <inheritdoc />
        public int ResourceCost { get; }

        /// <inheritdoc />
        public event EventHandler<ResourceStateChangedEventArgs>? StateChanged;

        /// <inheritdoc />
        public virtual async Task InitializeAsync(CancellationToken cancellationToken = default) {
            this.ThrowIfDisposed();

            try {
                await this.InitializeResourceAsync(cancellationToken).ConfigureAwait(false);
                this.TransitionState(ResourceState.Active);
            }
            catch (Exception ex) {
                this.TransitionState(ResourceState.Error, ex);
                throw;
            }
        }

        /// <inheritdoc />
        public virtual async Task CleanupAsync(CancellationToken cancellationToken = default) {
            if (this.state == ResourceState.Disposed || this.state == ResourceState.Disposing) {
                return;
            }

            this.TransitionState(ResourceState.Disposing);

            try {
                await this.CleanupResourceAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) {
                this.TransitionState(ResourceState.Error, ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync() {
            if (this.disposed) {
                return;
            }

            this.disposed = true;

            try {
                await this.CleanupAsync().ConfigureAwait(false);
            }
            finally {
                this.TransitionState(ResourceState.Disposed);
            }
        }

        /// <summary>
        /// Sets metadata for this resource.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value.</param>
        protected void SetMetadata(string key, object value) {
            this.metadata[key] = value;
        }

        /// <summary>
        /// Transitions the resource to a new state and raises the StateChanged event.
        /// </summary>
        /// <param name="newState">The new state.</param>
        /// <param name="error">Optional error that caused the transition.</param>
        protected void TransitionState(ResourceState newState, Exception? error = null) {
            var oldState = this.state;
            this.state = newState;

            this.StateChanged?.Invoke(this, new ResourceStateChangedEventArgs(this.ResourceId, oldState, newState, error));
        }

        /// <summary>
        /// Performs the actual resource initialization. Override in derived classes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous initialization.</returns>
        protected abstract Task InitializeResourceAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Performs the actual resource cleanup. Override in derived classes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous cleanup.</returns>
        protected abstract Task CleanupResourceAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Throws an exception if the resource has been disposed.
        /// </summary>
        protected void ThrowIfDisposed() {
            if (this.disposed) {
                throw new ObjectDisposedException(this.GetType().Name);
            }
        }
    }
}
