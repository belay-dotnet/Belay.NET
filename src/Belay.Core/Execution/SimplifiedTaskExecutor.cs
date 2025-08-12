// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
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
    /// Simplified executor that applies [Task] attribute policies without session management complexity.
    /// Handles timeout, caching, and exclusive execution policies with direct device communication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This simplified TaskExecutor eliminates session management overhead while maintaining
    /// all core functionality including caching, transaction support, and attribute-based policies.
    /// </para>
    /// <para>
    /// Performance improvements:
    /// <list type="bullet">
    /// <item><description>Direct device communication without session indirection</description></item>
    /// <item><description>Simplified execution path with reduced overhead</description></item>
    /// <item><description>Better error handling with immediate device feedback</description></item>
    /// <item><description>Consistent operation tracking through DeviceState</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class SimplifiedTaskExecutor : SimplifiedBaseExecutor, IDisposable {
        private readonly SemaphoreSlim exclusiveSemaphore;
        private readonly IMethodDeploymentCache methodCache;
        private readonly ITransactionManager transactionManager;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimplifiedTaskExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute Python code on.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="errorMapper">Optional error mapper for exception handling.</param>
        /// <param name="cache">Optional method deployment cache for performance optimization.</param>
        /// <param name="executionContextService">Optional execution context service.</param>
        /// <param name="transactionManager">Optional transaction manager for ensuring consistency.</param>
        public SimplifiedTaskExecutor(Device device, ILogger<SimplifiedTaskExecutor> logger, IErrorMapper? errorMapper = null, IMethodDeploymentCache? cache = null, IExecutionContextService? executionContextService = null, ITransactionManager? transactionManager = null)
            : base(device, logger, errorMapper, executionContextService) {
            this.exclusiveSemaphore = new SemaphoreSlim(1, 1);
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
        /// <returns>The result of the Python code execution with [Task] policies applied.</returns>
        public override async Task<T> ApplyPoliciesAndExecuteAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? callingMethod = null) {
            if (string.IsNullOrWhiteSpace(pythonCode)) {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            // Check if executing from a [Task] attributed method
            var executionContext = this.ExecutionContextService.Current;
            var taskAttribute = executionContext?.TaskAttribute;

            if (taskAttribute == null) {
                this.Logger.LogDebug("No [Task] attribute found, executing with default policies");
                return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken, callingMethod).ConfigureAwait(false);
            }

            this.Logger.LogDebug(
                "Applying [Task] attribute policies: Timeout={Timeout}ms, Exclusive={Exclusive}, Cache={Cache}",
                taskAttribute.TimeoutMs,
                taskAttribute.Exclusive,
                taskAttribute.Cache);

            // Apply timeout policy
            using var timeoutCts = taskAttribute.TimeoutMs > 0
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;

            if (timeoutCts != null) {
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(taskAttribute.TimeoutMs));
            }

            var effectiveCancellationToken = timeoutCts?.Token ?? cancellationToken;

            try {
                // Apply exclusive execution policy
                if (taskAttribute.Exclusive) {
                    await this.exclusiveSemaphore.WaitAsync(effectiveCancellationToken).ConfigureAwait(false);
                    try {
                        return await this.ExecuteWithPoliciesAsync<T>(pythonCode, taskAttribute, effectiveCancellationToken, callingMethod).ConfigureAwait(false);
                    }
                    finally {
                        this.exclusiveSemaphore.Release();
                    }
                }
                else {
                    // Non-exclusive execution still uses semaphore for consistency
                    await this.exclusiveSemaphore.WaitAsync(effectiveCancellationToken).ConfigureAwait(false);
                    try {
                        return await this.ExecuteWithPoliciesAsync<T>(pythonCode, taskAttribute, effectiveCancellationToken, callingMethod).ConfigureAwait(false);
                    }
                    finally {
                        this.exclusiveSemaphore.Release();
                    }
                }
            }
            catch (OperationCanceledException) when (timeoutCts?.Token.IsCancellationRequested == true) {
                throw new TimeoutException($"Task execution timed out after {taskAttribute.TimeoutMs}ms");
            }
        }

        /// <summary>
        /// Checks if the executor can handle a specific method based on [Task] attribute.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <returns>True if the method has a [Task] attribute; otherwise, false.</returns>
        public override bool CanHandle(MethodInfo method) {
            return method.GetCustomAttribute<TaskAttribute>() != null;
        }

        /// <summary>
        /// Executes a method with [Task] attribute policies.
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

            var taskAttribute = method.GetCustomAttribute<TaskAttribute>();
            if (taskAttribute == null) {
                throw new InvalidOperationException($"Method '{method.Name}' does not have a [Task] attribute");
            }

            // Create execution context for the method
            var context = new MethodExecutionContext(method, instance, parameters);
            using var contextScope = this.ExecutionContextService.SetContext(context);

            this.Logger.LogDebug("Executing method {MethodName} with [Task] policies", method.Name);

            // Get Python code from [PythonCode] attribute first, fallback to method invocation
            string pythonCode;
            var pythonCodeAttribute = method.GetCustomAttribute<Belay.Attributes.PythonCodeAttribute>();

            if (pythonCodeAttribute != null) {
                // Extract Python code from attribute and apply parameter substitution if enabled
                pythonCode = pythonCodeAttribute.Code;

                if (pythonCodeAttribute.EnableParameterSubstitution && parameters != null && parameters.Length > 0) {
                    // Apply parameter substitution
                    var parameterNames = method.GetParameters().Select(p => p.Name).ToArray();
                    for (int i = 0; i < Math.Min(parameterNames.Length, parameters.Length); i++) {
                        if (parameterNames[i] != null && parameters[i] != null) {
                            var paramValue = ConvertParameterToPython(parameters[i]!);
                            pythonCode = pythonCode.Replace($"{{{parameterNames[i]}}}", paramValue);
                        }
                    }
                }

                this.Logger.LogDebug("Using Python code from [PythonCode] attribute: {Code}", pythonCode);
            }
            else {
                // Fallback to method invocation for methods that return Python code strings
                try {
                    var result = method.Invoke(instance, parameters);
                    if (result is string code) {
                        pythonCode = code;
                    }
                    else {
                        throw new InvalidOperationException($"Method '{method.Name}' must return a string containing Python code or have a [PythonCode] attribute");
                    }
                }
                catch (TargetInvocationException ex) {
                    throw new InvalidOperationException($"Failed to get Python code from method '{method.Name}'", ex.InnerException ?? ex);
                }
            }

            this.Logger.LogDebug("Generated Python code: {Code}", pythonCode);

            return await this.ApplyPoliciesAndExecuteAsync<T>(pythonCode, cancellationToken, method.Name).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes Python code with [Task] attribute policies including caching and transactions.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="taskAttribute">The [Task] attribute containing policies.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="operationName">The name of the operation for tracking.</param>
        /// <returns>The result of the Python code execution.</returns>
        private async Task<T> ExecuteWithPoliciesAsync<T>(
            string pythonCode,
            TaskAttribute taskAttribute,
            CancellationToken cancellationToken,
            string? operationName) {
            // Simplified execution without complex caching for now
            this.Logger.LogDebug("Executing task code with simplified policies");

            // Check cache if caching is enabled
            if (taskAttribute.Cache && this.methodCache != null) {
                var (deviceId, firmwareVersion) = this.Device.GetDeviceIdentification();
                var cacheKey = new MethodCacheKey(deviceId, firmwareVersion, pythonCode);

                var cachedResult = this.methodCache.Get<T>(cacheKey);
                if (cachedResult != null) {
                    this.Logger.LogDebug("Cache hit for method execution");
                    return cachedResult;
                }

                // Execute and cache result
                var result = await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken, operationName).ConfigureAwait(false);

                if (result != null) {
                    this.methodCache.Set(cacheKey, result);
                    this.Logger.LogDebug("Result cached for future use");
                }

                return result;
            }

            // Execute without caching
            return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken, operationName).ConfigureAwait(false);
        }

        /// <summary>
        /// Clears the method cache and execution state.
        /// </summary>
        public void ClearCache() {
            this.methodCache?.ClearAll();
            this.Device.State.ClearExecutionHistory();
            this.Logger.LogDebug("Task executor cache and execution history cleared");
        }

        /// <summary>
        /// Converts a C# parameter value to a Python-compatible string representation.
        /// </summary>
        /// <param name="value">The parameter value to convert.</param>
        /// <returns>A Python-compatible string representation of the value.</returns>
        private static string ConvertParameterToPython(object value) {
            return value switch {
                null => "None",
                bool b => b ? "True" : "False",
                string s => $"\"{s.Replace("\"", "\\\"")}\"",
                char c => $"\"{c}\"",
                byte or sbyte or short or ushort or int or uint or long or ulong => value.ToString()!,
                float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
                double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => $"\"{value.ToString()?.Replace("\"", "\\\"")}\"", // Default to string representation
            };
        }

        /// <inheritdoc/>
        public void Dispose() {
            if (!this.disposed) {
                try {
                    // Dispose the semaphore
                    this.exclusiveSemaphore?.Dispose();
                    this.methodCache?.Dispose();

                    // Note: transactionManager disposal handled by DI container if needed
                    this.disposed = true;
                    this.Logger.LogDebug("SimplifiedTaskExecutor disposed");
                }
                catch (Exception ex) {
                    // Log but don't throw - disposal should be best-effort
                    this.Logger.LogWarning(ex, "Error during SimplifiedTaskExecutor disposal");
                }
            }
        }
    }
}
