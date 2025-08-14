// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Belay.Core.Execution;

/// <summary>
/// Manages a collection of executors and routes method executions to the appropriate executor.
/// Provides thread-safe executor registration and method dispatching.
/// </summary>
public sealed class ExecutorManager : IDisposable
{
    private readonly List<IExecutor> executors;
    private readonly ConcurrentDictionary<MethodInfo, IExecutor> executorCache;
    private readonly SemaphoreSlim exclusiveLock;
    private readonly ILogger<ExecutorManager> logger;
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutorManager"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public ExecutorManager(ILogger<ExecutorManager>? logger = null)
    {
        executors = new List<IExecutor>();
        executorCache = new ConcurrentDictionary<MethodInfo, IExecutor>();
        exclusiveLock = new SemaphoreSlim(1, 1);
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ExecutorManager>.Instance;

        // Register default executors
        RegisterDefaultExecutors();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutorManager"/> class with custom executors.
    /// </summary>
    /// <param name="customExecutors">Custom executors to register.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public ExecutorManager(IEnumerable<IExecutor> customExecutors, ILogger<ExecutorManager>? logger = null)
        : this(logger)
    {
        foreach (var executor in customExecutors)
        {
            RegisterExecutor(executor);
        }
    }

    /// <summary>
    /// Registers an executor with the manager.
    /// </summary>
    /// <param name="executor">The executor to register.</param>
    public void RegisterExecutor(IExecutor executor)
    {
        if (executor == null) throw new ArgumentNullException(nameof(executor));

        lock (executors)
        {
            executors.Add(executor);
            // Sort by priority (highest first)
            executors.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        // Clear cache since executor priorities may have changed
        executorCache.Clear();

        logger.LogDebug("Registered executor {ExecutorType} with priority {Priority}", 
            executor.GetType().Name, executor.Priority);
    }

    /// <summary>
    /// Executes a method with return value using the appropriate executor.
    /// </summary>
    /// <typeparam name="T">The return type of the method.</typeparam>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of the method execution.</returns>
    public async Task<T> ExecuteAsync<T>(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var executor = GetExecutorForMethod(context.Method);
        if (executor == null)
        {
            throw new InvalidOperationException(
                $"No executor found for method {context.MethodName}. " +
                "Ensure the method has appropriate attributes or register a custom executor.");
        }

        // Handle exclusive access requirement
        if (context.RequiresExclusiveAccess)
        {
            await exclusiveLock.WaitAsync(cancellationToken);
            try
            {
                return await executor.ExecuteAsync<T>(context, cancellationToken);
            }
            finally
            {
                exclusiveLock.Release();
            }
        }
        else
        {
            return await executor.ExecuteAsync<T>(context, cancellationToken);
        }
    }

    /// <summary>
    /// Executes a method without return value using the appropriate executor.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync<string>(context, cancellationToken);
    }

    /// <summary>
    /// Gets statistics about registered executors.
    /// </summary>
    /// <returns>A dictionary containing executor statistics.</returns>
    public Dictionary<string, object> GetExecutorStatistics()
    {
        lock (executors)
        {
            return new Dictionary<string, object>
            {
                ["TotalExecutors"] = executors.Count,
                ["ExecutorTypes"] = executors.Select(e => e.GetType().Name).ToArray(),
                ["CachedMethods"] = executorCache.Count,
                ["ExecutorsByPriority"] = executors
                    .GroupBy(e => e.Priority)
                    .ToDictionary(g => g.Key.ToString(), g => g.Select(e => e.GetType().Name).ToArray())
            };
        }
    }

    /// <summary>
    /// Clears the executor cache. Use this if method attributes change at runtime.
    /// </summary>
    public void ClearCache()
    {
        executorCache.Clear();
        logger.LogDebug("Cleared executor cache");
    }

    /// <summary>
    /// Gets the executor that can handle the specified method.
    /// Uses caching for performance.
    /// </summary>
    /// <param name="method">The method to find an executor for.</param>
    /// <returns>The executor that can handle the method, or null if none found.</returns>
    private IExecutor? GetExecutorForMethod(MethodInfo method)
    {
        // Check cache first
        if (executorCache.TryGetValue(method, out var cachedExecutor))
        {
            return cachedExecutor;
        }

        // Find the first executor that can handle this method (highest priority first)
        IExecutor? foundExecutor = null;
        lock (executors)
        {
            foundExecutor = executors.FirstOrDefault(e => e.CanHandle(method));
        }

        // Cache the result (even if null)
        if (foundExecutor != null)
        {
            executorCache.TryAdd(method, foundExecutor);
            logger.LogTrace("Method {MethodName} will be handled by {ExecutorType}", 
                method.Name, foundExecutor.GetType().Name);
        }
        else
        {
            logger.LogWarning("No executor found for method {MethodName}", method.Name);
        }

        return foundExecutor;
    }

    /// <summary>
    /// Registers the default set of executors.
    /// </summary>
    private void RegisterDefaultExecutors()
    {
        // Register TaskExecutor as the primary executor
        RegisterExecutor(new TaskExecutor());

        logger.LogDebug("Registered default executors");
    }

    /// <summary>
    /// Throws an exception if the manager has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(ExecutorManager));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
            return;

        logger.LogDebug("Disposing ExecutorManager");

        // Dispose all executors
        lock (executors)
        {
            foreach (var executor in executors)
            {
                if (executor is IDisposable disposableExecutor)
                {
                    try
                    {
                        disposableExecutor.Dispose();
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Error disposing executor {ExecutorType}", 
                            executor.GetType().Name);
                    }
                }
            }
            executors.Clear();
        }

        // Clear cache
        executorCache.Clear();

        // Dispose semaphore
        exclusiveLock.Dispose();

        disposed = true;
    }
}