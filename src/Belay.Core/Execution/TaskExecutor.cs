// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System;
    using System.Collections.Concurrent;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Belay.Attributes;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Executor that applies [Task] attribute policies around Python code execution.
    /// Handles timeout, caching, and exclusive execution policies.
    /// </summary>
    public sealed class TaskExecutor : BaseExecutor, IDisposable {
        private readonly SemaphoreSlim exclusiveExecutionSemaphore;
        private readonly ConcurrentDictionary<string, object?> resultCache;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute Python code on.</param>
        /// <param name="sessionManager">The session manager for device coordination.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        public TaskExecutor(Device device, Belay.Core.Sessions.IDeviceSessionManager sessionManager, ILogger<TaskExecutor> logger)
            : base(device, sessionManager, logger) {
            this.exclusiveExecutionSemaphore = new SemaphoreSlim(1, 1);
            this.resultCache = new ConcurrentDictionary<string, object?>();
        }

        /// <summary>
        /// Applies [Task] attribute policies around Python code execution.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute on the device.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="callingMethod">The method that initiated this execution.</param>
        /// <returns>The result of the Python code execution with policies applied.</returns>
        public override async Task<T> ApplyPoliciesAndExecuteAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? callingMethod = null) {
            this.ThrowIfDisposed();

            // Get the calling method to extract [Task] attribute
            var method = this.GetCallingMethod(3); // Skip ApplyPoliciesAndExecuteAsync, calling method, and ExecuteAsync
            var taskAttribute = method?.GetAttribute<TaskAttribute>();

            if (taskAttribute == null) {
                // No [Task] attribute found, execute directly without policies
                this.Logger.LogDebug("No [Task] attribute found, executing without policies");
                return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken).ConfigureAwait(false);
            }

            this.Logger.LogDebug(
                "Applying [Task] policies for method {MethodName}: Timeout={TimeoutMs}ms, Cache={Cache}, Exclusive={Exclusive}",
                method?.Name ?? callingMethod, taskAttribute.TimeoutMs, taskAttribute.Cache, taskAttribute.Exclusive);

            // Check cache first if caching is enabled
            string? cacheKey = null;
            if (taskAttribute.Cache) {
                cacheKey = GenerateCacheKey(pythonCode);
                if (this.resultCache.TryGetValue(cacheKey, out var cachedResult)) {
                    this.Logger.LogDebug("Returning cached result for method {MethodName}", method?.Name ?? callingMethod);
                    return ConvertResult<T>(cachedResult);
                }
            }

            try {
                // Apply timeout from attribute if specified
                using var timeoutCts = CreateTimeoutCts(taskAttribute.TimeoutMs);
                var effectiveCancellationToken = CombineCancellationTokens(cancellationToken, timeoutCts);

                // Handle exclusive execution
                T result;
                if (taskAttribute.Exclusive) {
                    result = await this.ExecuteExclusiveAsync<T>(pythonCode, effectiveCancellationToken, method?.Name ?? callingMethod).ConfigureAwait(false);
                }
                else {
                    result = await this.ExecuteOnDeviceAsync<T>(pythonCode, effectiveCancellationToken).ConfigureAwait(false);
                }

                // Cache result if caching is enabled
                if (taskAttribute.Cache && cacheKey != null) {
                    this.resultCache.TryAdd(cacheKey, result);
                    this.Logger.LogDebug("Cached result for method {MethodName}", method?.Name ?? callingMethod);
                }

                this.Logger.LogDebug("[Task] method {MethodName} completed successfully", method?.Name ?? callingMethod);
                return result;
            }
            catch (OperationCanceledException) when (taskAttribute.TimeoutMs.HasValue) {
                var methodName = method?.Name ?? callingMethod;
                this.Logger.LogWarning("[Task] method {MethodName} timed out after {TimeoutMs}ms", methodName, taskAttribute.TimeoutMs);
                throw new TimeoutException($"Task method {methodName} timed out after {taskAttribute.TimeoutMs}ms");
            }
            catch (Exception ex) {
                this.Logger.LogError(ex, "[Task] method {MethodName} failed", method?.Name ?? callingMethod);
                throw;
            }
        }

        /// <summary>
        /// Executes Python code with exclusive access to ensure no other exclusive methods run concurrently.
        /// </summary>
        private async Task<T> ExecuteExclusiveAsync<T>(string pythonCode, CancellationToken cancellationToken, string methodName) {
            this.Logger.LogDebug("Acquiring exclusive lock for method {MethodName}", methodName);

            await this.exclusiveExecutionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try {
                this.Logger.LogDebug("Executing method {MethodName} in exclusive mode", methodName);
                return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken).ConfigureAwait(false);
            }
            finally {
                this.exclusiveExecutionSemaphore.Release();
                this.Logger.LogDebug("Released exclusive lock for method {MethodName}", methodName);
            }
        }

        /// <summary>
        /// Checks if a method has the [Task] attribute.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <returns>True if the method has the [Task] attribute.</returns>
        public bool CanHandle(MethodInfo method) {
            return method.HasAttribute<TaskAttribute>();
        }

        /// <summary>
        /// Clears the result cache.
        /// </summary>
        public void ClearCache() {
            this.ThrowIfDisposed();
            var count = this.resultCache.Count;
            this.resultCache.Clear();
            this.Logger.LogDebug("Cleared {Count} cached [Task] results", count);
        }

        /// <summary>
        /// Gets the number of cached results.
        /// </summary>
        /// <returns></returns>
        public int GetCacheSize() {
            this.ThrowIfDisposed();
            return this.resultCache.Count;
        }

        /// <summary>
        /// Gets statistics about the exclusive execution usage.
        /// </summary>
        /// <returns></returns>
        public (int CurrentCount, int MaxCount) GetExclusiveExecutionStats() {
            this.ThrowIfDisposed();
            return (this.exclusiveExecutionSemaphore.CurrentCount, 1); // Max count is always 1 for exclusive
        }

        /// <summary>
        /// Gets the methods that have been executed.
        /// </summary>
        /// <returns>A collection of executed method names.</returns>
        public Task<IReadOnlyCollection<string>> GetExecutedMethodsAsync() {
            this.ThrowIfDisposed();
            // Since we don't track method names in this implementation, return an empty collection
            return Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());
        }

        /// <inheritdoc />
        public void Dispose() {
            if (this.disposed) {
                return;
            }

            try {
                this.exclusiveExecutionSemaphore?.Dispose();
                this.resultCache?.Clear();

                this.Logger.LogDebug("TaskExecutor disposed");
            }
            catch (Exception ex) {
                this.Logger.LogWarning(ex, "Error during TaskExecutor disposal");
            }
            finally {
                this.disposed = true;
            }
        }

        private void ThrowIfDisposed() {
            if (this.disposed) {
                throw new ObjectDisposedException(nameof(TaskExecutor));
            }
        }
    }
}
