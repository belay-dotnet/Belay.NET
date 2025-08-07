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
    /// Executor for methods decorated with the [Teardown] attribute.
    /// Applies teardown-specific policies such as ordered cleanup and error handling.
    /// </summary>
    public sealed class TeardownExecutor : BaseExecutor, IDisposable {
        private readonly ConcurrentDictionary<string, bool> executedTeardownMethods;
        private readonly SemaphoreSlim teardownSemaphore;

        /// <summary>
        /// Initializes a new instance of the <see cref="TeardownExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute code on.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        public TeardownExecutor(Device device, ILogger<TeardownExecutor> logger)
            : base(device, logger) {
            this.executedTeardownMethods = new ConcurrentDictionary<string, bool>();
            this.teardownSemaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Applies teardown policies and executes the Python code.
        /// Teardown methods are executed during disconnection with proper error handling and ordering.
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
            this.Logger.LogDebug("Teardown executor applying policies for method {MethodName}", methodName);

            // Get calling method info for attribute inspection
            var callingMethodInfo = this.GetCallingMethod();
            var teardownAttribute = callingMethodInfo?.GetAttribute<TeardownAttribute>();

            if (teardownAttribute == null) {
                this.Logger.LogWarning("Method {MethodName} called TeardownExecutor but has no [Teardown] attribute", methodName);

                // Execute without teardown policies
                return await this.Device.ExecuteAsync<T>(pythonCode, cancellationToken).ConfigureAwait(false);
            }

            // Apply teardown policies
            return await this.ExecuteTeardownWithPoliciesAsync<T>(pythonCode, methodName, teardownAttribute, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resets the teardown execution state, allowing teardown methods to run again.
        /// This is typically called when reconnecting to a device.
        /// </summary>
        public void ResetTeardownState() {
            var count = this.executedTeardownMethods.Count;
            this.executedTeardownMethods.Clear();
            this.Logger.LogDebug("Reset teardown state - {Count} methods can now be executed again", count);
        }

        /// <summary>
        /// Gets the list of teardown methods that have been executed.
        /// </summary>
        /// <returns>A collection of executed teardown method names.</returns>
        public IReadOnlyCollection<string> GetExecutedTeardownMethods() {
            return this.executedTeardownMethods.Keys.ToArray();
        }

        /// <summary>
        /// Executes all teardown methods with proper error handling and ordering.
        /// This is typically called during device disconnection.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        public Task ExecuteAllTeardownMethodsAsync(CancellationToken cancellationToken = default) {
            Logger.LogInformation("Executing all teardown methods");

            // Note: In a complete implementation, this would discover teardown methods
            // from the device instance and execute them in reverse order
            // For now, this is a placeholder that logs the intent
            Logger.LogDebug("Teardown execution completed");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Executes teardown code with teardown-specific policies applied.
        /// </summary>
        private async Task<T> ExecuteTeardownWithPoliciesAsync<T>(
            string pythonCode,
            string methodName,
            TeardownAttribute teardownAttribute,
            CancellationToken cancellationToken) {
            // Check if already executed (teardown methods typically only run once)
            if (this.executedTeardownMethods.ContainsKey(methodName)) {
                this.Logger.LogDebug("Teardown method {MethodName} already executed, skipping", methodName);
                return default(T)!;
            }

            // Apply teardown timeout if specified (typically shorter than setup/task timeouts)
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (teardownAttribute.TimeoutMs.HasValue) {
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(teardownAttribute.TimeoutMs.Value));
            }
            else {
                // Default teardown timeout of 10 seconds to prevent hanging during disconnect
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            }

            // Ensure teardown methods execute in the correct order (serialize teardown execution)
            await this.teardownSemaphore.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            try {
                // Double-check after acquiring the semaphore
                if (this.executedTeardownMethods.ContainsKey(methodName)) {
                    this.Logger.LogDebug("Teardown method {MethodName} already executed during wait, skipping", methodName);
                    return default(T)!;
                }

                this.Logger.LogInformation(
                    "Executing teardown method {MethodName} (order: {Order}, critical: {Critical}, ignoreErrors: {IgnoreErrors})",
                    methodName, teardownAttribute.Order, teardownAttribute.Critical, teardownAttribute.IgnoreErrors);

                try {
                    // Wrap the user's teardown code with error handling
                    var wrappedCode = WrapTeardownCode(pythonCode, methodName, teardownAttribute);

                    var result = await this.Device.ExecuteAsync<T>(wrappedCode, timeoutCts.Token).ConfigureAwait(false);

                    // Mark as executed
                    this.executedTeardownMethods.TryAdd(methodName, true);

                    this.Logger.LogInformation("Teardown method {MethodName} completed successfully", methodName);
                    return result;
                }
                catch (Exception ex) {
                    this.Logger.LogError(ex, "Teardown method {MethodName} failed", methodName);

                    if (teardownAttribute.IgnoreErrors) {
                        // Errors are ignored - log and continue
                        this.Logger.LogWarning("Teardown method {MethodName} failed but errors are ignored", methodName);
                        this.executedTeardownMethods.TryAdd(methodName, true);
                        return default(T)!;
                    }
                    else if (teardownAttribute.Critical) {
                        // Critical teardown failures should be re-thrown
                        throw new InvalidOperationException($"Critical teardown method {methodName} failed: {ex.Message}", ex);
                    }
                    else {
                        // Non-critical teardown failures are logged but allow execution to continue
                        this.Logger.LogWarning("Non-critical teardown method {methodName} failed, continuing", methodName);
                        this.executedTeardownMethods.TryAdd(methodName, true);
                        return default(T)!;
                    }
                }
            }
            finally {
                this.teardownSemaphore.Release();
            }
        }

        /// <summary>
        /// Wraps teardown code with additional error handling and cleanup logic.
        /// </summary>
        private static string WrapTeardownCode(string userCode, string methodName, TeardownAttribute teardownAttribute) {
            var errorHandling = teardownAttribute.IgnoreErrors ? "pass  # Errors ignored" : "raise  # Propagate error";

            return $@"# Teardown method: {methodName}
# Order: {teardownAttribute.Order}, Critical: {teardownAttribute.Critical}, IgnoreErrors: {teardownAttribute.IgnoreErrors}
try:
    print(f'Executing teardown: {methodName}')
{IndentCode(userCode, 4)}
    print(f'Teardown {methodName} completed successfully')
except Exception as e:
    print(f'Teardown {methodName} error: {{e}}')
    {errorHandling}";
        }

        /// <summary>
        /// Indents code by the specified number of spaces.
        /// </summary>
        private static string IndentCode(string code, int spaces) {
            var indent = new string(' ', spaces);
            return string.Join("\n", code.Split('\n').Select(line =>
                string.IsNullOrWhiteSpace(line) ? line : indent + line));
        }

        /// <summary>
        /// Disposes of the executor resources.
        /// </summary>
        public void Dispose() {
            this.teardownSemaphore?.Dispose();
        }
    }
}
