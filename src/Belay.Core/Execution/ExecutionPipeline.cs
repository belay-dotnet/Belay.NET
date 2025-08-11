// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Belay.Attributes;
    using Belay.Core.Caching;
    using Belay.Core.Sessions;
    using Belay.Core.Transactions;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Execution context for enhanced method execution.
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    public class ExecutionContext<T> {
        /// <summary>
        /// Gets or sets the method being executed.
        /// </summary>
        public required MethodInfo Method { get; set; }

        /// <summary>
        /// Gets or sets the instance to invoke the method on (null for static methods).
        /// </summary>
        public object? Instance { get; set; }

        /// <summary>
        /// Gets or sets the parameters to pass to the method.
        /// </summary>
        public object?[]? Parameters { get; set; }

        /// <summary>
        /// Gets or sets the cancellation token for the execution.
        /// </summary>
        public CancellationToken CancellationToken { get; set; } = default;

        /// <summary>
        /// Gets or sets the interception context for pipeline processing.
        /// </summary>
        public required MethodInterceptionContext InterceptionContext { get; set; }

        /// <summary>
        /// Gets or sets the device for execution.
        /// </summary>
        public required Device Device { get; set; }

        /// <summary>
        /// Gets or sets the session manager for device coordination.
        /// </summary>
        public required IDeviceSessionManager SessionManager { get; set; }

        /// <summary>
        /// Gets or sets the logger for diagnostic information.
        /// </summary>
        public required ILogger Logger { get; set; }

        /// <summary>
        /// Gets or sets whether the execution is completed.
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Gets or sets the result of the execution.
        /// </summary>
        public T Result { get; set; } = default!;

        /// <summary>
        /// Gets or sets additional context data for pipeline stages.
        /// </summary>
        public Dictionary<string, object?> ContextData { get; set; } = new Dictionary<string, object?>();
    }

    /// <summary>
    /// Method interception context for caching pipeline configuration.
    /// </summary>
    public class MethodInterceptionContext {
        /// <summary>
        /// Gets or sets the method being intercepted.
        /// </summary>
        public required MethodInfo Method { get; set; }

        /// <summary>
        /// Gets or sets the instance type (null for static methods).
        /// </summary>
        public Type? InstanceType { get; set; }

        /// <summary>
        /// Gets or sets the execution pipeline stages.
        /// </summary>
        public required List<IPipelineStage> Pipeline { get; set; }

        /// <summary>
        /// Gets or sets cached metadata for the method.
        /// </summary>
        public Dictionary<string, object?> Metadata { get; set; } = new Dictionary<string, object?>();
    }

    /// <summary>
    /// Interface for execution pipeline stages.
    /// </summary>
    public interface IPipelineStage {
        /// <summary>
        /// Processes an execution context through this pipeline stage.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="context">The execution context to process.</param>
        /// <returns>The processed execution context.</returns>
        Task<ExecutionContext<T>> ProcessAsync<T>(ExecutionContext<T> context);
    }

    /// <summary>
    /// Validation pipeline stage that validates method parameters and context.
    /// </summary>
    public class ValidationPipelineStage : IPipelineStage {
        /// <summary>
        /// Validates the execution context and parameters.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="context">The execution context to validate.</param>
        /// <returns>The validated execution context.</returns>
        public Task<ExecutionContext<T>> ProcessAsync<T>(ExecutionContext<T> context) {
            // Validate method
            if (context.Method == null) {
                throw new ArgumentException("Method cannot be null", nameof(context));
            }

            // Validate parameter count
            var parameters = context.Method.GetParameters();
            var providedParams = context.Parameters;

            if (parameters.Length > 0 && (providedParams == null || providedParams.Length != parameters.Length)) {
                throw new ArgumentException(
                    $"Method '{context.Method.Name}' expects {parameters.Length} parameters but {providedParams?.Length ?? 0} were provided");
            }

            // Validate parameter types (basic validation)
            if (providedParams != null) {
                for (int i = 0; i < parameters.Length; i++) {
                    var expectedType = parameters[i].ParameterType;
                    var providedValue = providedParams[i];

                    if (providedValue != null && !expectedType.IsAssignableFrom(providedValue.GetType())) {
                        // Allow some basic type conversions
                        if (!CanConvertType(providedValue.GetType(), expectedType)) {
                            context.Logger.LogWarning(
                                "Parameter type mismatch for {MethodName}[{ParameterIndex}]: expected {ExpectedType}, got {ActualType}",
                                context.Method.Name, i, expectedType.Name, providedValue.GetType().Name);
                        }
                    }
                }
            }

            context.Logger.LogTrace("Validation stage completed for method {MethodName}", context.Method.Name);
            return Task.FromResult(context);
        }

        private static bool CanConvertType(Type fromType, Type toType) {
            // Allow numeric conversions
            if (fromType.IsPrimitive && toType.IsPrimitive) {
                return true;
            }

            // Allow string conversions
            if (fromType == typeof(string) || toType == typeof(string)) {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Task attribute pipeline stage that handles Task attribute-specific processing.
    /// </summary>
    public class TaskAttributePipelineStage : IPipelineStage {
        private readonly ITransactionManager transactionManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskAttributePipelineStage"/> class.
        /// </summary>
        /// <param name="transactionManager">The transaction manager for consistency.</param>
        public TaskAttributePipelineStage(ITransactionManager transactionManager) {
            this.transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
        }

        /// <summary>
        /// Processes Task attribute-specific logic.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="context">The execution context to process.</param>
        /// <returns>The processed execution context.</returns>
        public async Task<ExecutionContext<T>> ProcessAsync<T>(ExecutionContext<T> context) {
            var taskAttribute = context.Method.GetCustomAttribute<TaskAttribute>();
            if (taskAttribute == null) {
                return context;
            }

            context.Logger.LogDebug(
                "Processing Task attribute for method {MethodName}: Timeout={TimeoutMs}ms, Cache={Cache}, Exclusive={Exclusive}",
                context.Method.Name, taskAttribute.TimeoutMs, taskAttribute.Cache, taskAttribute.Exclusive);

            // Store attribute in context for later stages
            context.ContextData["TaskAttribute"] = taskAttribute;

            // Apply timeout if specified
            if (taskAttribute.TimeoutMs.HasValue) {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(taskAttribute.TimeoutMs.Value));
                var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeoutCts.Token);
                context.CancellationToken = combinedCts.Token;
                context.ContextData["TimeoutCts"] = combinedCts;
            }

            context.Logger.LogTrace("Task attribute stage completed for method {MethodName}", context.Method.Name);
            return context;
        }
    }

    /// <summary>
    /// Thread attribute pipeline stage that handles Thread attribute-specific processing.
    /// </summary>
    public class ThreadAttributePipelineStage : IPipelineStage {
        /// <summary>
        /// Processes Thread attribute-specific logic.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="context">The execution context to process.</param>
        /// <returns>The processed execution context.</returns>
        public Task<ExecutionContext<T>> ProcessAsync<T>(ExecutionContext<T> context) {
            var threadAttribute = context.Method.GetCustomAttribute<ThreadAttribute>();
            if (threadAttribute == null) {
                return Task.FromResult(context);
            }

            context.Logger.LogDebug(
                "Processing Thread attribute for method {MethodName}: AutoRestart={AutoRestart}",
                context.Method.Name, threadAttribute.AutoRestart);

            // Store attribute in context for later stages
            context.ContextData["ThreadAttribute"] = threadAttribute;

            context.Logger.LogTrace("Thread attribute stage completed for method {MethodName}", context.Method.Name);
            return Task.FromResult(context);
        }
    }

    /// <summary>
    /// Deployment pipeline stage that handles method deployment to the device.
    /// </summary>
    public class DeploymentPipelineStage : IPipelineStage {
        private readonly IMethodDeploymentCache deploymentCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeploymentPipelineStage"/> class.
        /// </summary>
        /// <param name="deploymentCache">The deployment cache for tracking deployed methods.</param>
        public DeploymentPipelineStage(IMethodDeploymentCache deploymentCache) {
            this.deploymentCache = deploymentCache ?? throw new ArgumentNullException(nameof(deploymentCache));
        }

        /// <summary>
        /// Processes method deployment logic.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="context">The execution context to process.</param>
        /// <returns>The processed execution context.</returns>
        public Task<ExecutionContext<T>> ProcessAsync<T>(ExecutionContext<T> context) {
            // TODO: Implement method deployment logic
            // This would check if the method is deployed and deploy it if necessary

            context.Logger.LogTrace("Deployment stage completed for method {MethodName}", context.Method.Name);
            return Task.FromResult(context);
        }
    }

    /// <summary>
    /// Execution pipeline stage that performs the actual method execution.
    /// </summary>
    public class ExecutionPipelineStage : IPipelineStage {
        /// <summary>
        /// Performs the actual method execution.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="context">The execution context to execute.</param>
        /// <returns>The executed context with result.</returns>
        public async Task<ExecutionContext<T>> ProcessAsync<T>(ExecutionContext<T> context) {
            context.Logger.LogDebug("Executing method {MethodName} through execution stage", context.Method.Name);

            try {
                // This stage doesn't actually execute - it signals that default execution should be used
                // The EnhancedExecutor will handle the actual execution after pipeline processing
                context.Logger.LogTrace("Execution stage prepared for method {MethodName}", context.Method.Name);
                return context;
            }
            catch (Exception ex) {
                context.Logger.LogError(ex, "Execution stage failed for method {MethodName}", context.Method.Name);
                throw;
            }
        }
    }

    /// <summary>
    /// Manages the execution pipeline and provides pipeline utilities.
    /// </summary>
    public class ExecutionPipeline : IDisposable {
        private readonly ILogger logger;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionPipeline"/> class.
        /// </summary>
        /// <param name="logger">The logger for pipeline operations.</param>
        public ExecutionPipeline(ILogger logger) {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the number of stages in the pipeline.
        /// </summary>
        public int StageCount { get; private set; }

        /// <summary>
        /// Clears any pipeline state.
        /// </summary>
        public void ClearState() {
            this.logger.LogDebug("Pipeline state cleared");
        }

        /// <inheritdoc />
        public void Dispose() {
            if (!this.disposed) {
                this.logger.LogDebug("ExecutionPipeline disposed");
                this.disposed = true;
            }
        }
    }

    /// <summary>
    /// Statistics for enhanced execution.
    /// </summary>
    public class EnhancedExecutionStatistics {
        /// <summary>
        /// Gets or sets the number of methods that have been intercepted.
        /// </summary>
        public int InterceptedMethodCount { get; set; }

        /// <summary>
        /// Gets or sets the deployment cache statistics.
        /// </summary>
        public CacheStatistics DeploymentCacheStatistics { get; set; } = new CacheStatistics();

        /// <summary>
        /// Gets or sets the number of registered specialized executors.
        /// </summary>
        public int SpecializedExecutorCount { get; set; }

        /// <summary>
        /// Gets or sets the number of pipeline stages.
        /// </summary>
        public int PipelineStageCount { get; set; }
    }
}