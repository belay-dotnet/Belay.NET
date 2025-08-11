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
    using Belay.Core.Sessions;
    using Belay.Core.Transactions;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Enhanced executor that provides advanced method interception, pipeline processing,
    /// and enhanced attribute-based execution with pre/post-processing hooks.
    /// </summary>
    public sealed class EnhancedExecutor : BaseExecutor, IEnhancedExecutor, IDisposable {
        private readonly Dictionary<Type, IExecutor> specializedExecutors;
        private readonly ConcurrentDictionary<string, MethodInterceptionContext> interceptionCache;
        private readonly IMethodDeploymentCache deploymentCache;
        private readonly ITransactionManager transactionManager;
        private readonly ExecutionPipeline pipeline;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnhancedExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute Python code on.</param>
        /// <param name="sessionManager">The session manager for device coordination.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="errorMapper">Optional error mapper for exception handling.</param>
        /// <param name="cache">Optional method deployment cache for performance optimization.</param>
        /// <param name="executionContextService">Optional execution context service.</param>
        /// <param name="transactionManager">Optional transaction manager for ensuring consistency.</param>
        /// <param name="specializedExecutors">Optional dictionary of specialized executors for specific attribute types.</param>
        public EnhancedExecutor(
            Device device,
            IDeviceSessionManager sessionManager,
            ILogger<EnhancedExecutor> logger,
            IErrorMapper? errorMapper = null,
            IMethodDeploymentCache? cache = null,
            IExecutionContextService? executionContextService = null,
            ITransactionManager? transactionManager = null,
            Dictionary<Type, IExecutor>? specializedExecutors = null)
            : base(device, sessionManager, logger, errorMapper, executionContextService) {
            this.specializedExecutors = specializedExecutors ?? new Dictionary<Type, IExecutor>();
            this.interceptionCache = new ConcurrentDictionary<string, MethodInterceptionContext>();
            this.deploymentCache = cache ?? new MethodDeploymentCache(new MethodCacheConfiguration(), logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<MethodDeploymentCache>.Instance);
            this.transactionManager = transactionManager ?? new TransactionManager(Microsoft.Extensions.Logging.Abstractions.NullLogger<TransactionManager>.Instance);
            this.pipeline = new ExecutionPipeline(logger);

            this.InitializeDefaultSpecializedExecutors();
        }

        /// <summary>
        /// Enhanced method execution with full interception pipeline.
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

            var cacheKey = GenerateInterceptionCacheKey(method, instance?.GetType());
            var context = this.interceptionCache.GetOrAdd(cacheKey, _ => this.CreateInterceptionContext(method, instance));

            this.Logger.LogDebug(
                "Enhanced execution for method {MethodName} with pipeline: {Pipeline}",
                method.Name, string.Join(" -> ", context.Pipeline.Select(p => p.GetType().Name)));

            // Execute through the enhanced pipeline
            var executionContext = new ExecutionContext<T> {
                Method = method,
                Instance = instance,
                Parameters = parameters,
                CancellationToken = cancellationToken,
                InterceptionContext = context,
                Device = this.Device,
                SessionManager = this.SessionManager,
                Logger = this.Logger,
            };

            return await this.ExecuteThroughPipelineAsync(executionContext).ConfigureAwait(false);
        }

        /// <summary>
        /// Applies enhanced policies and executes Python code through the interception pipeline.
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

            // Get enhanced execution context
            var methodContext = this.GetCurrentMethodContext();
            var method = methodContext?.Method;

            if (method != null) {
                // Use full enhanced execution if we have method context
                return await this.ExecuteAsync<T>(method, null, null, cancellationToken).ConfigureAwait(false);
            }

            // Fallback to direct execution for code without method context
            this.Logger.LogDebug(
                "No method context available, executing Python code directly: {Code}",
                pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

            return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken).ConfigureAwait(false);
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
            return method.HasAttribute<TaskAttribute>() ||
                   method.HasAttribute<ThreadAttribute>() ||
                   method.HasAttribute<SetupAttribute>() ||
                   method.HasAttribute<TeardownAttribute>() ||
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
        /// <returns>Comprehensive execution statistics.</returns>
        public EnhancedExecutionStatistics GetExecutionStatistics() {
            this.ThrowIfDisposed();

            return new EnhancedExecutionStatistics {
                InterceptedMethodCount = this.interceptionCache.Count,
                DeploymentCacheStatistics = this.deploymentCache.GetStatistics(),
                SpecializedExecutorCount = this.specializedExecutors.Count,
                PipelineStageCount = this.pipeline.StageCount,
            };
        }

        /// <summary>
        /// Clears all execution caches and resets pipeline state.
        /// </summary>
        public void ClearExecutionCache() {
            this.ThrowIfDisposed();

            this.interceptionCache.Clear();
            this.deploymentCache.ClearAll();
            this.pipeline.ClearState();

            this.Logger.LogDebug("Cleared enhanced executor caches and pipeline state");
        }

        private async Task<T> ExecuteThroughPipelineAsync<T>(ExecutionContext<T> context) {
            // Execute through the pipeline stages
            foreach (var stage in context.InterceptionContext.Pipeline) {
                context = await stage.ProcessAsync(context).ConfigureAwait(false);

                // Allow pipeline stages to short-circuit execution
                if (context.IsCompleted) {
                    return context.Result;
                }
            }

            // If no pipeline stage completed execution, use default execution
            return await this.ExecuteWithDefaultStrategy<T>(context).ConfigureAwait(false);
        }

        private async Task<T> ExecuteWithDefaultStrategy<T>(ExecutionContext<T> context) {
            // Check for specialized executor first
            var specializedExecutor = this.GetSpecializedExecutor(context.Method);
            if (specializedExecutor != null) {
                this.Logger.LogDebug("Delegating to specialized executor: {ExecutorType}", specializedExecutor.GetType().Name);
                return await specializedExecutor.ExecuteAsync<T>(
                    context.Method,
                    context.Instance,
                    context.Parameters,
                    context.CancellationToken).ConfigureAwait(false);
            }

            // Generate Python code and execute
            var pythonCode = this.GeneratePythonMethodCall(context.Method, context.Instance, context.Parameters);
            return await this.ExecuteOnDeviceAsync<T>(pythonCode, context.CancellationToken).ConfigureAwait(false);
        }

        private MethodInterceptionContext CreateInterceptionContext(MethodInfo method, object? instance) {
            var context = new MethodInterceptionContext {
                Method = method,
                InstanceType = instance?.GetType(),
                Pipeline = new List<IPipelineStage>(),
            };

            // Build pipeline based on method attributes
            this.BuildExecutionPipeline(context, method);

            return context;
        }

        private void BuildExecutionPipeline(MethodInterceptionContext context, MethodInfo method) {
            // Add validation stage
            context.Pipeline.Add(new ValidationPipelineStage());

            // Add attribute-specific stages
            if (method.HasAttribute<TaskAttribute>()) {
                context.Pipeline.Add(new TaskAttributePipelineStage(this.transactionManager));
            }

            if (method.HasAttribute<ThreadAttribute>()) {
                context.Pipeline.Add(new ThreadAttributePipelineStage());
            }

            // Add deployment stage if method appears deployable
            if (this.IsDeployableMethod(method)) {
                context.Pipeline.Add(new DeploymentPipelineStage(this.deploymentCache));
            }

            // Add execution stage (always last)
            context.Pipeline.Add(new ExecutionPipelineStage());
        }

        private bool HasSpecializedExecutor(MethodInfo method) {
            return method.GetCustomAttributes<Attribute>()
                .Any(attr => this.specializedExecutors.ContainsKey(attr.GetType()));
        }

        private IExecutor? GetSpecializedExecutor(MethodInfo method) {
            var attributes = method.GetCustomAttributes<Attribute>();
            foreach (var attr in attributes) {
                if (this.specializedExecutors.TryGetValue(attr.GetType(), out var executor)) {
                    return executor;
                }
            }

            return null;
        }

        private void InitializeDefaultSpecializedExecutors() {
            // Register TaskExecutor for Task attributes
            if (!this.specializedExecutors.ContainsKey(typeof(TaskAttribute))) {
                var taskExecutor = new TaskExecutor(
                    this.Device,
                    this.SessionManager,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<TaskExecutor>.Instance,
                    this.ErrorMapper,
                    this.deploymentCache,
                    this.ExecutionContextService,
                    this.transactionManager);
                this.RegisterSpecializedExecutor(typeof(TaskAttribute), taskExecutor);
            }
        }

        private static string GenerateInterceptionCacheKey(MethodInfo method, Type? instanceType) {
            var instanceTypeName = instanceType?.FullName ?? "static";
            return $"{method.DeclaringType?.FullName}.{method.Name}@{instanceTypeName}#{method.GetSignatureHash()}";
        }

        /// <inheritdoc />
        public void Dispose() {
            if (this.disposed) {
                return;
            }

            try {
                // Dispose specialized executors
                foreach (var executor in this.specializedExecutors.Values) {
                    if (executor is IDisposable disposableExecutor) {
                        disposableExecutor.Dispose();
                    }
                }

                this.deploymentCache?.Dispose();
                this.pipeline?.Dispose();
                this.interceptionCache.Clear();

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
