// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Belay.Attributes;
using Microsoft.Extensions.Logging;

namespace Belay.Core.Execution;

/// <summary>
/// Executor for methods decorated with the <see cref="TeardownAttribute"/>.
/// Handles device cleanup and resource release methods with order-based execution,
/// error handling policies, and emergency shutdown support.
/// </summary>
/// <remarks>
/// <para>
/// The TeardownExecutor is responsible for executing cleanup methods during
/// device disconnection. It enforces execution order, handles error policies
/// (ignore vs fail), supports critical teardown operations, and applies
/// appropriate timeouts for cleanup operations.
/// </para>
/// <para>
/// Key features:
/// <list type="bullet">
/// <item><description>Order-based execution (lower Order values execute first)</description></item>
/// <item><description>Error handling policies (IgnoreErrors for non-critical cleanup)</description></item>
/// <item><description>Critical teardown support for safety-related operations</description></item>
/// <item><description>Short timeout defaults for responsive disconnection</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class TeardownExecutor : BaseExecutor
{
    /// <summary>
    /// Gets the execution priority for this executor.
    /// Teardown has lower priority (80) to ensure it executes after normal operations.
    /// </summary>
    public override int Priority => 80;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeardownExecutor"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public TeardownExecutor(ILogger<TeardownExecutor>? logger = null)
        : base(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TeardownExecutor>.Instance)
    {
    }

    /// <summary>
    /// Determines whether this executor can handle the specified method.
    /// </summary>
    /// <param name="method">The method to evaluate.</param>
    /// <returns>True if the method has a <see cref="TeardownAttribute"/>; otherwise, false.</returns>
    public override bool CanHandle(MethodInfo method)
    {
        return method.GetCustomAttribute<TeardownAttribute>() != null;
    }

    /// <summary>
    /// Applies teardown-specific execution policies from the TeardownAttribute.
    /// </summary>
    /// <param name="context">The execution context to modify.</param>
    protected override void ApplyExecutionPolicies(ExecutionContext context)
    {
        base.ApplyExecutionPolicies(context);

        var teardownAttr = context.Method.GetCustomAttribute<TeardownAttribute>();
        if (teardownAttr == null) return;

        // Apply teardown-specific timeout (teardown should be fast)
        if (teardownAttr.TimeoutMs > 0)
        {
            context.Timeout = TimeSpan.FromMilliseconds(teardownAttr.TimeoutMs);
        }
        else if (!context.Timeout.HasValue)
        {
            // Default shorter timeout for teardown operations (quick cleanup)
            context.Timeout = TimeSpan.FromSeconds(30);
        }

        // Teardown methods typically don't use caching (they're cleanup operations)
        context.UseCache = false;

        // Store teardown-specific properties for error handling
        context.Properties["TeardownOrder"] = teardownAttr.Order;
        context.Properties["TeardownIgnoreErrors"] = teardownAttr.IgnoreErrors;
        context.Properties["TeardownCritical"] = teardownAttr.Critical;
        context.Properties["IsTeardownMethod"] = true;

        // Teardown may need exclusive access for safe cleanup
        context.RequiresExclusiveAccess = true;
    }

    /// <summary>
    /// Generates Python code for teardown method execution.
    /// Teardown methods typically execute the method body with error protection.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The Python code to execute on the device.</returns>
    protected override Task<string> GeneratePythonCodeAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var teardownAttr = context.Method.GetCustomAttribute<TeardownAttribute>()!;
        var method = context.Method;
        var args = context.Arguments;

        // Convert method name to Python snake_case
        var functionName = ConvertToPythonCase(method.Name);

        // Remove common prefixes for teardown methods
        if (functionName.StartsWith("teardown_"))
            functionName = functionName[9..]; // Remove "teardown_"
        if (functionName.StartsWith("cleanup_"))
            functionName = functionName[8..]; // Remove "cleanup_"
        if (functionName.StartsWith("stop_"))
            functionName = functionName[5..]; // Remove "stop_"
        if (functionName.EndsWith("_async"))
            functionName = functionName[..^6]; // Remove "_async"

        // Convert arguments to Python representation
        var pythonArgs = args.Select(FormatPythonValue);
        
        // Generate function call
        var pythonCode = $"{functionName}({string.Join(", ", pythonArgs)})";

        // Add teardown tracking comment for debugging
        var teardownInfo = $"# Teardown method: Order={teardownAttr.Order}, IgnoreErrors={teardownAttr.IgnoreErrors}, Critical={teardownAttr.Critical}";
        
        // Wrap in error handling if IgnoreErrors is true
        if (teardownAttr.IgnoreErrors)
        {
            pythonCode = $@"{teardownInfo}
try:
    {pythonCode}
except Exception as e:
    print(f'Teardown error (ignored): {{e}}')
    pass  # Ignore teardown errors as requested";
        }
        else
        {
            pythonCode = $"{teardownInfo}\n{pythonCode}";
        }
        
        return Task.FromResult(pythonCode);
    }

    /// <summary>
    /// Gets timeout configuration from TeardownAttribute.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns>The timeout duration if specified; otherwise, default teardown timeout.</returns>
    protected override TimeSpan? GetTimeoutFromAttributes(MethodInfo method)
    {
        var teardownAttr = method.GetCustomAttribute<TeardownAttribute>();
        if (teardownAttr?.TimeoutMs > 0)
        {
            return TimeSpan.FromMilliseconds(teardownAttr.TimeoutMs);
        }

        // Default shorter timeout for teardown operations
        return TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Gets caching policy from TeardownAttribute.
    /// Teardown methods typically don't use caching as they're cleanup operations.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns>False - teardown methods don't use caching.</returns>
    protected override bool GetCachingPolicyFromAttributes(MethodInfo method)
    {
        // Teardown methods are cleanup operations that shouldn't be cached
        return false;
    }

    /// <summary>
    /// Performs cleanup when the executor is disposed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the cleanup operation.</param>
    public override async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        // Log cleanup for teardown executor
        // Note: TeardownExecutor itself doesn't have specific cleanup needs,
        // as it handles cleanup for other operations
        
        await Task.CompletedTask; // No specific cleanup needed
    }

    /// <summary>
    /// Executes teardown methods with special error handling based on IgnoreErrors policy.
    /// </summary>
    /// <typeparam name="T">The return type of the method.</typeparam>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of the method execution.</returns>
    public override async Task<T> ExecuteAsync<T>(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var teardownAttr = context.Method.GetCustomAttribute<TeardownAttribute>();
        if (teardownAttr == null)
        {
            throw new InvalidOperationException("TeardownExecutor called on method without TeardownAttribute");
        }

        try
        {
            return await base.ExecuteAsync<T>(context, cancellationToken);
        }
        catch (Exception ex) when (teardownAttr.IgnoreErrors && !teardownAttr.Critical)
        {
            // For non-critical teardown methods with IgnoreErrors=true, log but don't fail
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            logger.LogWarning(ex, 
                "Teardown method {MethodName} (Order={Order}) failed but errors are ignored",
                context.MethodName, teardownAttr.Order);

            // Return default value for ignored failures
            return default(T)!;
        }
        catch (Exception ex) when (!teardownAttr.IgnoreErrors || teardownAttr.Critical)
        {
            // For critical teardown methods or when IgnoreErrors=false, wrap exception
            var errorType = teardownAttr.Critical ? "Critical" : "Standard";
            throw new DeviceException(
                $"{errorType} teardown method {context.MethodName} (Order={teardownAttr.Order}) failed during device cleanup",
                ex);
        }
    }

    /// <summary>
    /// Executes multiple teardown methods in the correct order.
    /// This is typically called by the device disconnection process.
    /// </summary>
    /// <param name="teardownMethods">The teardown methods to execute.</param>
    /// <param name="device">The device connection.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous teardown operation.</returns>
    public static async Task ExecuteTeardownSequenceAsync(
        IEnumerable<MethodInfo> teardownMethods, 
        IDeviceConnection device, 
        CancellationToken cancellationToken = default)
    {
        // Sort teardown methods by Order (ascending), then by name for deterministic behavior
        var sortedMethods = teardownMethods
            .Select(m => new { Method = m, Attr = m.GetCustomAttribute<TeardownAttribute>()! })
            .Where(x => x.Attr != null)
            .OrderBy(x => x.Attr.Order)
            .ThenBy(x => x.Method.Name)
            .Select(x => x.Method)
            .ToList();

        var executor = new TeardownExecutor();

        // Execute all teardown methods in order
        foreach (var method in sortedMethods)
        {
            try
            {
                var context = new ExecutionContext(method, Array.Empty<object>(), device);
                await executor.ExecuteAsync<object>(context, cancellationToken);
            }
            catch (Exception ex)
            {
                var teardownAttr = method.GetCustomAttribute<TeardownAttribute>()!;
                
                // Log the error but continue with other teardown methods
                // (individual method error handling is done in ExecuteAsync above)
                var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
                logger.LogError(ex, 
                    "Failed to execute teardown method {MethodName} (Order={Order})", 
                    method.Name, teardownAttr.Order);
                
                // Continue with next teardown method unless it was critical
                if (teardownAttr.Critical && !teardownAttr.IgnoreErrors)
                {
                    // For critical methods that don't ignore errors, we still continue
                    // but log it as a serious issue
                    logger.LogCritical(
                        "Critical teardown method {MethodName} failed - device may not be in safe state",
                        method.Name);
                }
            }
        }
    }
}