// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Belay.Attributes;
using Microsoft.Extensions.Logging;

namespace Belay.Core.Execution;

/// <summary>
/// Executor for methods decorated with the TaskAttribute.
/// Handles standard method execution with optional caching and timeout configuration.
/// </summary>
public sealed class TaskExecutor : BaseExecutor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskExecutor"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public TaskExecutor(ILogger<TaskExecutor>? logger = null)
        : base(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TaskExecutor>.Instance)
    {
    }

    /// <inheritdoc />
    public override int Priority => 100; // Standard priority for task execution

    /// <inheritdoc />
    public override bool CanHandle(MethodInfo method)
    {
        return method.GetCustomAttribute<TaskAttribute>() != null;
    }

    /// <inheritdoc />
    protected override void ApplyExecutionPolicies(ExecutionContext context)
    {
        base.ApplyExecutionPolicies(context);

        var taskAttribute = context.Method.GetCustomAttribute<TaskAttribute>();
        if (taskAttribute == null) return;

        // Apply task-specific policies
        context.UseCache = taskAttribute.Cache;
        
        if (taskAttribute.TimeoutMs > 0)
        {
            context.Timeout = TimeSpan.FromMilliseconds(taskAttribute.TimeoutMs);
        }

        // Tasks marked as Exclusive require exclusive device access
        context.RequiresExclusiveAccess = taskAttribute.Exclusive;
    }

    /// <inheritdoc />
    protected override Task<string> GeneratePythonCodeAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var taskAttribute = context.Method.GetCustomAttribute<TaskAttribute>();
        if (taskAttribute == null)
        {
            // Fallback to default generation
            return base.GeneratePythonCodeAsync(context, cancellationToken);
        }

        var pythonCode = GenerateTaskPythonCode(context, taskAttribute);
        return Task.FromResult(pythonCode);
    }

    /// <summary>
    /// Generates Python code specific to TaskAttribute configuration.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="taskAttribute">The task attribute configuration.</param>
    /// <returns>The generated Python code.</returns>
    private string GenerateTaskPythonCode(ExecutionContext context, TaskAttribute taskAttribute)
    {
        var method = context.Method;
        var args = context.Arguments;

        // Use explicit name if provided, otherwise derive from method name
        var functionName = !string.IsNullOrEmpty(taskAttribute.Name) 
            ? taskAttribute.Name 
            : ConvertToPythonCase(method.Name);
        
        // Remove common C# prefixes if no explicit name provided
        if (string.IsNullOrEmpty(taskAttribute.Name))
        {
            if (functionName.StartsWith("get_"))
                functionName = functionName[4..];
            if (functionName.StartsWith("set_"))
                functionName = functionName[4..];
        }

        // Convert arguments to Python representation
        var pythonArgs = args.Select(FormatPythonValue);
        
        return $"{functionName}({string.Join(", ", pythonArgs)})";
    }

    /// <inheritdoc />
    protected override TimeSpan? GetTimeoutFromAttributes(MethodInfo method)
    {
        var taskAttribute = method.GetCustomAttribute<TaskAttribute>();
        
        return taskAttribute?.TimeoutMs > 0 
            ? TimeSpan.FromMilliseconds(taskAttribute.TimeoutMs) 
            : null;
    }

    /// <inheritdoc />
    protected override bool GetCachingPolicyFromAttributes(MethodInfo method)
    {
        var taskAttribute = method.GetCustomAttribute<TaskAttribute>();
        return taskAttribute?.Cache ?? false;
    }
}