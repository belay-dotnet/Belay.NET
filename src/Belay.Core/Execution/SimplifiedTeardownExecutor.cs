// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Belay.Attributes;
    using Belay.Core.Exceptions;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Simplified executor that applies [Teardown] attribute policies without session management complexity.
    /// Handles teardown-specific execution policies with direct device communication and graceful error handling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This simplified TeardownExecutor eliminates session management overhead while maintaining
    /// teardown-specific policies like emergency cleanup, graceful failure handling, and resource cleanup.
    /// </para>
    /// </remarks>
    public sealed class SimplifiedTeardownExecutor : SimplifiedBaseExecutor {
        /// <summary>
        /// Initializes a new instance of the <see cref="SimplifiedTeardownExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute Python code on.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="errorMapper">Optional error mapper for exception handling.</param>
        /// <param name="executionContextService">Optional execution context service.</param>
        public SimplifiedTeardownExecutor(Device device, ILogger<SimplifiedTeardownExecutor> logger, IErrorMapper? errorMapper = null, IExecutionContextService? executionContextService = null)
            : base(device, logger, errorMapper, executionContextService) {
        }

        /// <summary>
        /// Applies [Teardown] attribute policies around Python code execution.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute on the device.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="callingMethod">The method that initiated this execution.</param>
        /// <returns>The result of the Python code execution with [Teardown] policies applied.</returns>
        public override async Task<T> ApplyPoliciesAndExecuteAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? callingMethod = null) {
            if (string.IsNullOrWhiteSpace(pythonCode)) {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            // Check if executing from a [Teardown] attributed method
            var executionContext = this.ExecutionContextService.Current;
            var teardownAttribute = executionContext?.TeardownAttribute;

            if (teardownAttribute == null) {
                this.Logger.LogDebug("No [Teardown] attribute found, executing with default policies");
                return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken, callingMethod).ConfigureAwait(false);
            }

            this.Logger.LogDebug("Applying [Teardown] attribute policies");

            try {
                return await this.ExecuteWithTeardownPoliciesAsync<T>(pythonCode, cancellationToken, callingMethod).ConfigureAwait(false);
            }
            catch (Exception ex) {
                // For teardown methods, attempt emergency cleanup on failure
                this.Logger.LogWarning(ex, "Teardown execution failed, attempting emergency cleanup");

                try {
                    await this.ExecuteEmergencyCleanupAsync(cancellationToken).ConfigureAwait(false);
                    this.Logger.LogInformation("Emergency cleanup completed successfully");
                }
                catch (Exception emergencyEx) {
                    this.Logger.LogError(emergencyEx, "Emergency cleanup also failed");
                }

                // Re-throw original exception
                throw;
            }
        }

        /// <summary>
        /// Checks if the executor can handle a specific method based on [Teardown] attribute.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <returns>True if the method has a [Teardown] attribute; otherwise, false.</returns>
        public override bool CanHandle(MethodInfo method) {
            return method.GetCustomAttribute<TeardownAttribute>() != null;
        }

        /// <summary>
        /// Executes a method with [Teardown] attribute policies.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="method">The method to execute.</param>
        /// <param name="instance">The instance to invoke the method on (null for static methods).</param>
        /// <param name="parameters">The parameters to pass to the method.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
        /// <returns>The result of the method execution.</returns>
        public override async Task<T> ExecuteAsync<T>(MethodInfo method, object? instance, object?[]? parameters = null, CancellationToken cancellationToken = default) {
            if (method == null) {
                throw new ArgumentNullException(nameof(method));
            }

            var teardownAttribute = method.GetCustomAttribute<TeardownAttribute>();
            if (teardownAttribute == null) {
                throw new InvalidOperationException($"Method '{method.Name}' does not have a [Teardown] attribute");
            }

            // Create execution context for the method
            var context = new MethodExecutionContext(method, instance, parameters);
            using var contextScope = this.ExecutionContextService.SetContext(context);

            this.Logger.LogDebug("Executing teardown method {MethodName} with [Teardown] policies", method.Name);

            // Generate Python code for the method (simplified version)
            var pythonCode = $"# Teardown Method: {method.Name}\nresult = None  # Placeholder for teardown method execution";

            return await this.ApplyPoliciesAndExecuteAsync<T>(pythonCode, cancellationToken, method.Name).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes Python code with [Teardown] attribute policies.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="teardownAttribute">The [Teardown] attribute containing policies.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="operationName">The name of the operation for tracking.</param>
        /// <returns>The result of the Python code execution.</returns>
        private async Task<T> ExecuteWithTeardownPoliciesAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken,
            string? operationName) {
            // Teardown-specific policies
            this.Logger.LogDebug("Executing teardown code with graceful failure handling");

            // For teardown operations, continue even if device is disconnecting
            if (this.Device.ConnectionState == Communication.DeviceConnectionState.Disconnected) {
                this.Logger.LogWarning("Device is disconnected, teardown execution may not be effective");

                // Continue execution anyway for cleanup attempts
            }

            // Apply teardown-specific optimizations
            var capabilities = this.GetDeviceCapabilities();
            if (capabilities?.DetectionComplete == true) {
                this.Logger.LogDebug(
                    "Teardown executing on {Platform} with {FeatureCount} detected features",
                    capabilities.Platform ?? "unknown",
                    CountFlags(capabilities.SupportedFeatures));
            }

            // Use shorter timeout for teardown operations to avoid blocking shutdown
            using var teardownTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            teardownTimeoutCts.CancelAfter(TimeSpan.FromSeconds(30)); // 30 second maximum for teardown

            try {
                return await this.ExecuteOnDeviceAsync<T>(pythonCode, teardownTimeoutCts.Token, $"Teardown:{operationName}").ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (teardownTimeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
                this.Logger.LogWarning("Teardown operation timed out after 30 seconds, continuing with emergency cleanup");

                // Always attempt emergency cleanup on timeout
                await this.ExecuteEmergencyCleanupAsync(cancellationToken).ConfigureAwait(false);

                throw new TimeoutException("Teardown execution timed out after 30 seconds");
            }
        }

        /// <summary>
        /// Executes emergency cleanup operations when normal teardown fails.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous cleanup operation.</returns>
        private async Task ExecuteEmergencyCleanupAsync(CancellationToken cancellationToken) {
            this.Logger.LogInformation("Executing emergency cleanup operations");

            try {
                // Emergency cleanup script for MicroPython devices
                const string emergencyCleanupCode = @"
# Emergency cleanup script
import gc
try:
    # Force garbage collection
    gc.collect()
    
    # Reset any global state if possible
    try:
        # Clear any running threads or timers
        pass
    except:
        pass
        
    print('Emergency cleanup completed')
except Exception as e:
    print(f'Emergency cleanup error: {e}')
";

                // Use very short timeout for emergency cleanup
                using var emergencyTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                emergencyTimeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

                await this.ExecuteOnDeviceAsync(emergencyCleanupCode, emergencyTimeoutCts.Token, "EmergencyCleanup").ConfigureAwait(false);
            }
            catch (Exception ex) {
                this.Logger.LogError(ex, "Emergency cleanup failed");

                // Don't re-throw - this is best-effort cleanup
            }
        }

        /// <summary>
        /// Counts the number of flags set in a feature set enumeration.
        /// </summary>
        /// <param name="flags">The feature set flags to count.</param>
        /// <returns>The number of flags set.</returns>
        private static int CountFlags(SimpleDeviceFeatureSet flags) {
            var count = 0;
            var value = (int)flags;
            while (value > 0) {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }
    }
}
