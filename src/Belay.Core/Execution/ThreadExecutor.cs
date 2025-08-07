// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System.Collections.Concurrent;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Belay.Attributes;
    using Microsoft.Extensions.Logging;
    using ThreadPriority = Belay.Attributes.ThreadPriority;

    /// <summary>
    /// Represents a method that has been deployed to a device.
    /// </summary>
    public class DeployedMethod {
        /// <summary>
        /// Gets the name of the method on the device.
        /// </summary>
        public string DeviceMethodName { get; init; } = string.Empty;

        /// <summary>
        /// Gets a hash representing the method's signature.
        /// </summary>
        public string SignatureHash { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents information about a running background thread on the device.
    /// </summary>
    public sealed class RunningThread {
        /// <summary>
        /// Gets the unique identifier for this thread on the device.
        /// </summary>
        public required string ThreadId { get; init; }

        /// <summary>
        /// Gets the name of the thread method.
        /// </summary>
        public required string MethodName { get; init; }

        /// <summary>
        /// Gets the timestamp when the thread was started.
        /// </summary>
        public required DateTime StartedAt { get; init; }

        /// <summary>
        /// Gets a value indicating whether the thread should auto-restart on failure.
        /// </summary>
        public bool AutoRestart { get; init; }

        /// <summary>
        /// Gets the priority level for the thread.
        /// </summary>
        public ThreadPriority Priority { get; init; }

        /// <summary>
        /// Gets the maximum runtime for the thread in milliseconds.
        /// </summary>
        public int? MaxRuntimeMs { get; init; }

        /// <summary>
        /// Gets a value indicating whether the thread is currently running.
        /// </summary>
        public bool IsRunning { get; internal set; } = true;
    }

    /// <summary>
    /// Executor for methods decorated with the [Thread] attribute.
    /// Applies thread-specific policies such as background execution and lifecycle management.
    /// </summary>
    public sealed class ThreadExecutor : BaseExecutor, IDisposable {
        private readonly ConcurrentDictionary<string, RunningThread> runningThreads;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute code on.</param>
        /// <param name="sessionManager">The session manager for device coordination.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        public ThreadExecutor(Device device, Belay.Core.Sessions.IDeviceSessionManager sessionManager, ILogger<ThreadExecutor> logger)
            : base(device, sessionManager, logger) {
            this.runningThreads = new ConcurrentDictionary<string, RunningThread>();
        }

        /// <summary>
        /// Applies thread policies and executes the Python code in a background thread.
        /// Thread methods run asynchronously on the device without blocking the host.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
        /// <param name="callingMethod">The name of the calling method for identification.</param>
        /// <returns>The result of starting the thread (typically void or thread info).</returns>
        public override async Task<T> ApplyPoliciesAndExecuteAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? callingMethod = null) {
            if (string.IsNullOrWhiteSpace(pythonCode)) {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            var methodName = callingMethod ?? "Unknown";
            this.Logger.LogDebug("Thread executor applying policies for method {MethodName}", methodName);

            // Get calling method info for attribute inspection
            var callingMethodInfo = this.GetCallingMethod();
            var threadAttribute = callingMethodInfo?.GetAttribute<ThreadAttribute>();

            if (threadAttribute == null) {
                this.Logger.LogWarning("Method {MethodName} called ThreadExecutor but has no [Thread] attribute", methodName);

                // Execute without thread policies
                return await this.Device.ExecuteAsync<T>(pythonCode, cancellationToken).ConfigureAwait(false);
            }

            // Apply thread policies
            return await this.StartThreadWithPoliciesAsync<T>(pythonCode, methodName, threadAttribute, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets information about all currently running threads.
        /// </summary>
        /// <returns>A collection of running thread information.</returns>
        public IReadOnlyCollection<RunningThread> GetRunningThreads() {
            return this.runningThreads.Values.ToArray();
        }

        /// <summary>
        /// Stops a specific thread by name.
        /// </summary>
        /// <param name="threadName">The name of the thread to stop.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>True if the thread was stopped, false if it wasn't running.</returns>
        public async Task<bool> StopThreadAsync(string threadName, CancellationToken cancellationToken = default) {
            if (this.runningThreads.TryGetValue(threadName, out var thread)) {
                this.Logger.LogInformation("Stopping thread {ThreadName} (ID: {ThreadId})", threadName, thread.ThreadId);

                // Send stop signal to the thread
                var stopCode = $"""
                    # Stop thread {threadName}
                    globals()['{threadName}_stop'] = True
                    """;

                await this.Device.ExecuteAsync(stopCode, cancellationToken).ConfigureAwait(false);

                // Mark as stopped and remove from tracking
                thread.IsRunning = false;
                this.runningThreads.TryRemove(threadName, out _);

                this.Logger.LogInformation("Thread {ThreadName} stopped", threadName);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks the health of all running threads and updates their status.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task CheckThreadHealthAsync(CancellationToken cancellationToken = default) {
            this.Logger.LogDebug("Checking health of {Count} threads", this.runningThreads.Count);

            var healthCheckTasks = this.runningThreads.Values.Select(async thread => {
                try {
                    var healthCheckCode = $@"# Check thread {thread.MethodName} health
import json
import time

thread_active = globals().get('{thread.MethodName}_active', False)
thread_stop = globals().get('{thread.MethodName}_stop', True)
last_heartbeat = globals().get('{thread.MethodName}_last_heartbeat', 0)
current_time = time.ticks_ms()
heartbeat_age = time.ticks_diff(current_time, last_heartbeat) if last_heartbeat > 0 else -1

json.dumps({{
    ""active"": thread_active and not thread_stop,
    ""thread_id"": ""{thread.ThreadId}"",
    ""heartbeat_age_ms"": heartbeat_age,
    ""stopped"": thread_stop
}})";

                    var result = await this.Device.ExecuteAsync<string>(healthCheckCode, cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(result)) {
                        // Parse the health status (simplified - in production you'd use proper JSON parsing)
                        var isActive = result.Contains("\"active\": true");
                        var isStopped = result.Contains("\"stopped\": true");

                        // Extract heartbeat age for monitoring (simplified parsing)
                        var heartbeatMatch = System.Text.RegularExpressions.Regex.Match(result, @"""heartbeat_age_ms"":\s*(\d+)");
                        if (heartbeatMatch.Success && int.TryParse(heartbeatMatch.Groups[1].Value, out var heartbeatAge)) {
                            if (heartbeatAge > 10000) { // 10 seconds without heartbeat
                                this.Logger.LogWarning("Thread {ThreadName} heartbeat is stale ({HeartbeatAge}ms old)", thread.MethodName, heartbeatAge);
                            }
                        }

                        if ((!isActive || isStopped) && thread.IsRunning) {
                            this.Logger.LogInformation("Thread {ThreadName} is no longer running", thread.MethodName);
                            thread.IsRunning = false;
                            this.runningThreads.TryRemove(thread.MethodName, out _);
                        }
                    }
                }
                catch (Exception ex) {
                    this.Logger.LogError(ex, "Failed to check health of thread {ThreadName}", thread.MethodName);
                }
            });

            await Task.WhenAll(healthCheckTasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Stops all running threads.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task<int> StopAllThreadsAsync(CancellationToken cancellationToken = default) {
            this.Logger.LogInformation("Stopping all {Count} running threads", this.runningThreads.Count);

            if (this.runningThreads.IsEmpty) {
                this.Logger.LogDebug("No threads to stop");
                return 0;
            }

            // First, send stop signals to all threads concurrently
            var stopTasks = this.runningThreads.Keys.Select(async threadName => {
                try {
                    return await this.StopThreadAsync(threadName, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) {
                    this.Logger.LogError(ex, "Failed to stop thread {ThreadName}", threadName);
                    return false;
                }
            });

            var stopResults = await Task.WhenAll(stopTasks).ConfigureAwait(false);
            var stoppedCount = stopResults.Count(r => r);

            this.Logger.LogInformation("Sent stop signals to {StoppedCount}/{TotalCount} threads", stoppedCount, stopResults.Length);

            // Give threads time to stop gracefully
            const int gracePeriodMs = 2000;
            this.Logger.LogDebug("Waiting {GracePeriod}ms for threads to stop gracefully", gracePeriodMs);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(gracePeriodMs);

            try {
                // Monitor for all threads to stop
                while (!this.runningThreads.IsEmpty && !cts.Token.IsCancellationRequested) {
                    await Task.Delay(100, cts.Token).ConfigureAwait(false);
                    await this.CheckThreadHealthAsync(cts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) {
                // Expected if grace period expired
            }

            if (!this.runningThreads.IsEmpty) {
                this.Logger.LogWarning("{RemainingCount} threads did not stop gracefully", this.runningThreads.Count);

                // Force cleanup of remaining threads
                var remainingThreads = this.runningThreads.Keys.ToArray();
                foreach (var threadName in remainingThreads) {
                    this.Logger.LogWarning("Force-stopping thread {ThreadName}", threadName);
                    this.runningThreads.TryRemove(threadName, out _);
                }
            }

            this.Logger.LogInformation("Thread shutdown completed");
            return stoppedCount;
        }

        /// <summary>
        /// Starts a thread with thread-specific policies applied.
        /// </summary>
        private async Task<T> StartThreadWithPoliciesAsync<T>(
            string pythonCode,
            string methodName,
            ThreadAttribute threadAttribute,
            CancellationToken cancellationToken) {
            var threadName = !string.IsNullOrEmpty(threadAttribute.Name)
                ? threadAttribute.Name
                : methodName;

            var threadId = Guid.NewGuid().ToString("N")[..8];

            // Check if thread is already running
            if (this.runningThreads.ContainsKey(threadName)) {
                this.Logger.LogWarning("Thread {ThreadName} is already running", threadName);
                return default(T)!;
            }

            this.Logger.LogInformation(
                "Starting thread {ThreadName} (ID: {ThreadId}, priority: {Priority}, autoRestart: {AutoRestart})",
                threadName, threadId, threadAttribute.Priority, threadAttribute.AutoRestart);

            // Create thread info
            var runningThread = new RunningThread {
                ThreadId = threadId,
                MethodName = methodName,
                StartedAt = DateTime.UtcNow,
                AutoRestart = threadAttribute.AutoRestart,
                Priority = threadAttribute.Priority,
                MaxRuntimeMs = threadAttribute.MaxRuntimeMs,
                IsRunning = true,
            };

            // Register the thread
            this.runningThreads.TryAdd(threadName, runningThread);

            try {
                // Prepare the thread wrapper code that includes lifecycle management
                var threadWrapperCode = GenerateThreadWrapperCode(pythonCode, threadName, threadAttribute, threadId);

                // Execute the thread startup code
                var result = await this.Device.ExecuteAsync<T>(threadWrapperCode, cancellationToken).ConfigureAwait(false);

                this.Logger.LogInformation("Thread {ThreadName} started successfully", threadName);
                return result;
            }
            catch (Exception ex) {
                this.Logger.LogError(ex, "Failed to start thread {ThreadName}", threadName);

                // Remove from tracking on failure
                this.runningThreads.TryRemove(threadName, out _);
                throw;
            }
        }

        /// <summary>
        /// Generates Python code that wraps the user's thread code with lifecycle management.
        /// </summary>
        private static string GenerateThreadWrapperCode(string userCode, string threadName, ThreadAttribute threadAttribute, string threadId) {
            var maxRuntime = threadAttribute.MaxRuntimeMs?.ToString() ?? "None";
            var autoRestart = threadAttribute.AutoRestart.ToString().ToLower();

            return $@"import _thread
import time

def {threadName}_wrapper():
    '''{threadName} thread wrapper with lifecycle management'''
    thread_id = '{threadId}'
    print(f'Thread {threadName} ({threadId}) starting')
    
    try:
        # Thread control flags
        globals()['{threadName}_stop'] = False
        globals()['{threadName}_active'] = True
        globals()['{threadName}_last_heartbeat'] = time.ticks_ms()
        
        # Runtime tracking
        start_time = time.ticks_ms()
        max_runtime = {maxRuntime}
        successful_runtime = 0
        
        # Main execution loop
        iteration_count = 0
        while not globals().get('{threadName}_stop', False):
            try:
                # Update heartbeat
                globals()['{threadName}_last_heartbeat'] = time.ticks_ms()
                
                # Check runtime limit
                current_time = time.ticks_ms()
                elapsed = time.ticks_diff(current_time, start_time)
                if max_runtime and elapsed > max_runtime:
                    print(f'Thread {threadName} exceeded max runtime ({maxRuntime}ms), stopping')
                    break
                
                # Execute user code
{IndentCode(userCode, 16)}
                
                iteration_count += 1
                successful_runtime = elapsed
                
                # Reset restart delay on successful execution
                if iteration_count > 10:  # After 10 successful iterations
                    globals()['{threadName}_restart_delay'] = 1
                    
            except Exception as user_ex:
                print(f'Thread {threadName} user code error: ' + str(user_ex))
                # Don't break the loop for user code errors - let auto-restart handle it
                if not {autoRestart}:
                    print(f'Thread {threadName} stopping due to error (no auto-restart)')
                    break
                else:
                    # Brief pause before next iteration
                    time.sleep(1)
            
            # Brief yield to prevent monopolizing the CPU
            time.sleep_ms(10)
            
            # Periodic stop check even if user code doesn't yield
            if iteration_count % 100 == 0:
                if globals().get('{threadName}_stop', False):
                    print(f'Thread {threadName} received stop signal')
                    break
                    
    except Exception as e:
        print(f'Thread {threadName} wrapper error: ' + str(e))
        if {autoRestart}:
            print(f'Thread {threadName} will auto-restart')
            # Implement exponential backoff for auto-restart
            restart_delay = globals().get('{threadName}_restart_delay', 1)
            restart_delay = min(restart_delay * 2, 60)  # Cap at 60 seconds
            globals()['{threadName}_restart_delay'] = restart_delay
            
            print(f'Thread {threadName} restarting in ' + str(restart_delay) + ' seconds')
            
            # Sleep with periodic stop checks
            sleep_end = time.ticks_ms() + (restart_delay * 1000)
            while time.ticks_ms() < sleep_end:
                if globals().get('{threadName}_stop', False):
                    print(f'Thread {threadName} stop requested during restart delay')
                    return
                time.sleep_ms(100)
            
            # Check if we should still restart
            if not globals().get('{threadName}_stop', False):
                print(f'Thread {threadName} auto-restarting (attempt after ' + str(successful_runtime) + 'ms)')
                _thread.start_new_thread({threadName}_wrapper, ())
    finally:
        # Cleanup
        globals()['{threadName}_active'] = False
        globals()['{threadName}_stop'] = True
        print(f'Thread {threadName} ({threadId}) ended after ' + str(iteration_count) + ' iterations')

# Start the thread
_thread.start_new_thread({threadName}_wrapper, ())
print(f'Thread {threadName} launched with ID {threadId}')";
        }

        /// <summary>
        /// Indents code by the specified number of spaces.
        /// </summary>
        private static string IndentCode(string code, int spaces) {
            var indent = new string(' ', spaces);
            return string.Join("\n", code.Split('\n').Select(line =>
                string.IsNullOrWhiteSpace(line) ? line : indent + line));
        }

        /// <summary>
        /// Clears the cache of deployed methods.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ClearCacheAsync() {
            this.Logger.LogInformation("Clearing thread executor method cache");

            // Implement method cache clearing logic
            // This might involve stopping threads and resetting internal state
            await this.StopAllThreadsAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all deployed methods in the thread executor.
        /// </summary>
        /// <returns>A collection of deployed methods.</returns>
        public Task<IReadOnlyCollection<DeployedMethod>> GetDeployedMethodsAsync() {
            // Placeholder implementation
            // In a real scenario, you would track deployed methods during deployment
            this.Logger.LogInformation("Retrieving deployed methods");
            return Task.FromResult<IReadOnlyCollection<DeployedMethod>>(Array.Empty<DeployedMethod>());
        }

        /// <summary>
        /// Disposes of the executor resources.
        /// </summary>
        public void Dispose() {
            // Stop all running threads when disposing
            try {
                this.StopAllThreadsAsync().Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex) {
                this.Logger.LogError(ex, "Error stopping threads during disposal");
            }
        }
    }
}
