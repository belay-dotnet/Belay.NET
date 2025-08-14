// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Belay.Attributes;
using Microsoft.Extensions.Logging;

namespace Belay.Core.Execution;

/// <summary>
/// Executor for methods decorated with the <see cref="SetupAttribute"/>.
/// Handles device initialization methods with order-based execution,
/// critical failure handling, and extended timeouts for hardware initialization.
/// </summary>
/// <remarks>
/// <para>
/// The SetupExecutor is responsible for executing initialization methods during
/// device connection. It enforces the execution order, handles critical vs
/// non-critical failures, and applies appropriate timeouts for hardware setup.
/// </para>
/// <para>
/// Key features:
/// <list type="bullet">
/// <item><description>Order-based execution (lower Order values execute first)</description></item>
/// <item><description>Critical failure handling (can fail device connection)</description></item>
/// <item><description>Extended timeout support for hardware initialization</description></item>
/// <item><description>State tracking for initialization completion</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class SetupExecutor : BaseExecutor
{
    /// <summary>
    /// Gets the execution priority for this executor.
    /// Setup has high priority (90) to ensure initialization occurs early.
    /// </summary>
    public override int Priority => 90;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetupExecutor"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public SetupExecutor(ILogger<SetupExecutor>? logger = null)
        : base(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SetupExecutor>.Instance)
    {
    }

    /// <summary>
    /// Determines whether this executor can handle the specified method.
    /// </summary>
    /// <param name="method">The method to evaluate.</param>
    /// <returns>True if the method has a <see cref="SetupAttribute"/>; otherwise, false.</returns>
    public override bool CanHandle(MethodInfo method)
    {
        return method.GetCustomAttribute<SetupAttribute>() != null;
    }

    /// <summary>
    /// Applies setup-specific execution policies from the SetupAttribute.
    /// </summary>
    /// <param name="context">The execution context to modify.</param>
    protected override void ApplyExecutionPolicies(ExecutionContext context)
    {
        base.ApplyExecutionPolicies(context);

        var setupAttr = context.Method.GetCustomAttribute<SetupAttribute>();
        if (setupAttr == null) return;

        // Apply setup-specific timeout (setup often needs more time)
        if (setupAttr.TimeoutMs > 0)
        {
            context.Timeout = TimeSpan.FromMilliseconds(setupAttr.TimeoutMs);
        }
        else if (!context.Timeout.HasValue)
        {
            // Default longer timeout for setup operations
            context.Timeout = TimeSpan.FromMinutes(2);
        }

        // Setup methods typically don't use caching (they're one-time operations)
        context.UseCache = false;

        // Store setup-specific properties for error handling
        context.Properties["SetupOrder"] = setupAttr.Order;
        context.Properties["SetupCritical"] = setupAttr.Critical;
        context.Properties["IsSetupMethod"] = true;

        // Setup may need exclusive access for hardware initialization
        context.RequiresExclusiveAccess = true;
    }

    /// <summary>
    /// Generates Python code for setup method execution.
    /// Setup methods typically execute the method body directly without special handling.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The Python code to execute on the device.</returns>
    protected override Task<string> GeneratePythonCodeAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var setupAttr = context.Method.GetCustomAttribute<SetupAttribute>()!;
        var method = context.Method;
        var args = context.Arguments;

        // Convert method name to Python snake_case
        var functionName = ConvertToPythonCase(method.Name);

        // Remove common prefixes for setup methods
        if (functionName.StartsWith("setup_"))
            functionName = functionName[6..]; // Remove "setup_"
        if (functionName.StartsWith("initialize_"))
            functionName = functionName[11..]; // Remove "initialize_"
        if (functionName.EndsWith("_async"))
            functionName = functionName[..^6]; // Remove "_async"

        // Convert arguments to Python representation
        var pythonArgs = args.Select(FormatPythonValue);
        
        // Generate function call with setup context
        var pythonCode = $"{functionName}({string.Join(", ", pythonArgs)})";

        // Add setup tracking comment for debugging
        var setupInfo = $"# Setup method: Order={setupAttr.Order}, Critical={setupAttr.Critical}";
        
        return Task.FromResult($"{setupInfo}\n{pythonCode}");
    }

    /// <summary>
    /// Gets timeout configuration from SetupAttribute.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns>The timeout duration if specified; otherwise, null.</returns>
    protected override TimeSpan? GetTimeoutFromAttributes(MethodInfo method)
    {
        var setupAttr = method.GetCustomAttribute<SetupAttribute>();
        if (setupAttr?.TimeoutMs > 0)
        {
            return TimeSpan.FromMilliseconds(setupAttr.TimeoutMs);
        }

        // Default longer timeout for setup operations
        return TimeSpan.FromMinutes(2);
    }

    /// <summary>
    /// Gets caching policy from SetupAttribute.
    /// Setup methods typically don't use caching as they're one-time operations.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns>False - setup methods don't use caching.</returns>
    protected override bool GetCachingPolicyFromAttributes(MethodInfo method)
    {
        // Setup methods are typically one-time operations that shouldn't be cached
        return false;
    }

    /// <summary>
    /// Performs cleanup when the executor is disposed.
    /// Ensures any pending setup operations are properly handled.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the cleanup operation.</param>
    public override async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        // Log cleanup for setup executor
        // Note: Setup methods are typically completed during device connection,
        // so cleanup mainly involves logging and state verification
        
        await Task.CompletedTask; // No specific cleanup needed for setup operations
    }

    /// <summary>
    /// Executes setup methods with special error handling for critical vs non-critical setup.
    /// </summary>
    /// <typeparam name="T">The return type of the method.</typeparam>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of the method execution.</returns>
    public override async Task<T> ExecuteAsync<T>(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var setupAttr = context.Method.GetCustomAttribute<SetupAttribute>();
        if (setupAttr == null)
        {
            throw new InvalidOperationException("SetupExecutor called on method without SetupAttribute");
        }

        try
        {
            return await base.ExecuteAsync<T>(context, cancellationToken);
        }
        catch (Exception ex) when (!setupAttr.Critical)
        {
            // For non-critical setup methods, log the error but don't fail the execution
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            logger.LogWarning(ex, 
                "Non-critical setup method {MethodName} (Order={Order}) failed but continuing initialization",
                context.MethodName, setupAttr.Order);

            // Return default value for non-critical failures
            return default(T)!;
        }
        catch (Exception ex) when (setupAttr.Critical)
        {
            // For critical setup methods, wrap the exception with setup context
            throw new DeviceException(
                $"Critical setup method {context.MethodName} (Order={setupAttr.Order}) failed during device initialization",
                ex);
        }
    }
}