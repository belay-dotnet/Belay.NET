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
    using Belay.Core.Caching;
    using Belay.Core.Exceptions;
    using Belay.Core.Transactions;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Executor that applies [Task] attribute policies around Python code execution.
    /// Handles timeout, caching, and exclusive execution policies.
    /// </summary>
    public sealed class TaskExecutor : BaseExecutor, IDisposable {
        private readonly ReaderWriterLockSlim executionLock;
        private readonly IMethodDeploymentCache methodCache;
        private readonly ITransactionManager transactionManager;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute Python code on.</param>
        /// <param name="sessionManager">The session manager for device coordination.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="errorMapper">Optional error mapper for exception handling.</param>
        /// <param name="cache">Optional method deployment cache for performance optimization.</param>
        /// <param name="executionContextService">Optional execution context service.</param>
        /// <param name="transactionManager">Optional transaction manager for ensuring consistency.</param>
        public TaskExecutor(Device device, Belay.Core.Sessions.IDeviceSessionManager sessionManager, ILogger<TaskExecutor> logger, IErrorMapper? errorMapper = null, IMethodDeploymentCache? cache = null, IExecutionContextService? executionContextService = null, ITransactionManager? transactionManager = null)
            : base(device, sessionManager, logger, errorMapper, executionContextService) {
            this.executionLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            this.methodCache = cache ?? new MethodDeploymentCache(new MethodCacheConfiguration(), logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<MethodDeploymentCache>.Instance);
            this.transactionManager = transactionManager ?? new TransactionManager(Microsoft.Extensions.Logging.Abstractions.NullLogger<TransactionManager>.Instance);
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

            // Get the execution context to extract [Task] attribute (secure replacement for stack inspection)
            var context = this.GetCurrentMethodContext();
            var taskAttribute = context?.TaskAttribute;
            var methodName = context?.MethodName ?? callingMethod ?? "Unknown";

            if (taskAttribute == null) {
                // No [Task] attribute found, execute directly without policies
                this.Logger.LogDebug("No [Task] attribute found for method {MethodName}, executing without policies", methodName);
                return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken).ConfigureAwait(false);
            }

            this.Logger.LogDebug(
                "Applying [Task] policies for method {MethodName}: Timeout={TimeoutMs}ms, Cache={Cache}, Exclusive={Exclusive}",
                methodName, taskAttribute.TimeoutMs, taskAttribute.Cache, taskAttribute.Exclusive);

            // Check cache first if caching is enabled
            MethodCacheKey? cacheKey = null;
            if (taskAttribute.Cache) {
                cacheKey = this.GenerateCacheKey(pythonCode, methodName);
                var cachedResult = this.methodCache.Get<T>(cacheKey);
                if (cachedResult != null) {
                    this.Logger.LogDebug("Returning cached result for method {MethodName}", methodName);
                    return cachedResult;
                }
            }

            // Execute within transaction boundary to ensure consistency between device operations and caching
            return await this.transactionManager.ExecuteInTransactionAsync(async transaction => {
                try {
                    // Apply timeout from attribute if specified
                    using var timeoutCts = CreateTimeoutCts(taskAttribute.TimeoutMs);
                    var effectiveCancellationToken = CombineCancellationTokens(cancellationToken, timeoutCts, out var linkedCts);

                    using (linkedCts) {
                        // Handle exclusive/non-exclusive execution using reader-writer lock
                        T result;
                        if (taskAttribute.Exclusive) {
                            result = await this.ExecuteExclusiveAsync<T>(pythonCode, effectiveCancellationToken, methodName).ConfigureAwait(false);
                        }
                        else
                        {
                            result = await this.ExecuteNonExclusiveAsync<T>(pythonCode, effectiveCancellationToken, methodName).ConfigureAwait(false);
                        }

                        // Cache result if caching is enabled - register compensating action for rollback
                        if (taskAttribute.Cache && cacheKey != null) {
                            this.methodCache.Set(cacheKey, result, expiresAfter: null);
                            this.Logger.LogDebug("Cached result for method {MethodName}", methodName);

                            // Register compensating action to remove cache entry on rollback
                            transaction.RegisterCompensatingAction(
                                async _ => {
                                    this.methodCache.Remove(cacheKey);
                                    this.Logger.LogDebug("Rolled back cache entry for method {MethodName}", methodName);
                                },
                                $"Remove cache entry for method {methodName}");
                        }

                        this.Logger.LogDebug("[Task] method {MethodName} completed successfully", methodName);
                        return result;
                    }
                }
                catch (OperationCanceledException) when (taskAttribute.TimeoutMs.HasValue) {
                    this.Logger.LogWarning("[Task] method {MethodName} timed out after {TimeoutMs}ms", methodName, taskAttribute.TimeoutMs);
                    throw new TimeoutException($"Task method {methodName} timed out after {taskAttribute.TimeoutMs}ms");
                }
                catch (Exception ex) {
                    this.Logger.LogError(ex, "[Task] method {MethodName} failed within transaction", methodName);
                    throw;
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes Python code with exclusive access, preventing all other methods (both exclusive and non-exclusive) from running concurrently.
        /// Uses writer lock to ensure complete isolation.
        /// </summary>
        private async Task<T> ExecuteExclusiveAsync<T>(string pythonCode, CancellationToken cancellationToken, string methodName) {
            this.Logger.LogDebug("Acquiring exclusive (writer) lock for method {MethodName}", methodName);

            // For exclusive methods, we need a writer lock that blocks everything
            // We need to handle this carefully with async/await
            return await Task.Run(async () => {
                this.executionLock.EnterWriteLock();
                try {
                    this.Logger.LogDebug("Executing method {MethodName} in exclusive mode (writer lock acquired)", methodName);
                    return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken).ConfigureAwait(false);
                }
                finally {
                    this.executionLock.ExitWriteLock();
                    this.Logger.LogDebug("Released exclusive (writer) lock for method {MethodName}", methodName);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes Python code with non-exclusive access, allowing concurrent execution with other non-exclusive methods.
        /// Uses reader lock to allow concurrency while preventing exclusive methods from interfering.
        /// </summary>
        private async Task<T> ExecuteNonExclusiveAsync<T>(string pythonCode, CancellationToken cancellationToken, string methodName) {
            this.Logger.LogDebug("Acquiring non-exclusive (reader) lock for method {MethodName}", methodName);

            // For non-exclusive methods, we use a reader lock that allows concurrency with other readers
            // but blocks when a writer (exclusive method) is active
            return await Task.Run(async () => {
                this.executionLock.EnterReadLock();
                try {
                    this.Logger.LogDebug("Executing method {MethodName} in non-exclusive mode (reader lock acquired)", methodName);
                    return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken).ConfigureAwait(false);
                }
                finally {
                    this.executionLock.ExitReadLock();
                    this.Logger.LogDebug("Released non-exclusive (reader) lock for method {MethodName}", methodName);
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates that a method can be handled by this executor.
        /// </summary>
        /// <param name="method">The method to validate.</param>
        /// <returns>True if the method has a [Task] attribute, false otherwise.</returns>
        public override bool CanHandle(MethodInfo method) {
            return method?.HasAttribute<TaskAttribute>() == true;
        }

        /// <summary>
        /// Clears the result cache.
        /// </summary>
        public void ClearCache() {
            this.ThrowIfDisposed();
            var stats = this.methodCache.GetStatistics();
            this.methodCache.ClearAll();
            this.Logger.LogDebug("Cleared {Count} cached [Task] results", stats.CurrentEntryCount);
        }

        /// <summary>
        /// Gets statistics about the cache.
        /// </summary>
        /// <returns>Cache statistics.</returns>
        public CacheStatistics GetCacheStatistics() {
            this.ThrowIfDisposed();
            return this.methodCache.GetStatistics();
        }

        /// <summary>
        /// Gets statistics about the exclusive execution usage.
        /// </summary>
        /// <returns>Statistics about current lock state and usage.</returns>
        public (bool IsWriteLockHeld, int CurrentReaderCount, int WaitingReaderCount, int WaitingWriterCount) GetExclusiveExecutionStats() {
            this.ThrowIfDisposed();
            return (
                this.executionLock.IsWriteLockHeld,
                this.executionLock.CurrentReadCount,
                this.executionLock.WaitingReadCount,
                this.executionLock.WaitingWriteCount
            );
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

        /// <summary>
        /// Generates a cache key for the given Python code and method name.
        /// </summary>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="methodName">The method name that initiated this execution.</param>
        /// <returns>A cache key for the method execution.</returns>
        private MethodCacheKey GenerateCacheKey(string pythonCode, string methodName) {
            // Get device-specific identification information
            var (deviceId, firmwareVersion) = this.Device.GetDeviceIdentification();

            // Create a signature from the Python code and method name
            var methodSignature = $"{methodName}:{pythonCode.GetHashCode():X8}";

            return new MethodCacheKey(deviceId, firmwareVersion, methodSignature);
        }

        /// <inheritdoc />
        public void Dispose() {
            if (this.disposed) {
                return;
            }

            try {
                this.executionLock?.Dispose();
                this.methodCache?.Dispose();

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
