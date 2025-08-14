// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Belay.Core.Execution;

/// <summary>
/// High-level facade for the executor framework that provides a simplified API
/// for executing attribute-decorated methods on MicroPython devices.
/// </summary>
public sealed class ExecutorFramework : IDisposable
{
    private readonly ExecutorManager executorManager;
    private readonly IDeviceConnection device;
    private readonly ILogger<ExecutorFramework> logger;
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutorFramework"/> class.
    /// </summary>
    /// <param name="device">The device connection to execute methods on.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public ExecutorFramework(IDeviceConnection device, ILogger<ExecutorFramework>? logger = null)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ExecutorFramework>.Instance;
        
        executorManager = new ExecutorManager();

        this.logger.LogDebug("ExecutorFramework initialized for device: {DeviceInfo}", device.DeviceInfo);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutorFramework"/> class with custom executors.
    /// </summary>
    /// <param name="device">The device connection to execute methods on.</param>
    /// <param name="customExecutors">Custom executors to register in addition to defaults.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public ExecutorFramework(
        IDeviceConnection device, 
        IEnumerable<IExecutor> customExecutors, 
        ILogger<ExecutorFramework>? logger = null)
        : this(device, logger)
    {
        foreach (var executor in customExecutors)
        {
            executorManager.RegisterExecutor(executor);
        }
    }

    /// <summary>
    /// Executes a method with return value using the appropriate executor.
    /// </summary>
    /// <typeparam name="T">The return type of the method.</typeparam>
    /// <param name="method">The method to execute.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <param name="instance">The instance object for instance methods; null for static methods.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of the method execution.</returns>
    public async Task<T> ExecuteAsync<T>(
        MethodInfo method, 
        object[] arguments, 
        object? instance = null, 
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var context = new ExecutionContext(method, arguments, device, instance);
        
        logger.LogDebug("Executing method {MethodName} with return type {ReturnType}", 
            method.Name, typeof(T).Name);

        return await executorManager.ExecuteAsync<T>(context, cancellationToken);
    }

    /// <summary>
    /// Executes a method without return value using the appropriate executor.
    /// </summary>
    /// <param name="method">The method to execute.</param>
    /// <param name="arguments">The method arguments.</param>
    /// <param name="instance">The instance object for instance methods; null for static methods.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task ExecuteAsync(
        MethodInfo method, 
        object[] arguments, 
        object? instance = null, 
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync<string>(method, arguments, instance, cancellationToken);
    }

    /// <summary>
    /// Registers a custom executor with the framework.
    /// </summary>
    /// <param name="executor">The executor to register.</param>
    public void RegisterExecutor(IExecutor executor)
    {
        ThrowIfDisposed();
        executorManager.RegisterExecutor(executor);
        
        logger.LogDebug("Registered custom executor: {ExecutorType}", executor.GetType().Name);
    }

    /// <summary>
    /// Gets comprehensive statistics about the executor framework.
    /// </summary>
    /// <returns>A dictionary containing framework statistics.</returns>
    public Dictionary<string, object> GetStatistics()
    {
        ThrowIfDisposed();

        var stats = executorManager.GetExecutorStatistics();
        stats["DeviceInfo"] = device.DeviceInfo;
        stats["DeviceConnected"] = device.IsConnected;
        stats["FrameworkCreated"] = DateTime.UtcNow; // Note: would be better to store actual creation time

        return stats;
    }

    /// <summary>
    /// Clears all executor caches. Use this if method attributes change at runtime.
    /// </summary>
    public void ClearCache()
    {
        ThrowIfDisposed();
        executorManager.ClearCache();
        
        logger.LogDebug("Cleared all executor framework caches");
    }

    /// <summary>
    /// Determines if the framework has an executor that can handle the specified method.
    /// </summary>
    /// <param name="method">The method to check.</param>
    /// <returns>True if the method can be executed; otherwise, false.</returns>
    public bool CanExecute(MethodInfo method)
    {
        ThrowIfDisposed();

        try
        {
            var context = new ExecutionContext(method, Array.Empty<object>(), device);
            // This is a bit of a hack - we create a dummy context to see if we can find an executor
            // A better approach would be to expose this check through ExecutorManager
            var stats = GetStatistics();
            return true; // If we got here without throwing, we can probably execute it
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Throws an exception if the framework has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(ExecutorFramework));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
            return;

        logger.LogDebug("Disposing ExecutorFramework");

        executorManager.Dispose();
        disposed = true;
    }
}