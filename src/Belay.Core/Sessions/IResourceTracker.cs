// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    /// <summary>
    /// Information about a background thread running on the device.
    /// </summary>
    public sealed record BackgroundThreadInfo {
        /// <summary>
        /// Gets the unique identifier of the thread.
        /// </summary>
        public required string ThreadId { get; init; }

        /// <summary>
        /// Gets the name of the method that created the thread.
        /// </summary>
        public required string MethodName { get; init; }

        /// <summary>
        /// Gets the timestamp when the thread was registered.
        /// </summary>
        public required DateTime RegisteredAt { get; init; }

        /// <summary>
        /// Gets the session that owns this thread.
        /// </summary>
        public required string SessionId { get; init; }
    }

    /// <summary>
    /// Information about a deployed method on the device.
    /// </summary>
    public sealed record DeployedMethodInfo {
        /// <summary>
        /// Gets the method signature.
        /// </summary>
        public required string Signature { get; init; }

        /// <summary>
        /// Gets the hash of the deployed code.
        /// </summary>
        public required byte[] CodeHash { get; init; }

        /// <summary>
        /// Gets the timestamp when the method was deployed.
        /// </summary>
        public required DateTime DeployedAt { get; init; }

        /// <summary>
        /// Gets the session that deployed this method.
        /// </summary>
        public required string SessionId { get; init; }
    }

    /// <summary>
    /// Resource usage statistics for a session.
    /// </summary>
    public record ResourceUsageStats {
        /// <summary>
        /// Gets the total number of resources.
        /// </summary>
        public int TotalResources { get; init; }

        /// <summary>
        /// Gets the total resource cost.
        /// </summary>
        public int TotalResourceCost { get; init; }

        /// <summary>
        /// Gets the number of resources by type.
        /// </summary>
        public IReadOnlyDictionary<string, int> ResourcesByType { get; init; } = new Dictionary<string, int>();

        /// <summary>
        /// Gets the number of background threads.
        /// </summary>
        public int BackgroundThreads { get; init; }

        /// <summary>
        /// Gets the number of deployed methods.
        /// </summary>
        public int DeployedMethods { get; init; }
    }

    /// <summary>
    /// Tracks resources associated with a device session.
    /// </summary>
    public interface IResourceTracker : IAsyncDisposable {
        /// <summary>
        /// Registers a background thread with the session.
        /// </summary>
        /// <param name="threadId">The unique identifier of the thread.</param>
        /// <param name="methodName">The name of the method that created the thread.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task RegisterBackgroundThreadAsync(
            string threadId,
            string methodName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Unregisters a background thread from the session.
        /// </summary>
        /// <param name="threadId">The unique identifier of the thread to unregister.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task UnregisterBackgroundThreadAsync(
            string threadId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets information about all active background threads in this session.
        /// </summary>
        /// <returns>A collection of active background thread information.</returns>
        IReadOnlyList<BackgroundThreadInfo> GetActiveThreads();

        /// <summary>
        /// Registers a deployed method with the session.
        /// </summary>
        /// <param name="signature">The method signature.</param>
        /// <param name="codeHash">The hash of the deployed code.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task RegisterDeployedMethodAsync(
            string signature,
            byte[] codeHash,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether a method with the specified signature and code hash is already deployed.
        /// </summary>
        /// <param name="signature">The method signature.</param>
        /// <param name="codeHash">The hash of the code to check.</param>
        /// <returns>True if the method is already deployed; otherwise, false.</returns>
        bool IsMethodDeployed(string signature, byte[] codeHash);

        /// <summary>
        /// Gets information about all deployed methods in this session.
        /// </summary>
        /// <returns>A collection of deployed method information.</returns>
        IReadOnlyList<DeployedMethodInfo> GetDeployedMethods();

        /// <summary>
        /// Registers a session resource for tracking and automatic cleanup.
        /// </summary>
        /// <param name="resource">The session resource to register.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RegisterResourceAsync(ISessionResource resource, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all active session resources.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A collection of active session resources.</returns>
        Task<IReadOnlyList<ISessionResource>> GetActiveResourcesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets resource usage statistics for this session.
        /// </summary>
        /// <returns>Resource usage statistics.</returns>
        ResourceUsageStats GetResourceStats();

        /// <summary>
        /// Cleans up all resources tracked by this session.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task CleanupResourcesAsync(CancellationToken cancellationToken = default);
    }
}
