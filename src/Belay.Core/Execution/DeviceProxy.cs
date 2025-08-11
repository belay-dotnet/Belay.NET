// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Dynamic proxy that intercepts method calls and routes them through the enhanced executor.
    /// This enables seamless attribute-based programming where C# methods are automatically
    /// executed on MicroPython devices with proper attribute handling.
    /// </summary>
    /// <typeparam name="T">The interface or base class to proxy.</typeparam>
    public class DeviceProxy<T> : DispatchProxy where T : class {
        private IEnhancedExecutor? executor;
        private ILogger? logger;
        private readonly ConcurrentDictionary<MethodInfo, bool> methodCapabilityCache = new();

        /// <summary>
        /// Creates a device proxy instance that intercepts method calls.
        /// </summary>
        /// <param name="executor">The enhanced executor to handle method calls.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        /// <returns>A proxy instance of type T.</returns>
        public static T Create(IEnhancedExecutor executor, ILogger? logger = null) {
            if (executor == null) {
                throw new ArgumentNullException(nameof(executor));
            }

            var proxy = Create<T, DeviceProxy<T>>() as DeviceProxy<T>;
            if (proxy == null) {
                throw new InvalidOperationException($"Failed to create proxy for type {typeof(T).Name}");
            }

            proxy.executor = executor;
            proxy.logger = logger;

            logger?.LogDebug("Created device proxy for type {TypeName}", typeof(T).Name);
            return (T)(object)proxy;
        }

        /// <summary>
        /// Intercepts method calls and routes them through the enhanced executor.
        /// </summary>
        /// <param name="targetMethod">The method being called.</param>
        /// <param name="args">The method arguments.</param>
        /// <returns>The result of the method execution.</returns>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod == null) {
                throw new ArgumentNullException(nameof(targetMethod));
            }

            if (this.executor == null) {
                throw new InvalidOperationException("Executor not initialized");
            }

            this.logger?.LogTrace("Intercepting method call: {MethodName}", targetMethod.Name);

            // Check if this method can be handled by the executor
            if (!this.CanHandleMethod(targetMethod)) {
                this.logger?.LogWarning(
                    "Method {MethodName} cannot be handled by executor, attempting direct invocation",
                    targetMethod.Name);

                // Attempt to invoke the method directly (this will only work for concrete methods)
                try {
                    return targetMethod.Invoke(this, args);
                }
                catch (Exception ex) {
                    this.logger?.LogError(ex, "Direct invocation failed for method {MethodName}", targetMethod.Name);
                    throw new InvalidOperationException(
                        $"Method '{targetMethod.Name}' cannot be executed. " +
                        "Methods must have supported attributes or be handled by the executor.", ex);
                }
            }

            // Route through enhanced executor
            return this.ExecuteMethodAsync(targetMethod, args).GetAwaiter().GetResult();
        }

        private async Task<object?> ExecuteMethodAsync(MethodInfo method, object?[]? args) {
            if (this.executor == null) {
                throw new InvalidOperationException("Executor not initialized");
            }

            try {
                // Determine return type
                var returnType = method.ReturnType;
                bool isAsync = returnType == typeof(Task) ||
                              (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>));

                if (isAsync) {
                    // Handle async methods
                    if (returnType == typeof(Task)) {
                        await this.executor.ExecuteAsync(method, this, args).ConfigureAwait(false);
                        return Task.CompletedTask;
                    }
                    else {
                        // Task<T>
                        var genericType = returnType.GetGenericArguments()[0];
                        var executeMethod = typeof(IEnhancedExecutor).GetMethod(nameof(IEnhancedExecutor.ExecuteAsync),
                            new[] { typeof(MethodInfo), typeof(object), typeof(object[]), typeof(CancellationToken) })
                            ?.MakeGenericMethod(genericType);

                        if (executeMethod == null) {
                            throw new InvalidOperationException($"Could not create generic execute method for type {genericType.Name}");
                        }

                        var task = (Task)executeMethod.Invoke(this.executor, new object[] { method, this, args ?? Array.Empty<object>(), CancellationToken.None })!;
                        await task.ConfigureAwait(false);

                        // Get result from completed task
                        var resultProperty = task.GetType().GetProperty("Result");
                        var result = resultProperty?.GetValue(task);

                        // Wrap result in Task<T>
                        var taskFromResult = typeof(Task).GetMethod(nameof(Task.FromResult))?.MakeGenericMethod(genericType);
                        return taskFromResult?.Invoke(null, new[] { result });
                    }
                }
                else {
                    // Handle sync methods
                    if (returnType == typeof(void)) {
                        await this.executor.ExecuteAsync(method, this, args).ConfigureAwait(false);
                        return null;
                    }
                    else {
                        var executeMethod = typeof(IEnhancedExecutor).GetMethod(nameof(IEnhancedExecutor.ExecuteAsync),
                            new[] { typeof(MethodInfo), typeof(object), typeof(object[]), typeof(CancellationToken) })
                            ?.MakeGenericMethod(returnType);

                        if (executeMethod == null) {
                            throw new InvalidOperationException($"Could not create generic execute method for type {returnType.Name}");
                        }

                        var task = (Task)executeMethod.Invoke(this.executor, new object[] { method, this, args ?? Array.Empty<object>(), CancellationToken.None })!;
                        await task.ConfigureAwait(false);

                        // Get result from completed task
                        var resultProperty = task.GetType().GetProperty("Result");
                        return resultProperty?.GetValue(task);
                    }
                }
            }
            catch (Exception ex) {
                this.logger?.LogError(ex, "Failed to execute method {MethodName} through proxy", method.Name);
                throw;
            }
        }

        private bool CanHandleMethod(MethodInfo method) {
            return this.methodCapabilityCache.GetOrAdd(method, m => {
                if (this.executor == null) {
                    return false;
                }

                // Check if executor can handle this method
                var canHandle = this.executor.CanHandle(m);

                this.logger?.LogTrace("Method {MethodName} can be handled: {CanHandle}", m.Name, canHandle);
                return canHandle;
            });
        }
    }

    /// <summary>
    /// Interface for enhanced executors that can be used with device proxies.
    /// </summary>
    public interface IEnhancedExecutor : IExecutor {
        /// <summary>
        /// Gets execution statistics from the enhanced executor.
        /// </summary>
        /// <returns>Enhanced execution statistics.</returns>
        EnhancedExecutionStatistics GetExecutionStatistics();

        /// <summary>
        /// Clears all execution caches and resets state.
        /// </summary>
        void ClearExecutionCache();
    }

    /// <summary>
    /// Factory for creating device proxies with proper configuration.
    /// </summary>
    public static class DeviceProxyFactory {
        /// <summary>
        /// Creates a device proxy for the specified interface type.
        /// </summary>
        /// <typeparam name="T">The interface type to proxy.</typeparam>
        /// <param name="device">The device to execute methods on.</param>
        /// <param name="logger">Optional logger for diagnostic information.</param>
        /// <returns>A proxy instance that routes method calls to the device.</returns>
        public static T CreateProxy<T>(Device device, ILogger? logger = null) where T : class {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            // Validate that T is an interface or abstract class
            if (!typeof(T).IsInterface && !typeof(T).IsAbstract) {
                throw new ArgumentException($"Type {typeof(T).Name} must be an interface or abstract class to be proxied");
            }

            // Create enhanced executor
            var enhancedExecutor = new EnhancedExecutor(
                device,
                device.Sessions,
                logger as ILogger<EnhancedExecutor> ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EnhancedExecutor>.Instance);

            // Create and return proxy
            return DeviceProxy<T>.Create(enhancedExecutor, logger);
        }

        /// <summary>
        /// Creates a device proxy with a custom enhanced executor.
        /// </summary>
        /// <typeparam name="T">The interface type to proxy.</typeparam>
        /// <param name="executor">The enhanced executor to use for method execution.</param>
        /// <param name="logger">Optional logger for diagnostic information.</param>
        /// <returns>A proxy instance that routes method calls through the executor.</returns>
        public static T CreateProxyWithExecutor<T>(IEnhancedExecutor executor, ILogger? logger = null) where T : class {
            if (executor == null) {
                throw new ArgumentNullException(nameof(executor));
            }

            // Validate that T is an interface or abstract class
            if (!typeof(T).IsInterface && !typeof(T).IsAbstract) {
                throw new ArgumentException($"Type {typeof(T).Name} must be an interface or abstract class to be proxied");
            }

            return DeviceProxy<T>.Create(executor, logger);
        }

        /// <summary>
        /// Validates that a type can be proxied.
        /// </summary>
        /// <param name="type">The type to validate.</param>
        /// <returns>True if the type can be proxied, false otherwise.</returns>
        public static bool CanProxy(Type type) {
            if (type == null) {
                return false;
            }

            // Must be interface or abstract class
            if (!type.IsInterface && !type.IsAbstract) {
                return false;
            }

            // Should have methods with supported attributes
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            return methods.Any(m =>
                m.GetCustomAttribute<Belay.Attributes.TaskAttribute>() != null ||
                m.GetCustomAttribute<Belay.Attributes.ThreadAttribute>() != null ||
                m.GetCustomAttribute<Belay.Attributes.SetupAttribute>() != null ||
                m.GetCustomAttribute<Belay.Attributes.TeardownAttribute>() != null);
        }
    }
}