// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System.Collections.Concurrent;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Belay.Attributes;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Executor for methods decorated with the [Setup] attribute.
    /// Applies setup-specific policies such as one-time execution and ordered initialization.
    /// </summary>
    public sealed class SetupExecutor : BaseExecutor, IDisposable {
        private readonly ConcurrentDictionary<string, bool> executedSetupMethods;
        private readonly SemaphoreSlim setupSemaphore;

        /// <summary>
        /// Initializes a new instance of the <see cref="SetupExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute code on.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        public SetupExecutor(Device device, ILogger<SetupExecutor> logger)
            : base(device, logger) {
            this.executedSetupMethods = new ConcurrentDictionary<string, bool>();
            this.setupSemaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Applies setup policies and executes the Python code.
        /// Setup methods are executed only once per device session with proper ordering.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
        /// <param name="callingMethod">The name of the calling method for identification.</param>
        /// <returns>The result of the Python code execution.</returns>
        public override async Task<T> ApplyPoliciesAndExecuteAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? callingMethod = null) {
            if (string.IsNullOrWhiteSpace(pythonCode)) {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            var methodName = callingMethod ?? "Unknown";
            this.Logger.LogDebug("Setup executor applying policies for method {MethodName}", methodName);

            // Get calling method info for attribute inspection
            var callingMethodInfo = this.GetCallingMethod();
            var setupAttribute = callingMethodInfo?.GetAttribute<SetupAttribute>();

            if (setupAttribute == null) {
                this.Logger.LogWarning("Method {MethodName} called SetupExecutor but has no [Setup] attribute", methodName);

                // Execute without setup policies
                return await this.Device.ExecuteAsync<T>(pythonCode, cancellationToken).ConfigureAwait(false);
            }

            // Apply setup policies
            return await this.ExecuteSetupWithPoliciesAsync<T>(pythonCode, methodName, setupAttribute, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resets the setup execution state, allowing setup methods to run again.
        /// This is typically called when reconnecting to a device.
        /// </summary>
        public void ResetSetupState() {
            var count = this.executedSetupMethods.Count;
            this.executedSetupMethods.Clear();
            this.Logger.LogDebug("Reset setup state - {Count} methods can now be executed again", count);
        }

        /// <summary>
        /// Gets the list of setup methods that have been executed.
        /// </summary>
        /// <returns>A collection of executed setup method names.</returns>
        public IReadOnlyCollection<string> GetExecutedSetupMethods() {
            return this.executedSetupMethods.Keys.ToArray();
        }

        /// <summary>
        /// Executes setup code with setup-specific policies applied.
        /// </summary>
        private async Task<T> ExecuteSetupWithPoliciesAsync<T>(
            string pythonCode,
            string methodName,
            SetupAttribute setupAttribute,
            CancellationToken cancellationToken) {
            // Check if already executed (setup methods should only run once per session)
            if (this.executedSetupMethods.ContainsKey(methodName)) {
                this.Logger.LogDebug("Setup method {MethodName} already executed, skipping", methodName);
                return default(T)!; // Setup methods that have already run return default value
            }

            // Apply setup timeout if specified
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (setupAttribute.TimeoutMs.HasValue) {
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(setupAttribute.TimeoutMs.Value));
            }

            // Ensure setup methods execute in the correct order (serialize setup execution)
            await this.setupSemaphore.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            try {
                // Double-check after acquiring the semaphore
                if (this.executedSetupMethods.ContainsKey(methodName)) {
                    this.Logger.LogDebug("Setup method {MethodName} already executed during wait, skipping", methodName);
                    return default(T)!;
                }

                this.Logger.LogInformation(
                    "Executing setup method {MethodName} (order: {Order}, critical: {Critical})",
                    methodName, setupAttribute.Order, setupAttribute.Critical);

                try {
                    var result = await this.Device.ExecuteAsync<T>(pythonCode, timeoutCts.Token).ConfigureAwait(false);

                    // Mark as executed
                    this.executedSetupMethods.TryAdd(methodName, true);

                    this.Logger.LogInformation("Setup method {MethodName} completed successfully", methodName);
                    return result;
                }
                catch (Exception ex) {
                    this.Logger.LogError(ex, "Setup method {MethodName} failed", methodName);

                    if (setupAttribute.Critical) {
                        // Critical setup failures should stop the initialization process
                        throw new InvalidOperationException($"Critical setup method {methodName} failed: {ex.Message}", ex);
                    }
                    else {
                        // Non-critical setup failures are logged but allow execution to continue
                        this.Logger.LogWarning("Non-critical setup method {MethodName} failed, continuing", methodName);
                        return default(T)!;
                    }
                }
            }
            finally {
                this.setupSemaphore.Release();
            }
        }

        /// <summary>
        /// Disposes of the executor resources.
        /// </summary>
        public void Dispose() {
            this.setupSemaphore?.Dispose();
        }
    }
}
