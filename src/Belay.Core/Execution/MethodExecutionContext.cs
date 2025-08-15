// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution;

using System.Reflection;

/// <summary>
/// Simple execution context for method calls.
/// Minimal implementation to replace sophisticated execution context system.
/// </summary>
public class MethodExecutionContext {
    /// <summary>
    /// Initializes a new instance of the <see cref="MethodExecutionContext"/> class.
    /// </summary>
    /// <param name="method">The method being executed.</param>
    /// <param name="instance">The instance object (if any).</param>
    /// <param name="parameters">The method parameters.</param>
    public MethodExecutionContext(MethodInfo method, object? instance, object?[]? parameters) {
        this.Method = method;
        this.Instance = instance;
        this.Parameters = parameters;
    }

    /// <summary>
    /// Gets the method being executed.
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// Gets the instance object (if any).
    /// </summary>
    public object? Instance { get; }

    /// <summary>
    /// Gets the method parameters.
    /// </summary>
    public object?[]? Parameters { get; }
}
