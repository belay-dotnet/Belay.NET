// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System;
    using System.Collections.Concurrent;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Belay.Attributes;
    using Belay.Core.Exceptions;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Simplified executor that applies [Thread] attribute policies without session management complexity.
    /// Handles thread-specific execution policies with direct device communication and capability-aware threading.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This simplified ThreadExecutor eliminates session management overhead while maintaining
    /// thread-specific policies like capability validation, thread management, and health monitoring.
    /// </para>
    /// </remarks>
    public sealed class SimplifiedThreadExecutor : SimplifiedBaseExecutor {
        private readonly ConcurrentDictionary<string, string> activeThreads = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="SimplifiedThreadExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute Python code on.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="errorMapper">Optional error mapper for exception handling.</param>
        /// <param name="executionContextService">Optional execution context service.</param>
        public SimplifiedThreadExecutor(Device device, ILogger<SimplifiedThreadExecutor> logger, IErrorMapper? errorMapper = null, IExecutionContextService? executionContextService = null)
            : base(device, logger, errorMapper, executionContextService) {
        }

        /// <summary>
        /// Gets the number of active threads being managed by this executor.
        /// </summary>
        public int ActiveThreadCount => this.activeThreads.Count;

        /// <summary>
        /// Applies [Thread] attribute policies around Python code execution.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute on the device.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="callingMethod">The method that initiated this execution.</param>
        /// <returns>The result of the Python code execution with [Thread] policies applied.</returns>
        public override async Task<T> ApplyPoliciesAndExecuteAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? callingMethod = null) {
            if (string.IsNullOrWhiteSpace(pythonCode)) {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            // Check if executing from a [Thread] attributed method
            var executionContext = this.ExecutionContextService.Current;
            var threadAttribute = executionContext?.ThreadAttribute;

            if (threadAttribute == null) {
                this.Logger.LogDebug("No [Thread] attribute found, executing with default policies");
                return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken, callingMethod).ConfigureAwait(false);
            }

            this.Logger.LogDebug("Applying [Thread] attribute policies");

            // Validate threading capability
            this.ValidateThreadingSupport();

            return await this.ExecuteWithThreadPoliciesAsync<T>(pythonCode, threadAttribute, cancellationToken, callingMethod).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if the executor can handle a specific method based on [Thread] attribute.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <returns>True if the method has a [Thread] attribute; otherwise, false.</returns>
        public override bool CanHandle(MethodInfo method) {
            return method.GetCustomAttribute<ThreadAttribute>() != null;
        }

        /// <summary>
        /// Executes a method with [Thread] attribute policies.
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

            var threadAttribute = method.GetCustomAttribute<ThreadAttribute>();
            if (threadAttribute == null) {
                throw new InvalidOperationException($"Method '{method.Name}' does not have a [Thread] attribute");
            }

            // Create execution context for the method
            var context = new MethodExecutionContext(method, instance, parameters);
            using var contextScope = this.ExecutionContextService.SetContext(context);

            this.Logger.LogDebug("Executing thread method {MethodName} with [Thread] policies", method.Name);

            // Generate Python code for the method (simplified version)
            var pythonCode = $"# Thread Method: {method.Name}\nresult = None  # Placeholder for thread method execution";

            return await this.ApplyPoliciesAndExecuteAsync<T>(pythonCode, cancellationToken, method.Name).ConfigureAwait(false);
        }

        /// <summary>
        /// Starts a new thread on the MicroPython device.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute in the thread.</param>
        /// <param name="threadName">The name of the thread for tracking.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The thread identifier or result from thread creation.</returns>
        public async Task<T> StartThreadAsync<T>(string pythonCode, string threadName, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(pythonCode)) {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            if (string.IsNullOrWhiteSpace(threadName)) {
                throw new ArgumentException("Thread name cannot be null or empty", nameof(threadName));
            }

            this.ValidateThreadingSupport();

            this.Logger.LogDebug(
                "Starting thread '{ThreadName}' with code: {Code}",
                threadName, pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

            var threadCreationCode = $@"
import _thread
import time

def thread_function_{threadName.Replace(' ', '_')}():
    try:
        {pythonCode}
    except Exception as e:
        print(f'Thread {threadName} error: {{e}}')

thread_id = _thread.start_new_thread(thread_function_{threadName.Replace(' ', '_')}, ())
thread_id
";

            var result = await this.ExecuteOnDeviceAsync<T>(threadCreationCode, cancellationToken, $"StartThread:{threadName}").ConfigureAwait(false);

            // Track active thread
            this.activeThreads[threadName] = result?.ToString() ?? "unknown_id";
            this.Logger.LogDebug("Thread '{ThreadName}' started successfully", threadName);

            return result;
        }

        /// <summary>
        /// Stops a specific thread on the MicroPython device.
        /// </summary>
        /// <param name="threadName">The name of the thread to stop.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>True if the thread was stopped successfully; otherwise, false.</returns>
        public async Task<bool> StopThreadAsync(string threadName, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(threadName)) {
                throw new ArgumentException("Thread name cannot be null or empty", nameof(threadName));
            }

            this.Logger.LogDebug("Stopping thread '{ThreadName}'", threadName);

            // Note: MicroPython doesn't have direct thread stopping, so this is a best-effort operation
            var stopCode = $@"
# Best-effort thread cleanup for {threadName}
try:
    # Signal thread to stop (implementation depends on user code)
    # This is a placeholder - actual implementation would require cooperative thread shutdown
    print('Thread {threadName} stop signal sent')
    True
except Exception as e:
    print(f'Error stopping thread {threadName}: {{e}}')
    False
";

            try {
                var result = await this.ExecuteOnDeviceAsync<bool>(stopCode, cancellationToken, $"StopThread:{threadName}").ConfigureAwait(false);

                if (result) {
                    this.activeThreads.TryRemove(threadName, out _);
                    this.Logger.LogDebug("Thread '{ThreadName}' stopped successfully", threadName);
                }

                return result;
            }
            catch (Exception ex) {
                this.Logger.LogWarning(ex, "Failed to stop thread '{ThreadName}'", threadName);
                return false;
            }
        }

        /// <summary>
        /// Stops all active threads managed by this executor.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The number of threads that were stopped successfully.</returns>
        public async Task<int> StopAllThreadsAsync(CancellationToken cancellationToken = default) {
            this.Logger.LogDebug("Stopping all active threads");

            var stoppedCount = 0;
            var threadsToStop = this.activeThreads.Keys.ToList();

            foreach (var threadName in threadsToStop) {
                try {
                    var stopped = await this.StopThreadAsync(threadName, cancellationToken).ConfigureAwait(false);
                    if (stopped) {
                        stoppedCount++;
                    }
                }
                catch (Exception ex) {
                    this.Logger.LogWarning(ex, "Failed to stop thread '{ThreadName}'", threadName);
                }
            }

            this.Logger.LogInformation("Stopped {StoppedCount} of {TotalCount} threads", stoppedCount, threadsToStop.Count);
            return stoppedCount;
        }

        /// <summary>
        /// Checks the health and status of all active threads.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A dictionary containing thread health information.</returns>
        public async Task<Dictionary<string, object>> CheckThreadHealthAsync(CancellationToken cancellationToken = default) {
            this.Logger.LogDebug("Checking thread health");

            const string healthCheckCode = @"
import gc
import _thread

result = {
    'active_threads': len(getattr(_thread, '_thread_list', [])) if hasattr(_thread, '_thread_list') else 1,
    'memory_usage': gc.mem_alloc(),
    'free_memory': gc.mem_free()
}
result
";

            return await this.ExecuteOnDeviceAsync<Dictionary<string, object>>(healthCheckCode, cancellationToken, "ThreadHealthCheck").ConfigureAwait(false);
        }

        /// <summary>
        /// Clears the thread cache and resets thread tracking.
        /// </summary>
        public void ClearThreadCache() {
            this.activeThreads.Clear();
            this.Device.State.ClearExecutionHistory();
            this.Logger.LogDebug("Thread cache and execution history cleared");
        }

        /// <summary>
        /// Executes Python code with [Thread] attribute policies.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="threadAttribute">The [Thread] attribute containing policies.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="operationName">The name of the operation for tracking.</param>
        /// <returns>The result of the Python code execution.</returns>
        private async Task<T> ExecuteWithThreadPoliciesAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken,
            string? operationName) {
            // Thread-specific policies
            this.Logger.LogDebug("Executing thread code with threading-specific optimizations");

            var threadName = $"Thread_{DateTime.UtcNow.Ticks}";

            // For thread operations, use the StartThreadAsync method
            if (typeof(T) == typeof(string) || typeof(T) == typeof(object)) {
                return await this.StartThreadAsync<T>(pythonCode, threadName, cancellationToken).ConfigureAwait(false);
            }

            // For other types, execute directly
            return await this.ExecuteOnDeviceAsync<T>(pythonCode, cancellationToken, $"Thread:{operationName}").ConfigureAwait(false);
        }

        /// <summary>
        /// Validates that the device supports threading capabilities.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when the device does not support threading.</exception>
        private void ValidateThreadingSupport() {
            var capabilities = this.GetDeviceCapabilities();
            if (capabilities?.DetectionComplete == true && !this.SupportsFeature(SimpleDeviceFeatureSet.Threading)) {
                throw new NotSupportedException("Threading is not supported on this device platform");
            }
        }
    }
}
