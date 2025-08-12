// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution
{
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
    public sealed class EnhancedExecutor : SimplifiedBaseExecutor, IEnhancedExecutor, IDisposable
    {
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
            : base(device, logger, errorMapper, executionContextService)
        {
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
            CancellationToken cancellationToken = default)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            this.ThrowIfDisposed();

            var cacheKey = this.GenerateInterceptionCacheKey(method, instance?.GetType());
            var context = this.interceptionCache.GetOrAdd(cacheKey, _ => this.CreateInterceptionContext(method, instance));

            this.Logger.LogDebug("Enhanced execution for method {MethodName} with {ExecutorCount} specialized executors",
                method.Name, this.specializedExecutors.Count);

            // Create execution context for the method
            var methodContext = new MethodExecutionContext(method, instance, parameters);
            using var contextScope = this.ExecutionContextService.SetContext(methodContext);

            // Check for specialized executor first
            var specializedExecutor = this.GetSpecializedExecutor(method);
            if (specializedExecutor != null)
            {
                this.Logger.LogDebug("Delegating to specialized executor: {ExecutorType}", specializedExecutor.GetType().Name);
                return await specializedExecutor.ExecuteAsync<T>(method, instance, parameters, cancellationToken).ConfigureAwait(false);
            }

            // Fallback to attribute-based execution through simplified executors
            return await this.ExecuteWithAttributeRouting<T>(method, instance, parameters, cancellationToken).ConfigureAwait(false);
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
            [CallerMemberName] string? callingMethod = null)
        {
            this.ThrowIfDisposed();

            // Check if we have an execution context and route appropriately
            var executionContext = this.ExecutionContextService.Current;
            if (executionContext != null)
            {
                this.Logger.LogDebug("Enhanced execution with context for method {MethodName}", executionContext.Method.Name);
                return await this.ExecuteAsync<T>(executionContext.Method, executionContext.Instance, executionContext.Parameters, cancellationToken).ConfigureAwait(false);
            }

            // Fallback to direct execution for code without method context
            this.Logger.LogDebug("No method context available, executing Python code directly: {Code}",
                pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

            return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken, callingMethod).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if this executor can handle the given method with enhanced capabilities.
        /// </summary>
        /// <param name="method">The method to validate.</param>
        /// <returns>True if the method can be handled by enhanced execution.</returns>
        public override bool CanHandle(MethodInfo method)
        {
            if (method == null)
            {
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
        public void RegisterSpecializedExecutor(Type attributeType, IExecutor executor)
        {
            if (attributeType == null)
            {
                throw new ArgumentNullException(nameof(attributeType));
            }

            if (executor == null)
            {
                throw new ArgumentNullException(nameof(executor));
            }

            this.specializedExecutors[attributeType] = executor;
            this.Logger.LogDebug("Registered specialized executor {ExecutorType} for attribute {AttributeType}",
                executor.GetType().Name, attributeType.Name);
        }

        /// <summary>
        /// Gets execution statistics from the enhanced executor.
        /// </summary>
        /// <returns>Enhanced execution statistics.</returns>
        public EnhancedExecutionStatistics GetExecutionStatistics()
        {
            this.ThrowIfDisposed();

            return new EnhancedExecutionStatistics
            {
                InterceptedMethodCount = this.interceptionCache.Count,
                DeploymentCacheStatistics = this.deploymentCache.GetStatistics(),
                SpecializedExecutorCount = this.specializedExecutors.Count,
                PipelineStageCount = 0, // Simplified executor doesn't use complex pipeline
            };
        }

        /// <summary>
        /// Clears all execution caches and resets state.
        /// </summary>
        public void ClearExecutionCache()
        {
            this.ThrowIfDisposed();

            this.interceptionCache.Clear();
            this.deploymentCache.ClearAll();
            this.Device.State.ClearExecutionHistory();

            this.Logger.LogDebug("Cleared enhanced executor caches and state");
        }

        /// <summary>
        /// Routes method execution through the appropriate simplified executor based on attributes.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="method">The method to execute.</param>
        /// <param name="instance">The instance to invoke the method on.</param>
        /// <param name="parameters">The parameters to pass to the method.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
        /// <returns>The result of the method execution.</returns>
        private async Task<T> ExecuteWithAttributeRouting<T>(
            MethodInfo method,
            object? instance,
            object?[]? parameters,
            CancellationToken cancellationToken)
        {
            // Route through appropriate simplified executor based on attribute
            if (method.GetCustomAttribute<TaskAttribute>() != null)
            {
                return await this.Device.Task.ExecuteAsync<T>(method, instance, parameters, cancellationToken).ConfigureAwait(false);
            }

            if (method.GetCustomAttribute<SetupAttribute>() != null)
            {
                return await this.Device.Setup.ExecuteAsync<T>(method, instance, parameters, cancellationToken).ConfigureAwait(false);
            }

            if (method.GetCustomAttribute<ThreadAttribute>() != null)
            {
                return await this.Device.Thread.ExecuteAsync<T>(method, instance, parameters, cancellationToken).ConfigureAwait(false);
            }

            if (method.GetCustomAttribute<TeardownAttribute>() != null)
            {
                return await this.Device.Teardown.ExecuteAsync<T>(method, instance, parameters, cancellationToken).ConfigureAwait(false);
            }

            // Generate Python code and execute directly if no specific attribute found
            var pythonCode = $"# Method: {method.Name}\\nresult = None  # Placeholder for method execution";
            return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken, method.Name).ConfigureAwait(false);
        }

        private MethodInterceptionContext CreateInterceptionContext(MethodInfo method, object? instance)
        {
            return new MethodInterceptionContext
            {
                Method = method,
                InstanceType = instance?.GetType(),
                Pipeline = new List<IPipelineStage>(), // Simplified - no complex pipeline
            };
        }

        private bool HasSpecializedExecutor(MethodInfo method)
        {
            return method.GetCustomAttributes<Attribute>()
                .Any(attr => this.specializedExecutors.ContainsKey(attr.GetType()));
        }

        private IExecutor? GetSpecializedExecutor(MethodInfo method)
        {
            var attributes = method.GetCustomAttributes<Attribute>();
            foreach (var attr in attributes)
            {
                if (this.specializedExecutors.TryGetValue(attr.GetType(), out var executor))
                {
                    return executor;
                }
            }

            return null;
        }

        private void InitializeDefaultSpecializedExecutors()
        {
            // Register simplified executors for standard attributes if not already provided
            if (!this.specializedExecutors.ContainsKey(typeof(TaskAttribute)))
            {
                this.RegisterSpecializedExecutor(typeof(TaskAttribute), this.Device.Task);
            }

            if (!this.specializedExecutors.ContainsKey(typeof(SetupAttribute)))
            {
                this.RegisterSpecializedExecutor(typeof(SetupAttribute), this.Device.Setup);
            }

            if (!this.specializedExecutors.ContainsKey(typeof(ThreadAttribute)))
            {
                this.RegisterSpecializedExecutor(typeof(ThreadAttribute), this.Device.Thread);
            }

            if (!this.specializedExecutors.ContainsKey(typeof(TeardownAttribute)))
            {
                this.RegisterSpecializedExecutor(typeof(TeardownAttribute), this.Device.Teardown);
            }
        }

        private string GenerateInterceptionCacheKey(MethodInfo method, Type? instanceType)
        {
            var instanceTypeName = instanceType?.FullName ?? "static";
            return $"{method.DeclaringType?.FullName}.{method.Name}@{instanceTypeName}#{method.GetHashCode()}";
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            try
            {
                // Clear caches
                this.interceptionCache.Clear();
                this.deploymentCache?.Dispose();

                // Note: Don't dispose Device.Task/Setup/Thread/Teardown as they're owned by Device
                // Only dispose specialized executors that were explicitly registered
                foreach (var executor in this.specializedExecutors.Values)
                {
                    if (executor is IDisposable disposableExecutor && 
                        executor != this.Device.Task && 
                        executor != this.Device.Setup && 
                        executor != this.Device.Thread && 
                        executor != this.Device.Teardown)
                    {
                        disposableExecutor.Dispose();
                    }
                }

                this.Logger.LogDebug("EnhancedExecutor disposed");
            }
            catch (Exception ex)
            {
                this.Logger.LogWarning(ex, "Error during EnhancedExecutor disposal");
            }
            finally
            {
                this.disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(EnhancedExecutor));
            }
        }
    }
}