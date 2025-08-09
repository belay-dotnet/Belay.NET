// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    using System.Collections.Concurrent;
    using System.Security.Cryptography;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Tracks and manages resources associated with a device session.
    /// </summary>
    public sealed class ResourceTracker : IResourceTracker {
        private readonly ILogger<ResourceTracker> logger;
        private readonly string sessionId;
        private readonly ConcurrentDictionary<string, BackgroundThreadInfo> backgroundThreads = new();
        private readonly ConcurrentDictionary<string, DeployedMethodInfo> deployedMethods = new();
        private readonly ConcurrentDictionary<string, ISessionResource> sessionResources = new();
        private readonly object lockObject = new();
        private bool disposed = false;
        private int totalResourceCost = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceTracker"/> class.
        /// </summary>
        /// <param name="sessionId">The identifier of the session this tracker belongs to.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        public ResourceTracker(string sessionId, ILogger<ResourceTracker> logger) {
            this.sessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public Task RegisterBackgroundThreadAsync(
            string threadId,
            string methodName,
            CancellationToken cancellationToken = default) {
            this.ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(threadId)) {
                throw new ArgumentException("Thread ID cannot be null or whitespace", nameof(threadId));
            }

            if (string.IsNullOrWhiteSpace(methodName)) {
                throw new ArgumentException("Method name cannot be null or whitespace", nameof(methodName));
            }

            var threadInfo = new BackgroundThreadInfo {
                ThreadId = threadId,
                MethodName = methodName,
                RegisteredAt = DateTime.UtcNow,
                SessionId = this.sessionId,
            };

            this.backgroundThreads.AddOrUpdate(threadId, threadInfo, (key, existing) => threadInfo);

            this.logger.LogDebug(
                "Registered background thread {ThreadId} for method {MethodName} in session {SessionId}",
                threadId,
                methodName,
                this.sessionId);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task UnregisterBackgroundThreadAsync(
            string threadId,
            CancellationToken cancellationToken = default) {
            this.ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(threadId)) {
                return Task.CompletedTask;
            }

            if (this.backgroundThreads.TryRemove(threadId, out var threadInfo)) {
                this.logger.LogDebug(
                    "Unregistered background thread {ThreadId} for method {MethodName} in session {SessionId}",
                    threadId,
                    threadInfo.MethodName,
                    this.sessionId);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public IReadOnlyList<BackgroundThreadInfo> GetActiveThreads() {
            this.ThrowIfDisposed();
            return this.backgroundThreads.Values.ToArray();
        }

        /// <inheritdoc />
        public Task RegisterDeployedMethodAsync(
            string signature,
            byte[] codeHash,
            CancellationToken cancellationToken = default) {
            this.ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(signature)) {
                throw new ArgumentException("Signature cannot be null or whitespace", nameof(signature));
            }

            if (codeHash == null || codeHash.Length == 0) {
                throw new ArgumentException("Code hash cannot be null or empty", nameof(codeHash));
            }

            var methodKey = this.GenerateMethodKey(signature, codeHash);
            var methodInfo = new DeployedMethodInfo {
                Signature = signature,
                CodeHash = codeHash,
                DeployedAt = DateTime.UtcNow,
                SessionId = this.sessionId,
            };

            this.deployedMethods.AddOrUpdate(methodKey, methodInfo, (key, existing) => methodInfo);

            this.logger.LogDebug(
                "Registered deployed method {Signature} in session {SessionId}",
                signature,
                this.sessionId);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public bool IsMethodDeployed(string signature, byte[] codeHash) {
            this.ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(signature) || codeHash == null || codeHash.Length == 0) {
                return false;
            }

            var methodKey = this.GenerateMethodKey(signature, codeHash);
            return this.deployedMethods.ContainsKey(methodKey);
        }

        /// <inheritdoc />
        public IReadOnlyList<DeployedMethodInfo> GetDeployedMethods() {
            this.ThrowIfDisposed();
            return this.deployedMethods.Values.ToArray();
        }

        /// <inheritdoc />
        public Task RegisterResourceAsync(ISessionResource resource, CancellationToken cancellationToken = default) {
            if (resource == null) {
                throw new ArgumentNullException(nameof(resource));
            }

            this.ThrowIfDisposed();

            lock (this.lockObject) {
                if (this.sessionResources.ContainsKey(resource.ResourceId)) {
                    throw new InvalidOperationException($"Resource with ID '{resource.ResourceId}' is already registered");
                }

                this.sessionResources.TryAdd(resource.ResourceId, resource);
                lock (this.lockObject) {
                    this.totalResourceCost += resource.ResourceCost;
                }

                // Subscribe to state changes for monitoring
                resource.StateChanged += this.OnResourceStateChanged;
            }

            this.logger.LogDebug(
                "Registered session resource {ResourceId} of type {ResourceType} with cost {Cost}",
                resource.ResourceId, resource.ResourceType, resource.ResourceCost);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<ISessionResource>> GetActiveResourcesAsync(CancellationToken cancellationToken = default) {
            this.ThrowIfDisposed();

            var activeResources = this.sessionResources.Values
                .Where(r => r.State == ResourceState.Active || r.State == ResourceState.Initializing)
                .ToArray();

            return Task.FromResult<IReadOnlyList<ISessionResource>>(activeResources);
        }

        /// <inheritdoc />
        public ResourceUsageStats GetResourceStats() {
            this.ThrowIfDisposed();

            var resourcesByType = this.sessionResources.Values
                .GroupBy(r => r.ResourceType)
                .ToDictionary(g => g.Key, g => g.Count());

            return new ResourceUsageStats {
                TotalResources = this.sessionResources.Count,
                TotalResourceCost = this.GetTotalResourceCostThreadSafe(),
                ResourcesByType = resourcesByType,
                BackgroundThreads = this.backgroundThreads.Count,
                DeployedMethods = this.deployedMethods.Count,
            };
        }

        /// <inheritdoc />
        public async Task CleanupResourcesAsync(CancellationToken cancellationToken = default) {
            this.ThrowIfDisposed();

            this.logger.LogInformation(
                "Cleaning up session resources: {ResourceCount} resources, {ThreadCount} threads, {MethodCount} methods in session {SessionId}",
                this.sessionResources.Count,
                this.backgroundThreads.Count,
                this.deployedMethods.Count,
                this.sessionId);

            // Clean up session resources in reverse order of registration
            var resources = this.sessionResources.Values.OrderByDescending(r => r.CreatedAt).ToArray();
            var cleanupTasks = new List<Task>();

            foreach (var resource in resources) {
                try {
                    cleanupTasks.Add(this.CleanupResourceSafelyAsync(resource, cancellationToken));
                }
                catch (Exception ex) {
                    this.logger.LogError(ex, "Error initiating cleanup for resource {ResourceId}", resource.ResourceId);
                }
            }

            // Wait for all cleanup tasks to complete
            if (cleanupTasks.Count > 0) {
                try {
                    await Task.WhenAll(cleanupTasks).ConfigureAwait(false);
                }
                catch (Exception ex) {
                    this.logger.LogError(ex, "Error during resource cleanup in session {SessionId}", this.sessionId);
                }
            }

            // Clean up legacy resources
            var threadIds = this.backgroundThreads.Keys.ToArray();
            foreach (var threadId in threadIds) {
                await this.UnregisterBackgroundThreadAsync(threadId, cancellationToken).ConfigureAwait(false);
            }

            // Clear deployed methods (they remain on device but are no longer tracked)
            this.deployedMethods.Clear();
            this.sessionResources.Clear();
            lock (this.lockObject) {
                this.totalResourceCost = 0;
            }

            this.logger.LogDebug("Completed resource cleanup for session {SessionId}", this.sessionId);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync() {
            if (this.disposed) {
                return;
            }

            lock (this.lockObject) {
                this.disposed = true;
            }

            try {
                await this.CleanupResourcesAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) {
                this.logger.LogError(ex, "Error during resource tracker disposal for session {SessionId}", this.sessionId);
            }

            this.logger.LogDebug("Resource tracker disposed for session {SessionId}", this.sessionId);
        }

        private string GenerateMethodKey(string signature, byte[] codeHash) {
            // Create a unique key combining signature and code hash
            using var sha256 = SHA256.Create();
            var signatureBytes = System.Text.Encoding.UTF8.GetBytes(signature);
            var combinedBytes = new byte[signatureBytes.Length + codeHash.Length];
            Array.Copy(signatureBytes, 0, combinedBytes, 0, signatureBytes.Length);
            Array.Copy(codeHash, 0, combinedBytes, signatureBytes.Length, codeHash.Length);

            var hashBytes = sha256.ComputeHash(combinedBytes);
            return Convert.ToBase64String(hashBytes);
        }

        private async Task CleanupResourceSafelyAsync(ISessionResource resource, CancellationToken cancellationToken) {
            try {
                // Unsubscribe from events first - this must happen regardless of cleanup success
                try {
                    resource.StateChanged -= this.OnResourceStateChanged;
                }
                catch (Exception eventEx) {
                    this.logger.LogWarning(eventEx, "Error unsubscribing from resource {ResourceId} events", resource.ResourceId);
                }

                // Perform cleanup
                try {
                    await resource.CleanupAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception cleanupEx) {
                    this.logger.LogError(cleanupEx, "Error during cleanup for resource {ResourceId}", resource.ResourceId);
                }

                // Dispose resource
                try {
                    await resource.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception disposeEx) {
                    this.logger.LogError(disposeEx, "Error disposing resource {ResourceId}", resource.ResourceId);
                }

                this.logger.LogDebug("Resource cleanup completed for {ResourceId}", resource.ResourceId);
            }
            catch (Exception ex) {
                this.logger.LogError(ex, "Unexpected error during resource {ResourceId} cleanup", resource.ResourceId);
            }
            finally {
                // Remove from tracked resources and adjust cost regardless of cleanup success/failure
                if (this.sessionResources.TryRemove(resource.ResourceId, out var removedResource)) {
                    lock (this.lockObject) {
                        this.totalResourceCost -= removedResource.ResourceCost;
                    }
                }
            }
        }

        private void OnResourceStateChanged(object? sender, ResourceStateChangedEventArgs e) {
            this.logger.LogDebug(
                "Resource {ResourceId} state changed from {OldState} to {NewState}",
                e.ResourceId, e.OldState, e.NewState);

            // Log errors if resource transitioned to error state
            if (e.NewState == ResourceState.Error && e.Error != null) {
                this.logger.LogWarning(
                    e.Error,
                    "Resource {ResourceId} transitioned to error state",
                    e.ResourceId);
            }
        }

        /// <summary>
        /// Gets the total resource cost in a thread-safe manner.
        /// </summary>
        /// <returns>The total resource cost.</returns>
        private int GetTotalResourceCostThreadSafe() {
            lock (this.lockObject) {
                return this.totalResourceCost;
            }
        }

        /// <summary>
        /// Throws an exception if this resource tracker has been disposed.
        /// </summary>
        private void ThrowIfDisposed() {
            lock (this.lockObject) {
                if (this.disposed) {
                    throw new ObjectDisposedException(nameof(ResourceTracker));
                }
            }
        }
    }
}
