// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Belay.Core.Execution;

/// <summary>
/// Base implementation of IExecutor that provides common functionality for all executor types.
/// Handles Python code generation, parameter conversion, and basic execution flow.
/// </summary>
public abstract class BaseExecutor : IExecutor
{
    private readonly ILogger logger;
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseExecutor"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    protected BaseExecutor(ILogger logger)
    {
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    /// <inheritdoc />
    public abstract int Priority { get; }

    /// <inheritdoc />
    public abstract bool CanHandle(MethodInfo method);

    /// <inheritdoc />
    public virtual async Task<T> ExecuteAsync<T>(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        
        logger.LogDebug("Executing method {MethodName} with {ArgCount} arguments using {ExecutorType}",
            context.MethodName, context.Arguments.Length, GetType().Name);

        try
        {
            // Apply execution policies from attributes
            ApplyExecutionPolicies(context);

            // Generate Python code for the method
            var pythonCode = await GeneratePythonCodeAsync(context, cancellationToken);
            
            logger.LogTrace("Generated Python code: {PythonCode}", pythonCode);

            // Execute with timeout and caching support
            return await ExecuteWithPoliciesAsync<T>(context, pythonCode, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute method {MethodName} using {ExecutorType}",
                context.MethodName, GetType().Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync<string>(context, cancellationToken);
    }

    /// <inheritdoc />
    public virtual Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates Python code for the method execution.
    /// Subclasses can override this to implement specialized code generation strategies.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The Python code to execute on the device.</returns>
    protected virtual Task<string> GeneratePythonCodeAsync(ExecutionContext context, CancellationToken cancellationToken = default)
    {
        var pythonCode = GenerateDefaultPythonCode(context);
        return Task.FromResult(pythonCode);
    }

    /// <summary>
    /// Applies execution policies from method attributes to the execution context.
    /// </summary>
    /// <param name="context">The execution context to modify.</param>
    protected virtual void ApplyExecutionPolicies(ExecutionContext context)
    {
        // Default implementation - subclasses can override for attribute-specific policies
        var method = context.Method;

        // Apply common timeout policies
        if (GetTimeoutFromAttributes(method) is TimeSpan timeout)
        {
            context.Timeout = timeout;
        }

        // Apply common caching policies
        context.UseCache = GetCachingPolicyFromAttributes(method);
    }

    /// <summary>
    /// Generates default Python code for method execution using basic function call pattern.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <returns>The generated Python code.</returns>
    protected string GenerateDefaultPythonCode(ExecutionContext context)
    {
        var method = context.Method;
        var args = context.Arguments;

        // Convert method name to Python snake_case
        var functionName = ConvertToPythonCase(method.Name);
        
        // Remove common C# prefixes
        if (functionName.StartsWith("get_"))
            functionName = functionName[4..];
        if (functionName.StartsWith("set_"))
            functionName = functionName[4..];

        // Convert arguments to Python representation
        var pythonArgs = args.Select(FormatPythonValue);
        
        return $"{functionName}({string.Join(", ", pythonArgs)})";
    }

    /// <summary>
    /// Converts a C# method name to Python snake_case convention.
    /// </summary>
    /// <param name="name">The C# method name.</param>
    /// <returns>The Python-style method name.</returns>
    protected static string ConvertToPythonCase(string name)
    {
        var result = new StringBuilder();
        
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
                result.Append('_');
            result.Append(char.ToLower(name[i]));
        }
        
        return result.ToString();
    }

    /// <summary>
    /// Formats a .NET object as a Python value representation.
    /// </summary>
    /// <param name="value">The .NET value to format.</param>
    /// <returns>The Python string representation of the value.</returns>
    protected static string FormatPythonValue(object? value)
    {
        return value switch
        {
            null => "None",
            string s => $"'{s.Replace("'", "\\'")}'",
            bool b => b ? "True" : "False",
            char c => $"'{c}'",
            byte or sbyte or short or ushort or int or uint or long or ulong => value.ToString()!,
            float f => f.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            decimal m => m.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString()!
        };
    }

    /// <summary>
    /// Gets timeout configuration from method attributes.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns>The timeout duration if specified; otherwise, null.</returns>
    protected virtual TimeSpan? GetTimeoutFromAttributes(MethodInfo method)
    {
        // This will be overridden by subclasses to check specific attribute types
        return null;
    }

    /// <summary>
    /// Gets caching policy from method attributes.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns>True if caching should be used; otherwise, false.</returns>
    protected virtual bool GetCachingPolicyFromAttributes(MethodInfo method)
    {
        // This will be overridden by subclasses to check specific attribute types
        return false;
    }

    /// <summary>
    /// Executes Python code with timeout and caching policies applied.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="context">The execution context.</param>
    /// <param name="pythonCode">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The execution result.</returns>
    private async Task<T> ExecuteWithPoliciesAsync<T>(ExecutionContext context, string pythonCode, CancellationToken cancellationToken)
    {
        // Apply timeout if specified
        using var timeoutSource = context.Timeout.HasValue 
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
            
        if (timeoutSource != null)
        {
            timeoutSource.CancelAfter(context.Timeout.Value);
        }

        var effectiveToken = timeoutSource?.Token ?? cancellationToken;

        try
        {
            // Use caching if enabled
            if (context.UseCache)
            {
                var cacheKey = $"executor_{GetType().Name}_{ComputeStableHash(pythonCode)}";
                return await SimpleCache.GetOrCreateAsync(cacheKey, async () =>
                {
                    return await ExecutePythonCodeAsync<T>(context.Device, pythonCode, effectiveToken);
                });
            }

            // Execute without caching
            return await ExecutePythonCodeAsync<T>(context.Device, pythonCode, effectiveToken);
        }
        catch (OperationCanceledException) when (timeoutSource?.Token.IsCancellationRequested == true)
        {
            throw new DeviceException($"Method {context.MethodName} timed out after {context.Timeout}");
        }
    }

    /// <summary>
    /// Executes Python code on the device with proper type handling.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="device">The device connection.</param>
    /// <param name="pythonCode">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The execution result.</returns>
    private static async Task<T> ExecutePythonCodeAsync<T>(IDeviceConnection device, string pythonCode, CancellationToken cancellationToken)
    {
        if (typeof(T) == typeof(string) || typeof(T) == typeof(object))
        {
            var result = await device.ExecutePython(pythonCode, cancellationToken);
            return (T)(object)result;
        }
        else
        {
            return await device.ExecutePython<T>(pythonCode, cancellationToken);
        }
    }

    /// <summary>
    /// Computes a stable hash for Python code to enable consistent caching.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>A stable hash string.</returns>
    private static string ComputeStableHash(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "empty";
            
        var length = input.Length;
        var firstChar = input[0];
        var lastChar = input[length - 1];
        var middle = length > 2 ? input[length / 2] : '0';
        
        return $"{length:X4}_{firstChar:X2}_{middle:X2}_{lastChar:X2}";
    }

    /// <summary>
    /// Throws an exception if the executor has been disposed.
    /// </summary>
    protected void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    /// <summary>
    /// Disposes the executor and releases any resources.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;
            
        logger.LogDebug("Disposing {ExecutorType}", GetType().Name);
        disposed = true;
        
        // Allow subclasses to perform cleanup
        _ = Task.Run(async () =>
        {
            try
            {
                await CleanupAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error during executor cleanup for {ExecutorType}", GetType().Name);
            }
        });
    }
}