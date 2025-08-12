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
    /// Simplified executor that applies [Setup] attribute policies without session management complexity.
    /// Handles setup-specific execution policies with direct device communication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This simplified SetupExecutor eliminates session management overhead while maintaining
    /// setup-specific policies like longer timeouts and initialization-specific optimizations.
    /// </para>
    /// </remarks>
    public sealed class SimplifiedSetupExecutor : SimplifiedBaseExecutor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SimplifiedSetupExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute Python code on.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="errorMapper">Optional error mapper for exception handling.</param>
        /// <param name="executionContextService">Optional execution context service.</param>
        public SimplifiedSetupExecutor(Device device, ILogger<SimplifiedSetupExecutor> logger, IErrorMapper? errorMapper = null, IExecutionContextService? executionContextService = null)
            : base(device, logger, errorMapper, executionContextService)
        {
        }

        /// <summary>
        /// Applies [Setup] attribute policies around Python code execution.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute on the device.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="callingMethod">The method that initiated this execution.</param>
        /// <returns>The result of the Python code execution with [Setup] policies applied.</returns>
        public override async Task<T> ApplyPoliciesAndExecuteAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? callingMethod = null)
        {
            if (string.IsNullOrWhiteSpace(pythonCode))
            {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            // Check if executing from a [Setup] attributed method
            var executionContext = this.ExecutionContextService.Current;
            var setupAttribute = executionContext?.SetupAttribute;

            if (setupAttribute == null)
            {
                this.Logger.LogDebug("No [Setup] attribute found, executing with default policies");
                return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken, callingMethod).ConfigureAwait(false);
            }

            this.Logger.LogDebug("Applying [Setup] attribute policies: Timeout={Timeout}ms",
                setupAttribute.TimeoutMs);

            // Apply setup-specific timeout policy (longer than default for initialization)
            using var timeoutCts = setupAttribute.TimeoutMs > 0
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            if (timeoutCts != null)
            {
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(setupAttribute.TimeoutMs));
            }

            var effectiveCancellationToken = timeoutCts?.Token ?? cancellationToken;

            try
            {
                return await this.ExecuteWithSetupPoliciesAsync<T>(pythonCode, setupAttribute, effectiveCancellationToken, callingMethod).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts?.Token.IsCancellationRequested == true)
            {
                throw new TimeoutException($"Setup execution timed out after {setupAttribute.TimeoutMs}ms");
            }
        }

        /// <summary>
        /// Checks if the executor can handle a specific method based on [Setup] attribute.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <returns>True if the method has a [Setup] attribute; otherwise, false.</returns>
        public override bool CanHandle(MethodInfo method)
        {
            return method.GetCustomAttribute<SetupAttribute>() != null;
        }

        /// <summary>
        /// Executes a method with [Setup] attribute policies.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="method">The method to execute.</param>
        /// <param name="instance">The instance to invoke the method on (null for static methods).</param>
        /// <param name="parameters">The parameters to pass to the method.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
        /// <returns>The result of the method execution.</returns>
        public override async Task<T> ExecuteAsync<T>(MethodInfo method, object? instance = null, object?[]? parameters = null, CancellationToken cancellationToken = default)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            var setupAttribute = method.GetCustomAttribute<SetupAttribute>();
            if (setupAttribute == null)
            {
                throw new InvalidOperationException($"Method '{method.Name}' does not have a [Setup] attribute");
            }

            // Create execution context for the method
            var context = new MethodExecutionContext(method, instance, parameters);
            using var contextScope = this.ExecutionContextService.SetContext(context);

            this.Logger.LogDebug("Executing setup method {MethodName} with [Setup] policies", method.Name);

            // Generate Python code for the method (simplified version)
            var pythonCode = $"# Setup Method: {method.Name}\nresult = None  # Placeholder for setup method execution";

            return await this.ApplyPoliciesAndExecuteAsync<T>(pythonCode, cancellationToken, method.Name).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes Python code with [Setup] attribute policies.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="setupAttribute">The [Setup] attribute containing policies.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="operationName">The name of the operation for tracking.</param>
        /// <returns>The result of the Python code execution.</returns>
        private async Task<T> ExecuteWithSetupPoliciesAsync<T>(
            string pythonCode,
            SetupAttribute setupAttribute,
            CancellationToken cancellationToken,
            string? operationName)
        {
            // Setup-specific policies
            this.Logger.LogDebug("Executing setup code with extended timeout and initialization optimizations");

            // Check device connection state
            if (this.Device.ConnectionState != Communication.DeviceConnectionState.Connected)
            {
                throw new InvalidOperationException("Device must be connected before executing setup code");
            }

            // Apply setup-specific optimizations
            var capabilities = this.GetDeviceCapabilities();
            if (capabilities?.DetectionComplete == true)
            {
                this.Logger.LogDebug("Setup executing on {Platform} with {FeatureCount} detected features",
                    capabilities.Platform ?? "unknown",
                    CountFlags(capabilities.SupportedFeatures));

                // For setup operations, allow extra time for low-memory devices
                if (capabilities.AvailableMemory < 30000)
                {
                    this.Logger.LogDebug("Low memory device detected, applying setup-specific delays");
                    await Task.Delay(5, cancellationToken).ConfigureAwait(false);
                }
            }

            return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken, $"Setup:{operationName}").ConfigureAwait(false);
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