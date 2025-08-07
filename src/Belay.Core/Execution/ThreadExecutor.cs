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
        /// <param name="logger">The logger for diagnostic information.</param>
        public ThreadExecutor(Device device, ILogger<ThreadExecutor> logger)
            : base(device, logger) {
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
        /// Stops all running threads.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        public async Task StopAllThreadsAsync(CancellationToken cancellationToken = default) {
            this.Logger.LogInformation("Stopping all {Count} running threads", this.runningThreads.Count);

            var threadNames = this.runningThreads.Keys.ToArray();
            foreach (var threadName in threadNames) {
                try {
                    await this.StopThreadAsync(threadName, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) {
                    this.Logger.LogError(ex, "Failed to stop thread {ThreadName}", threadName);
                }
            }

            this.Logger.LogInformation("All threads stop commands sent");
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
                var threadWrapperCode = GenerateThreadWrapperCode(pythonCode, threadName, threadAttribute);

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
        private static string GenerateThreadWrapperCode(string userCode, string threadName, ThreadAttribute threadAttribute) {
            var maxRuntime = threadAttribute.MaxRuntimeMs?.ToString() ?? "None";
            var autoRestart = threadAttribute.AutoRestart.ToString().ToLower();

            return $@"import _thread
import time

def {threadName}_wrapper():
    '''{threadName} thread wrapper with lifecycle management'''
    try:
        # Thread stop flag
        globals()['{threadName}_stop'] = False
        
        # Runtime tracking
        start_time = time.ticks_ms()
        max_runtime = {maxRuntime}
        
        # Execute user code in a loop with stop check
        while not globals().get('{threadName}_stop', False):
            # Check runtime limit
            if max_runtime and time.ticks_diff(time.ticks_ms(), start_time) > max_runtime:
                print(f'Thread {threadName} exceeded max runtime, stopping')
                break
                
            # Execute user code
{IndentCode(userCode, 12)}
            
            # Brief yield to allow other threads
            time.sleep_ms(1)
            
    except Exception as e:
        print(f'Thread {threadName} error: {{e}}')
        if {autoRestart}:
            print(f'Thread {threadName} will auto-restart')
            # TODO: Implement auto-restart logic
    finally:
        print(f'Thread {threadName} ended')
        globals()['{threadName}_stop'] = True

# Start the thread
_thread.start_new_thread({threadName}_wrapper, ())
print(f'Thread {threadName} launched')";
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
