// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution
{
    using System;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Belay.Attributes;
    using Belay.Core.Exceptions;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Simplified base class for all method executors that eliminates session management complexity.
    /// Provides direct device communication with DeviceState tracking for improved performance and reliability.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This simplified executor architecture eliminates the complex session management layer
    /// in favor of direct device communication with lightweight state tracking, providing:
    /// </para>
    /// <para>
    /// Key improvements:
    /// <list type="bullet">
    /// <item><description>Eliminates race conditions from concurrent session creation</description></item>
    /// <item><description>Reduces execution overhead by removing session indirection</description></item>
    /// <item><description>Provides direct access to device capabilities through DeviceState</description></item>
    /// <item><description>Aligns with single-threaded MicroPython device constraints</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public abstract class SimplifiedBaseExecutor : IExecutor
    {
        /// <summary>
        /// Gets the device instance to execute Python code on.
        /// </summary>
        protected Device Device { get; }

        /// <summary>
        /// Gets logger for diagnostic information.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the error mapper for mapping exceptions.
        /// </summary>
        protected IErrorMapper? ErrorMapper { get; }

        /// <summary>
        /// Gets the execution context service for secure method context access.
        /// </summary>
        protected IExecutionContextService ExecutionContextService { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimplifiedBaseExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute Python code on.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="errorMapper">Optional error mapper for exception handling.</param>
        /// <param name="executionContextService">Optional execution context service (creates default if null).</param>
        protected SimplifiedBaseExecutor(Device device, ILogger logger, IErrorMapper? errorMapper = null, IExecutionContextService? executionContextService = null)
        {
            this.Device = device ?? throw new ArgumentNullException(nameof(device));
            this.Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.ErrorMapper = errorMapper;
            this.ExecutionContextService = executionContextService ?? new ExecutionContextService();
        }

        /// <summary>
        /// Applies executor-specific policies around Python code execution.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute on the device.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="callingMethod">The method that initiated this execution (optional).</param>
        /// <returns>The result of the Python code execution with policies applied.</returns>
        public abstract Task<T> ApplyPoliciesAndExecuteAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? callingMethod = null);

        /// <summary>
        /// Applies executor-specific policies around Python code execution without returning a value.
        /// </summary>
        /// <param name="pythonCode">The Python code to execute on the device.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="callingMethod">The method that initiated this execution (optional).</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public virtual async Task ApplyPoliciesAndExecuteAsync(
            string pythonCode,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? callingMethod = null)
        {
            if (string.IsNullOrWhiteSpace(pythonCode))
            {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            await this.ApplyPoliciesAndExecuteAsync<object>(pythonCode, cancellationToken, callingMethod).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes Python code directly on the device with capability-aware optimizations.
        /// This is used as the final execution step after policies have been applied.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="operationName">Optional operation name for tracking.</param>
        /// <returns>The result of the Python code execution.</returns>
        protected async Task<T> ExecuteOnDeviceAsync<T>(string pythonCode, CancellationToken cancellationToken = default, string? operationName = null)
        {
            // Track operation in device state
            this.Device.State.SetCurrentOperation(operationName ?? "ExecuteCode");

            try
            {
                this.Logger.LogDebug(
                    "Executing Python code with simplified executor: {Code}",
                    pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

                // Apply capability-aware optimizations
                var result = await this.ExecuteWithCapabilityOptimizationAsync<T>(pythonCode, cancellationToken).ConfigureAwait(false);

                this.Logger.LogDebug("Python code execution completed successfully");

                return result;
            }
            finally
            {
                // Complete operation tracking
                this.Device.State.CompleteOperation();
            }
        }

        /// <summary>
        /// Executes Python code directly on the device without returning a value.
        /// </summary>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="operationName">Optional operation name for tracking.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        protected async Task ExecuteOnDeviceAsync(string pythonCode, CancellationToken cancellationToken = default, string? operationName = null)
        {
            await this.ExecuteOnDeviceAsync<object>(pythonCode, cancellationToken, operationName).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if the executor can handle a specific method based on its attributes.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <returns>True if the executor can handle the method; otherwise, false.</returns>
        public abstract bool CanHandle(MethodInfo method);

        /// <summary>
        /// Executes a method with the executor's specific policies.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="method">The method to execute.</param>
        /// <param name="instance">The instance to invoke the method on (null for static methods).</param>
        /// <param name="parameters">The parameters to pass to the method.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
        /// <returns>The result of the method execution.</returns>
        public abstract Task<T> ExecuteAsync<T>(MethodInfo method, object? instance = null, object?[]? parameters = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a method without returning a value.
        /// </summary>
        /// <param name="method">The method to execute.</param>
        /// <param name="instance">The instance to invoke the method on (null for static methods).</param>
        /// <param name="parameters">The parameters to pass to the method.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task ExecuteAsync(MethodInfo method, object? instance = null, object?[]? parameters = null, CancellationToken cancellationToken = default)
        {
            await this.ExecuteAsync<object>(method, instance, parameters, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes Python code with capability-aware optimization instead of session-based optimization.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The result of the Python code execution.</returns>
        protected virtual async Task<T> ExecuteWithCapabilityOptimizationAsync<T>(string pythonCode, CancellationToken cancellationToken)
        {
            try
            {
                // Check if device capabilities can inform execution optimization
                var capabilities = this.Device.State.Capabilities;
                if (capabilities?.DetectionComplete == true)
                {
                    // Apply platform-specific optimizations based on detected capabilities
                    if (capabilities.Platform == "esp8266" || capabilities.AvailableMemory < 50000)
                    {
                        // For low-memory devices, add small delay to prevent overwhelming
                        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                    }

                    this.Logger.LogTrace(
                        "Executing on {Platform} with {Memory} bytes available and {FeatureCount} features",
                        capabilities.Platform ?? "unknown",
                        capabilities.AvailableMemory,
                        CountFlags(capabilities.SupportedFeatures));
                }

                // Direct device execution without session indirection
                return await this.Device.Communication.ExecuteAsync<T>(pythonCode, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (this.ErrorMapper != null)
            {
                // Apply error mapping if available
                var mappedException = this.ErrorMapper.MapException(ex);
                this.Logger.LogDebug(ex, "Exception mapped from {OriginalType} to {MappedType}",
                    ex.GetType().Name, mappedException.GetType().Name);
                throw mappedException;
            }
        }

        /// <summary>
        /// Gets the current device capabilities if available.
        /// </summary>
        /// <returns>Device capabilities or null if not detected.</returns>
        protected SimpleDeviceCapabilities? GetDeviceCapabilities()
        {
            return this.Device.State.Capabilities;
        }

        /// <summary>
        /// Checks if the device supports a specific feature.
        /// </summary>
        /// <param name="feature">The feature to check.</param>
        /// <returns>True if the feature is supported; otherwise, false.</returns>
        protected bool SupportsFeature(SimpleDeviceFeatureSet feature)
        {
            return this.Device.State.Capabilities?.SupportsFeature(feature) == true;
        }

        /// <summary>
        /// Counts the number of flags set in a feature set enumeration.
        /// </summary>
        /// <param name="flags">The feature set flags to count.</param>
        /// <returns>The number of flags set.</returns>
        private static int CountFlags(SimpleDeviceFeatureSet flags)
        {
            var count = 0;
            var value = (int)flags;
            while (value > 0)
            {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }
    }
}