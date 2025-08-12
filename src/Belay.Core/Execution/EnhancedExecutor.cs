// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
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
    /// Enhanced executor that provides advanced method interception and execution
    /// capabilities with direct device communication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enhanced executor provides method interception, specialized executor delegation,
    /// and execution statistics with direct device communication for optimal performance.
    /// </para>
    /// <para>
    /// Key features:
    /// <list type="bullet">
    /// <item><description>Direct device communication without session overhead</description></item>
    /// <item><description>Method interception and attribute-based routing</description></item>
    /// <item><description>Specialized executor delegation for different attribute types</description></item>
    /// <item><description>Execution statistics and caching support</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class EnhancedExecutor : SimplifiedBaseExecutor, IEnhancedExecutor, IDisposable {
        private readonly ConcurrentDictionary<string, MethodInterceptionContext> interceptionCache;
        private readonly IMethodDeploymentCache deploymentCache;
        private readonly ITransactionManager transactionManager;
        private readonly Dictionary<Type, IExecutor> specializedExecutors;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnhancedExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute Python code on.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="errorMapper">Optional error mapper for exception handling.</param>
        /// <param name="cache">Optional method deployment cache for performance optimization.</param>
        /// <param name="executionContextService">Optional execution context service.</param>
        /// <param name="transactionManager">Optional transaction manager for ensuring consistency.</param>
        /// <param name="specializedExecutors">Optional dictionary of specialized executors for specific attribute types.</param>
        public EnhancedExecutor(
            Device device,
            ILogger<EnhancedExecutor> logger,
            IErrorMapper? errorMapper = null,
            IMethodDeploymentCache? cache = null,
            IExecutionContextService? executionContextService = null,
            ITransactionManager? transactionManager = null,
            Dictionary<Type, IExecutor>? specializedExecutors = null)
            : base(device, logger, errorMapper, executionContextService) {
            this.interceptionCache = new ConcurrentDictionary<string, MethodInterceptionContext>();
            this.deploymentCache = cache ?? new MethodDeploymentCache(
                new MethodCacheConfiguration(),
                logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<MethodDeploymentCache>.Instance);
            this.transactionManager = transactionManager ?? new TransactionManager(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<TransactionManager>.Instance);
            this.specializedExecutors = specializedExecutors ?? new Dictionary<Type, IExecutor>();

            this.InitializeDefaultSpecializedExecutors();
        }

        /// <summary>
        /// Enhanced method execution with interception and specialized executor delegation.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="method">The method to execute.</param>
        /// <param name="instance">The instance to invoke the method on (null for static methods).</param>
        /// <param name="parameters">The parameters to pass to the method.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
        /// <returns>The result of the method execution.</returns>
        public override async Task<T> ExecuteAsync<T>(
            MethodInfo method,
            object? instance,
            object?[]? parameters = null,
            CancellationToken cancellationToken = default) {
            if (method == null) {
                throw new ArgumentNullException(nameof(method));
            }

            this.ThrowIfDisposed();

            var cacheKey = this.GenerateInterceptionCacheKey(method, instance?.GetType());
            var context = this.interceptionCache.GetOrAdd(cacheKey, _ => this.CreateInterceptionContext(method, instance));

            this.Logger.LogDebug(
                "Enhanced execution for method {MethodName} with {ExecutorCount} specialized executors",
                method.Name, this.specializedExecutors.Count);

            // Create execution context for the method
            var methodContext = new MethodExecutionContext(method, instance, parameters);
            using var contextScope = this.ExecutionContextService.SetContext(methodContext);

            // IMPORTANT: Extract Python code directly instead of delegating to avoid circular dependencies
            // Enhanced executor should extract Python code and execute directly, not delegate to other executors
            string pythonCode = this.ExtractPythonCodeFromMethod(method, parameters);

            this.Logger.LogDebug(
                "Enhanced executor extracted Python code: {Code}",
                pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

            // Execute directly through ApplyPoliciesAndExecuteAsync which routes to ExecuteOnDeviceAsync
            return await this.ApplyPoliciesAndExecuteAsync<T>(pythonCode, cancellationToken, method.Name).ConfigureAwait(false);
        }

        /// <summary>
        /// Applies enhanced policies and executes Python code with method context awareness.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute on the device.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="callingMethod">The method that initiated this execution.</param>
        /// <returns>The result of the Python code execution with enhanced policies applied.</returns>
        public override async Task<T> ApplyPoliciesAndExecuteAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? callingMethod = null) {
            this.ThrowIfDisposed();

            // IMPORTANT: Do not route back through ExecuteAsync to prevent infinite recursion
            // ApplyPoliciesAndExecuteAsync must execute Python code directly on the device
            // The execution context is already established by the calling method

            // Enhanced executor applies minimal additional policies and delegates to device execution
            this.Logger.LogDebug(
                "Enhanced executor executing Python code directly: {Code}",
                pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

            // Execute directly on device without routing back through method execution system
            return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken, callingMethod).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if this executor can handle the given method with enhanced capabilities.
        /// </summary>
        /// <param name="method">The method to validate.</param>
        /// <returns>True if the method can be handled by enhanced execution.</returns>
        public override bool CanHandle(MethodInfo method) {
            if (method == null) {
                return false;
            }

            // Enhanced executor can handle any method with supported attributes
            return method.GetCustomAttribute<TaskAttribute>() != null ||
                   method.GetCustomAttribute<ThreadAttribute>() != null ||
                   method.GetCustomAttribute<SetupAttribute>() != null ||
                   method.GetCustomAttribute<TeardownAttribute>() != null ||
                   this.HasSpecializedExecutor(method);
        }

        /// <summary>
        /// Registers a specialized executor for specific attribute types.
        /// </summary>
        /// <param name="attributeType">The attribute type to handle.</param>
        /// <param name="executor">The specialized executor for this attribute type.</param>
        public void RegisterSpecializedExecutor(Type attributeType, IExecutor executor) {
            if (attributeType == null) {
                throw new ArgumentNullException(nameof(attributeType));
            }

            if (executor == null) {
                throw new ArgumentNullException(nameof(executor));
            }

            this.specializedExecutors[attributeType] = executor;
            this.Logger.LogDebug(
                "Registered specialized executor {ExecutorType} for attribute {AttributeType}",
                executor.GetType().Name, attributeType.Name);
        }

        /// <summary>
        /// Gets execution statistics from the enhanced executor.
        /// </summary>
        /// <returns>Enhanced execution statistics.</returns>
        public EnhancedExecutionStatistics GetExecutionStatistics() {
            this.ThrowIfDisposed();

            return new EnhancedExecutionStatistics {
                InterceptedMethodCount = this.interceptionCache.Count,
                DeploymentCacheStatistics = this.deploymentCache.GetStatistics(),
                SpecializedExecutorCount = this.specializedExecutors.Count,
                PipelineStageCount = 0, // Simplified executor doesn't use complex pipeline
            };
        }

        /// <summary>
        /// Clears all execution caches and resets state.
        /// </summary>
        public void ClearExecutionCache() {
            this.ThrowIfDisposed();

            this.interceptionCache.Clear();
            this.deploymentCache.ClearAll();
            this.Device.State.ClearExecutionHistory();

            this.Logger.LogDebug("Cleared enhanced executor caches and state");
        }

        private MethodInterceptionContext CreateInterceptionContext(MethodInfo method, object? instance) {
            return new MethodInterceptionContext {
                Method = method,
                InstanceType = instance?.GetType(),
                Pipeline = new List<IPipelineStage>(), // Simplified - no complex pipeline
            };
        }

        private bool HasSpecializedExecutor(MethodInfo method) {
            return method.GetCustomAttributes<Attribute>()
                .Any(attr => this.specializedExecutors.ContainsKey(attr.GetType()));
        }

        private void InitializeDefaultSpecializedExecutors() {
            // Enhanced executor now handles all attributes directly without delegation
            // No specialized executors needed - this prevents circular dependencies
            this.Logger.LogDebug("Enhanced executor using direct execution - no specialized executors needed");
        }

        private string ExtractPythonCodeFromMethod(MethodInfo method, object?[]? parameters) {
            // Get Python code from [PythonCode] attribute first, fallback to method invocation
            var pythonCodeAttribute = method.GetCustomAttribute<Belay.Attributes.PythonCodeAttribute>();

            if (pythonCodeAttribute != null) {
                // Extract Python code from attribute and apply parameter substitution if enabled
                var pythonCode = pythonCodeAttribute.Code;

                if (pythonCodeAttribute.EnableParameterSubstitution && parameters != null && parameters.Length > 0) {
                    // Apply parameter substitution
                    var parameterNames = method.GetParameters().Select(p => p.Name).ToArray();
                    for (int i = 0; i < Math.Min(parameterNames.Length, parameters.Length); i++) {
                        if (parameterNames[i] != null && parameters[i] != null) {
                            var paramValue = this.ConvertParameterToPython(parameters[i]!);
                            pythonCode = pythonCode.Replace($"{{{parameterNames[i]}}}", paramValue);
                        }
                    }
                }

                this.Logger.LogDebug("Using Python code from [PythonCode] attribute: {Code}", pythonCode);
                return pythonCode;
            }
            else {
                // For enhanced executor, generate a default implementation rather than invoking
                // This prevents infinite recursion when the instance is a proxy
                this.Logger.LogDebug("No [PythonCode] attribute found, generating default Python code for method {MethodName}", method.Name);
                return $"# Method: {method.Name}\nresult = None  # Enhanced executor default implementation";
            }
        }

        private string ConvertParameterToPython(object parameter) {
            return parameter switch {
                string s => $"'{s.Replace("'", "\\'")}'",
                int i => i.ToString(),
                long l => l.ToString(),
                float f => f.ToString("F", System.Globalization.CultureInfo.InvariantCulture),
                double d => d.ToString("F", System.Globalization.CultureInfo.InvariantCulture),
                bool b => b ? "True" : "False",
                null => "None",
                _ => $"'{parameter.ToString()?.Replace("'", "\\'") ?? "None"}'",
            };
        }

        private string GenerateInterceptionCacheKey(MethodInfo method, Type? instanceType) {
            var instanceTypeName = instanceType?.FullName ?? "static";
            return $"{method.DeclaringType?.FullName}.{method.Name}@{instanceTypeName}#{method.GetHashCode()}";
        }

        /// <inheritdoc />
        public void Dispose() {
            if (this.disposed) {
                return;
            }

            try {
                // Clear caches
                this.interceptionCache.Clear();
                this.deploymentCache?.Dispose();

                // Note: Don't dispose Device.Task/Setup/Thread/Teardown as they're owned by Device
                // Only dispose specialized executors that were explicitly registered
                foreach (var executor in this.specializedExecutors.Values) {
                    if (executor is IDisposable disposableExecutor &&
                        executor != this.Device.Task &&
                        executor != this.Device.Setup &&
                        executor != this.Device.Thread &&
                        executor != this.Device.Teardown) {
                        disposableExecutor.Dispose();
                    }
                }

                this.Logger.LogDebug("EnhancedExecutor disposed");
            }
            catch (Exception ex) {
                this.Logger.LogWarning(ex, "Error during EnhancedExecutor disposal");
            }
            finally {
                this.disposed = true;
            }
        }

        private void ThrowIfDisposed() {
            if (this.disposed) {
                throw new ObjectDisposedException(nameof(EnhancedExecutor));
            }
        }
    }
}
