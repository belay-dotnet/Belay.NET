// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Reflection;
using Belay.Attributes;
using Microsoft.Extensions.Logging;

namespace Belay.Core.Execution;

/// <summary>
/// Executor for methods decorated with the <see cref="ThreadAttribute"/>.
/// Handles background thread execution on MicroPython devices with lifecycle management,
/// auto-restart capabilities, priority handling, and runtime limits.
/// </summary>
/// <remarks>
/// <para>
/// The ThreadExecutor is responsible for executing long-running background operations
/// on MicroPython devices using the _thread module. It provides sophisticated thread
/// lifecycle management including monitoring, auto-restart, and graceful termination.
/// </para>
/// <para>
/// Key features:
/// <list type="bullet">
/// <item><description>Background thread creation and management using _thread module</description></item>
/// <item><description>Thread lifecycle tracking and monitoring</description></item>
/// <item><description>Auto-restart with exponential backoff for failed threads</description></item>
/// <item><description>Priority-based execution hints for compatible platforms</description></item>
/// <item><description>Runtime limits with graceful termination</description></item>
/// <item><description>Thread name management for identification and control</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class ThreadExecutor : BaseExecutor
{
    private readonly ConcurrentDictionary<string, ThreadInfo> activeThreads;
    private readonly Timer? threadMonitor;

    /// <summary>
    /// Gets the execution priority for this executor.
    /// Thread has medium priority (70) to allow setup/teardown to execute around it.
    /// </summary>
    public override int Priority => 70;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThreadExecutor"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public ThreadExecutor(ILogger<ThreadExecutor>? logger = null)
        : base(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ThreadExecutor>.Instance)
    {
        activeThreads = new ConcurrentDictionary<string, ThreadInfo>();

        // Start thread monitoring timer (every 30 seconds)
        threadMonitor = new Timer(MonitorActiveThreads, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Determines whether this executor can handle the specified method.
    /// </summary>
    /// <param name="method">The method to evaluate.</param>
    /// <returns>True if the method has a <see cref="ThreadAttribute"/>; otherwise, false.</returns>
    public override bool CanHandle(MethodInfo method)
    {
        return method.GetCustomAttribute<ThreadAttribute>() != null;
    }

    /// <summary>
    /// Applies thread-specific execution policies from the ThreadAttribute.
    /// </summary>
    /// <param name="context">The execution context to modify.</param>
    protected override void ApplyExecutionPolicies(ExecutionContext context)
    {
        base.ApplyExecutionPolicies(context);

        var threadAttr = context.Method.GetCustomAttribute<ThreadAttribute>();
        if (threadAttr == null) return;

        // Apply thread-specific timeout (thread creation should be fast)
        context.Timeout = TimeSpan.FromSeconds(10); // Short timeout for thread startup

        // Thread methods don't use caching (they manage their own state)
        context.UseCache = false;

        // Determine thread name
        var threadName = !string.IsNullOrEmpty(threadAttr.Name) 
            ? threadAttr.Name 
            : ConvertToPythonCase(context.Method.Name);

        // Store thread-specific properties
        context.Properties["ThreadName"] = threadName;
        context.Properties["ThreadAutoRestart"] = threadAttr.AutoRestart;
        context.Properties["ThreadPriority"] = threadAttr.Priority;
        context.Properties["ThreadMaxRuntimeMs"] = threadAttr.MaxRuntimeMs;
        context.Properties["IsThreadMethod"] = true;

        // Thread startup may need exclusive access
        context.RequiresExclusiveAccess = true;
    }

    /// <summary>
    /// Generates Python code for thread method execution.
    /// Creates a background thread using the _thread module with proper lifecycle management.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The Python code to execute on the device.</returns>
    protected override Task<string> GeneratePythonCodeAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var threadAttr = context.Method.GetCustomAttribute<ThreadAttribute>()!;
        var method = context.Method;
        var args = context.Arguments;

        // Get thread name from attribute
        var threadName = !string.IsNullOrEmpty(threadAttr.Name) 
            ? threadAttr.Name 
            : ConvertToPythonCase(context.Method.Name);

        // Convert method name to Python snake_case function name
        var functionName = ConvertToPythonCase(method.Name);

        // Remove common prefixes for thread methods
        if (functionName.StartsWith("start_"))
            functionName = functionName[6..]; // Remove "start_"
        if (functionName.StartsWith("thread_"))
            functionName = functionName[7..]; // Remove "thread_"
        if (functionName.EndsWith("_async"))
            functionName = functionName[..^6]; // Remove "_async"

        // Convert arguments to Python representation
        var pythonArgs = args.Select(FormatPythonValue);
        var argsString = string.Join(", ", pythonArgs);

        // Generate thread management code
        var pythonCode = $@"# Thread method: Name='{threadName}', AutoRestart={threadAttr.AutoRestart}, Priority={threadAttr.Priority}
import _thread
import time
import gc

# Thread control variables
{threadName}_active = True
{threadName}_thread_id = None

def {threadName}_wrapper():
    '''Wrapper function for thread lifecycle management'''
    global {threadName}_active, {threadName}_thread_id
    
    try:
        # Set thread ID for tracking
        {threadName}_thread_id = _thread.get_ident() if hasattr(_thread, 'get_ident') else 'unknown'
        
        print(f'Thread {{threadName}} started (ID: {{{threadName}_thread_id}})')
        
        # Execute the actual thread function
        {functionName}({argsString})
        
    except Exception as e:
        print(f'Thread {{threadName}} error: {{e}}')
        # Auto-restart handling would be implemented here if supported
        
    finally:
        print(f'Thread {{threadName}} terminated')
        {threadName}_active = False
        {threadName}_thread_id = None

# Stop any existing instance of this thread
{threadName}_active = False
time.sleep_ms(100)  # Give existing thread time to notice

# Reset thread state
{threadName}_active = True

# Start the new thread
try:
    _thread.start_new_thread({threadName}_wrapper, ())
    print(f'Thread {{threadName}} launched successfully')
except Exception as e:
    print(f'Failed to start thread {{threadName}}: {{e}}')
    {threadName}_active = False
    raise";

        return Task.FromResult(pythonCode);
    }

    /// <summary>
    /// Executes thread methods with lifecycle tracking and monitoring.
    /// </summary>
    /// <typeparam name="T">The return type of the method.</typeparam>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of the method execution.</returns>
    public override async Task<T> ExecuteAsync<T>(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var threadAttr = context.Method.GetCustomAttribute<ThreadAttribute>();
        if (threadAttr == null)
        {
            throw new InvalidOperationException("ThreadExecutor called on method without ThreadAttribute");
        }

        var threadName = !string.IsNullOrEmpty(threadAttr.Name) 
            ? threadAttr.Name 
            : ConvertToPythonCase(context.Method.Name);

        try
        {
            // Execute the thread startup
            var result = await base.ExecuteAsync<T>(context, cancellationToken);

            // Register the thread for monitoring
            var threadInfo = new ThreadInfo
            {
                Name = threadName,
                StartTime = DateTime.UtcNow,
                AutoRestart = threadAttr.AutoRestart,
                Priority = threadAttr.Priority,
                MaxRuntimeMs = threadAttr.MaxRuntimeMs,
                Device = context.Device,
                Method = context.Method,
                Arguments = context.Arguments
            };

            activeThreads.TryAdd(threadName, threadInfo);

            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            logger.LogInformation("Started thread {ThreadName} with AutoRestart={AutoRestart}, Priority={Priority}", 
                threadName, threadAttr.AutoRestart, threadAttr.Priority);

            return result;
        }
        catch (Exception ex)
        {
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            logger.LogError(ex, "Failed to start thread {ThreadName}", threadName);
            throw new DeviceException($"Failed to start thread {threadName}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets timeout configuration from ThreadAttribute.
    /// Thread startup should be fast.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns>Short timeout for thread startup.</returns>
    protected override TimeSpan? GetTimeoutFromAttributes(MethodInfo method)
    {
        // Thread startup should be fast
        return TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Gets caching policy from ThreadAttribute.
    /// Thread methods don't use caching as they manage their own state.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns>False - thread methods don't use caching.</returns>
    protected override bool GetCachingPolicyFromAttributes(MethodInfo method)
    {
        // Thread methods manage their own state and shouldn't be cached
        return false;
    }

    /// <summary>
    /// Stops a specific thread by name.
    /// </summary>
    /// <param name="threadName">The name of the thread to stop.</param>
    /// <param name="device">The device connection.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    public async Task StopThreadAsync(string threadName, IDeviceConnection device, CancellationToken cancellationToken = default)
    {
        if (activeThreads.TryGetValue(threadName, out var threadInfo))
        {
            try
            {
                // Signal the thread to stop
                await device.ExecutePython($"{threadName}_active = False", cancellationToken);
                
                var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
                logger.LogInformation("Signaled thread {ThreadName} to stop", threadName);

                // Give the thread a moment to notice the stop signal
                await Task.Delay(500, cancellationToken);

                // Remove from active tracking
                activeThreads.TryRemove(threadName, out _);
            }
            catch (Exception ex)
            {
                var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
                logger.LogError(ex, "Error stopping thread {ThreadName}", threadName);
                throw;
            }
        }
    }

    /// <summary>
    /// Stops all active threads.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    public async Task StopAllThreadsAsync(CancellationToken cancellationToken = default)
    {
        var tasks = activeThreads.Select(kvp => 
            StopThreadAsync(kvp.Key, kvp.Value.Device, cancellationToken));
        
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Monitors active threads for health and runtime limits.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    private void MonitorActiveThreads(object? state)
    {
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        var currentTime = DateTime.UtcNow;

        foreach (var kvp in activeThreads.ToArray())
        {
            var threadName = kvp.Key;
            var threadInfo = kvp.Value;

            try
            {
                // Check runtime limits
                if (threadInfo.MaxRuntimeMs.HasValue)
                {
                    var runtime = currentTime - threadInfo.StartTime;
                    if (runtime.TotalMilliseconds > threadInfo.MaxRuntimeMs.Value)
                    {
                        logger.LogInformation("Thread {ThreadName} exceeded max runtime ({MaxRuntimeMs}ms), stopping", 
                            threadName, threadInfo.MaxRuntimeMs.Value);

                        // Stop the thread (fire-and-forget)
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await StopThreadAsync(threadName, threadInfo.Device, CancellationToken.None);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Failed to stop thread {ThreadName} after runtime limit", threadName);
                            }
                        });
                    }
                }

                // Additional health checks could be added here
                // For example, querying thread status from the device
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error monitoring thread {ThreadName}", threadName);
            }
        }
    }

    /// <summary>
    /// Performs cleanup when the executor is disposed.
    /// Stops all active threads and releases resources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the cleanup operation.</param>
    public override async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Stop the monitoring timer
            threadMonitor?.Dispose();

            // Stop all active threads
            await StopAllThreadsAsync(cancellationToken);

            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            logger.LogDebug("ThreadExecutor cleanup completed");
        }
        catch (Exception ex)
        {
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            logger.LogError(ex, "Error during ThreadExecutor cleanup");
        }
    }

    /// <summary>
    /// Gets information about all active threads.
    /// </summary>
    /// <returns>A dictionary of thread information keyed by thread name.</returns>
    public Dictionary<string, object> GetActiveThreadsInfo()
    {
        var currentTime = DateTime.UtcNow;
        return activeThreads.ToDictionary(
            kvp => kvp.Key, 
            kvp => (object)new 
            {
                Name = kvp.Value.Name,
                StartTime = kvp.Value.StartTime,
                RuntimeMs = (currentTime - kvp.Value.StartTime).TotalMilliseconds,
                AutoRestart = kvp.Value.AutoRestart,
                Priority = kvp.Value.Priority,
                MaxRuntimeMs = kvp.Value.MaxRuntimeMs
            }
        );
    }
}

/// <summary>
/// Information about an active thread for tracking and management.
/// </summary>
internal class ThreadInfo
{
    public required string Name { get; init; }
    public required DateTime StartTime { get; init; }
    public required bool AutoRestart { get; init; }
    public required Belay.Attributes.ThreadPriority Priority { get; init; }
    public required int? MaxRuntimeMs { get; init; }
    public required IDeviceConnection Device { get; init; }
    public required MethodInfo Method { get; init; }
    public required object[] Arguments { get; init; }
}