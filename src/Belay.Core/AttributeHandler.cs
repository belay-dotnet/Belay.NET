// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

using System.Reflection;
using System.Text;
using Belay.Attributes;
using Belay.Core.Security;

/// <summary>
/// Simple attribute handler that replaces the complex executor hierarchy.
/// Handles method attributes with direct, understandable logic.
/// </summary>
public static class AttributeHandler {
    /// <summary>
    /// Executes a method with attribute-based Python code generation.
    /// </summary>
    /// <typeparam name="T">The return type of the method.</typeparam>
    /// <param name="device">The device connection to execute on.</param>
    /// <param name="method">The method info with attributes.</param>
    /// <param name="args">The method arguments.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of the method execution.</returns>
    public static async Task<T> ExecuteMethod<T>(IDeviceConnection device, MethodInfo method, object[] args, CancellationToken cancellationToken = default) {
        var pythonCode = GeneratePythonCode(method, args);
        var policies = GetExecutionPolicies(method);

        return await ExecuteWithPolicies<T>(device, pythonCode, policies, cancellationToken);
    }

    /// <summary>
    /// Executes a method without return value (void/Task).
    /// </summary>
    /// <param name="device">The device connection to execute on.</param>
    /// <param name="method">The method info with attributes.</param>
    /// <param name="args">The method arguments.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task ExecuteMethod(IDeviceConnection device, MethodInfo method, object[] args, CancellationToken cancellationToken = default) {
        await ExecuteMethod<string>(device, method, args, cancellationToken);
    }

    private static async Task<T> ExecuteWithPolicies<T>(IDeviceConnection device, string pythonCode, ExecutionPolicies policies, CancellationToken cancellationToken) {
        // Apply timeout if specified
        using var timeoutSource = policies.Timeout.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;

        if (timeoutSource != null) {
            timeoutSource.CancelAfter(policies.Timeout.Value);
        }

        var effectiveToken = timeoutSource?.Token ?? cancellationToken;

        try {
            // Use caching if enabled
            if (policies.Cache) {
                var cacheKey = $"device_exec_{ComputeStableHash(pythonCode)}";
                return await SimpleCache.GetOrCreateAsync(cacheKey, async () => {
                    if (typeof(T) == typeof(string) || typeof(T) == typeof(object)) {
                        var result = await device.ExecutePython(pythonCode, effectiveToken);
                        return (T)(object)result;
                    }
                    else {
                        return await device.ExecutePython<T>(pythonCode, effectiveToken);
                    }
                });
            }

            // Execute without caching
            if (typeof(T) == typeof(string) || typeof(T) == typeof(object)) {
                var result = await device.ExecutePython(pythonCode, effectiveToken);
                return (T)(object)result;
            }
            else {
                return await device.ExecutePython<T>(pythonCode, effectiveToken);
            }
        }
        catch (OperationCanceledException ex) when (timeoutSource?.IsCancellationRequested == true) {
            var timeoutMs = policies.Timeout?.TotalMilliseconds ?? 30000;
            throw new DeviceException($"Device operation timed out after {timeoutMs}ms", ex);
        }
    }

    private static string GeneratePythonCode(MethodInfo method, object[] args) {
        // Check for direct Python code attribute
        var codeAttr = method.GetCustomAttribute<PythonCodeAttribute>();
        if (codeAttr != null) {
            return SubstituteParameters(codeAttr.Code, method, args);
        }

        // For TaskAttribute, ThreadAttribute, etc., generate function calls
        var taskAttr = method.GetCustomAttribute<TaskAttribute>();
        if (taskAttr != null) {
            return GenerateTaskCall(method, args, taskAttr);
        }

        var threadAttr = method.GetCustomAttribute<ThreadAttribute>();
        if (threadAttr != null) {
            return GenerateTaskCall(method, args, null);
        }

        var setupAttr = method.GetCustomAttribute<SetupAttribute>();
        if (setupAttr != null) {
            return GenerateTaskCall(method, args, null);
        }

        var teardownAttr = method.GetCustomAttribute<TeardownAttribute>();
        if (teardownAttr != null) {
            return GenerateTaskCall(method, args, null);
        }

        // Fallback: generate simple function call
        return GenerateSimpleFunctionCall(method, args);
    }

    private static string SubstituteParameters(string pythonCode, MethodInfo method, object[] args) {
        var codeAttr = method.GetCustomAttribute<PythonCodeAttribute>();
        if (codeAttr?.EnableParameterSubstitution != true) {
            return pythonCode;
        }

        var parameters = method.GetParameters();
        var paramDict = new Dictionary<string, object?>();

        for (int i = 0; i < parameters.Length && i < args.Length; i++) {
            var paramName = parameters[i].Name!;
            if (!InputValidator.IsValidParameterName(paramName)) {
                throw new ArgumentException($"Invalid parameter name for Python code substitution: {paramName}", nameof(method));
            }

            paramDict[paramName] = args[i];
        }

        // Use secure template substitution from InputValidator
        return InputValidator.CreateSafeCodeFromTemplate(pythonCode, paramDict);
    }

    private static string GenerateTaskCall(MethodInfo method, object[] args, TaskAttribute? taskAttr) {
        var functionName = taskAttr?.Name ?? method.Name;

        // Convert C# naming to Python naming if no explicit name provided
        if (taskAttr?.Name == null) {
            if (functionName.StartsWith("Get")) {
                functionName = functionName[3..];
            }

            if (functionName.StartsWith("Set")) {
                functionName = functionName[3..];
            }

            functionName = ToPythonCase(functionName);
        }

        var argStrings = args.Select(FormatPythonValue);
        return $"{functionName}({string.Join(", ", argStrings)})";
    }

    private static string GenerateSimpleFunctionCall(MethodInfo method, object[] args) {
        var functionName = method.Name;

        // Convert C# naming to Python naming
        if (functionName.StartsWith("Get")) {
            functionName = functionName[3..];
        }

        if (functionName.StartsWith("Set")) {
            functionName = functionName[3..];
        }

        functionName = ToPythonCase(functionName);

        var argStrings = args.Select(FormatPythonValue);
        return $"{functionName}({string.Join(", ", argStrings)})";
    }

    private static string FormatPythonValue(object? value) {
        return value switch {
            null => "None",
            bool b => b ? "True" : "False",
            string s => $"'{InputValidator.SanitizePythonString(s)}'",
            char c => $"'{InputValidator.SanitizePythonString(c.ToString())}'",
            byte or sbyte or short or ushort or int or uint or long or ulong => value.ToString()!,
            float f => f.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => $"'{InputValidator.SanitizePythonString(value.ToString()!)}'",
        };
    }

    private static string ToPythonCase(string name) {
        var result = new StringBuilder();

        for (int i = 0; i < name.Length; i++) {
            if (i > 0 && char.IsUpper(name[i])) {
                result.Append('_');
            }

            result.Append(char.ToLower(name[i]));
        }

        return result.ToString();
    }

    private static ExecutionPolicies GetExecutionPolicies(MethodInfo method) {
        var policies = new ExecutionPolicies();

        // Check for timeout specifications in attributes (TaskAttribute takes precedence)
        var taskAttr = method.GetCustomAttribute<TaskAttribute>();
        if (taskAttr != null) {
            policies.Cache = taskAttr.Cache;
            if (taskAttr.TimeoutMs > 0) {
                policies.Timeout = TimeSpan.FromMilliseconds(taskAttr.TimeoutMs);
            }
        }

        // Check SetupAttribute for timeout if not already set
        if (policies.Timeout == null) {
            var setupAttr = method.GetCustomAttribute<SetupAttribute>();
            if (setupAttr?.TimeoutMs > 0) {
                policies.Timeout = TimeSpan.FromMilliseconds(setupAttr.TimeoutMs);
            }
        }

        // Check TeardownAttribute for timeout if not already set
        if (policies.Timeout == null) {
            var teardownAttr = method.GetCustomAttribute<TeardownAttribute>();
            if (teardownAttr?.TimeoutMs > 0) {
                policies.Timeout = TimeSpan.FromMilliseconds(teardownAttr.TimeoutMs);
            }
        }

        var threadAttr = method.GetCustomAttribute<ThreadAttribute>();
        if (threadAttr != null) {
            // ThreadAttribute implies exclusive execution
            policies.RequiresLock = true;
        }

        return policies;
    }

    private sealed class ExecutionPolicies {
        public bool RequiresLock { get; set; }

        public TimeSpan? Timeout { get; set; }

        public bool Cache { get; set; }
    }

    /// <summary>
    /// Computes a stable hash for Python code to avoid GetHashCode collisions.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>A stable hash string.</returns>
    private static string ComputeStableHash(string input) {
        // Use a simple but stable hash - combine string length with first/last chars
        // This is much more collision-resistant than GetHashCode()
        if (string.IsNullOrEmpty(input)) {
            return "empty";
        }

        var length = input.Length;
        var firstChar = input[0];
        var lastChar = input[length - 1];
        var middle = length > 2 ? input[length / 2] : '0';

        return $"{length:X4}_{firstChar:X2}_{middle:X2}_{lastChar:X2}";
    }
}
