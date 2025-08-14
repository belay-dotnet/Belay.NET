// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;

namespace Belay.Core.Execution;

/// <summary>
/// Represents an executor that can handle specific types of attribute-decorated methods.
/// This interface enables specialized execution strategies for different attribute types
/// (Task, Setup, Thread, Teardown) while maintaining a consistent execution contract.
/// </summary>
public interface IExecutor
{
    /// <summary>
    /// Gets the priority of this executor. Higher values indicate higher priority.
    /// Used to determine execution order when multiple executors can handle the same method.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Determines whether this executor can handle the specified method.
    /// </summary>
    /// <param name="method">The method to evaluate.</param>
    /// <returns>True if this executor can handle the method; otherwise, false.</returns>
    bool CanHandle(MethodInfo method);

    /// <summary>
    /// Executes a method with a return value.
    /// </summary>
    /// <typeparam name="T">The return type of the method.</typeparam>
    /// <param name="context">The execution context containing method, arguments, and device connection.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of the method execution.</returns>
    Task<T> ExecuteAsync<T>(ExecutionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a method without a return value.
    /// </summary>
    /// <param name="context">The execution context containing method, arguments, and device connection.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs any necessary cleanup for the executor.
    /// Called when the executor is no longer needed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task CleanupAsync(CancellationToken cancellationToken = default);
}