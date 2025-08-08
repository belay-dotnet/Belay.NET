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
        private volatile bool disposed = false;

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
        public async Task CleanupResourcesAsync(CancellationToken cancellationToken = default) {
            this.ThrowIfDisposed();

            this.logger.LogInformation(
                "Cleaning up session resources: {ThreadCount} threads, {MethodCount} methods in session {SessionId}",
                this.backgroundThreads.Count,
                this.deployedMethods.Count,
                this.sessionId);

            // Note: Actual thread termination on device would be handled by the ThreadExecutor
            // This cleanup just removes tracking information
            var threadIds = this.backgroundThreads.Keys.ToArray();
            foreach (var threadId in threadIds) {
                await this.UnregisterBackgroundThreadAsync(threadId, cancellationToken).ConfigureAwait(false);
            }

            // Clear deployed methods (they remain on device but are no longer tracked)
            this.deployedMethods.Clear();

            this.logger.LogDebug("Completed resource cleanup for session {SessionId}", this.sessionId);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync() {
            if (this.disposed) {
                return;
            }

            this.disposed = true;

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

        private void ThrowIfDisposed() {
            if (this.disposed) {
                throw new ObjectDisposedException(nameof(ResourceTracker));
            }
        }
    }
}
