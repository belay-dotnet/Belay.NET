// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System;
    using System.Collections.Concurrent;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Belay.Attributes;
    using Belay.Core.Exceptions;
    using Belay.Core.Sessions;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Base class for all method executors that provides common functionality for applying policies around Device.ExecuteAsync calls.
    /// </summary>
    public abstract class BaseExecutor : IExecutor {
        /// <summary>
        /// Gets the device instance to execute Python code on.
        /// </summary>
        protected Device Device { get; }

        /// <summary>
        /// Gets the session manager for coordinating device sessions.
        /// </summary>
        protected IDeviceSessionManager SessionManager { get; }

        /// <summary>
        /// Gets the current device session if available.
        /// </summary>
        protected IDeviceSession? CurrentSession { get; private set; }

        /// <summary>
        /// Gets logger for diagnostic information.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the error mapper for mapping exceptions.
        /// </summary>
        protected IErrorMapper? ErrorMapper { get; }

        /// <summary>
        /// Gets the execution context service for secure method context access.
        /// </summary>
        protected IExecutionContextService ExecutionContextService { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute Python code on.</param>
        /// <param name="sessionManager">The session manager for device coordination.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="errorMapper">Optional error mapper for exception handling.</param>
        /// <param name="executionContextService">Optional execution context service (creates default if null).</param>
        protected BaseExecutor(Device device, IDeviceSessionManager sessionManager, ILogger logger, IErrorMapper? errorMapper = null, IExecutionContextService? executionContextService = null) {
            this.Device = device ?? throw new ArgumentNullException(nameof(device));
            this.SessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            this.Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.ErrorMapper = errorMapper;
            this.ExecutionContextService = executionContextService ?? new ExecutionContextService();
        }

        /// <summary>
        /// Applies executor-specific policies around Python code execution.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute on the device.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="callingMethod">The method that initiated this execution (optional).</param>
        /// <returns>The result of the Python code execution with policies applied.</returns>
        public abstract Task<T> ApplyPoliciesAndExecuteAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? callingMethod = null);

        /// <summary>
        /// Applies executor-specific policies around Python code execution without returning a value.
        /// </summary>
        /// <param name="pythonCode">The Python code to execute on the device.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="callingMethod">The method that initiated this execution (optional).</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        public virtual async Task ApplyPoliciesAndExecuteAsync(
            string pythonCode,
            CancellationToken cancellationToken = default,
            [CallerMemberName] string? callingMethod = null) {
            if (string.IsNullOrWhiteSpace(pythonCode)) {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            await this.ApplyPoliciesAndExecuteAsync<object>(pythonCode, cancellationToken, callingMethod).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes Python code directly on the device without additional policies.
        /// This is used as the final execution step after policies have been applied.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The result of the Python code execution.</returns>
        protected async Task<T> ExecuteOnDeviceAsync<T>(string pythonCode, CancellationToken cancellationToken = default) {
            return await this.SessionManager.ExecuteInSessionAsync(
                this.Device.Communication,
                async session => {
                    // Store current session for access by other methods
                    this.CurrentSession = session;

                    try {
                        this.Logger.LogDebug(
                            "Executing Python code in session {SessionId}: {Code}",
                            session.SessionId,
                            pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

                        // Check if we can optimize execution using session context
                        var result = await this.ExecuteWithSessionOptimizationAsync<T>(session, pythonCode, cancellationToken).ConfigureAwait(false);

                        this.Logger.LogDebug(
                            "Python code execution completed in session {SessionId}",
                            session.SessionId);

                        return result;
                    }
                    finally {
                        this.CurrentSession = null;
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes Python code directly on the device without returning a value.
        /// </summary>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        protected async Task ExecuteOnDeviceAsync(string pythonCode, CancellationToken cancellationToken = default) {
            await this.ExecuteOnDeviceAsync<object>(pythonCode, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes an operation within a session context, providing access to session state and resources.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="operation">The operation to execute within the session.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The result of the operation.</returns>
        protected async Task<T> ExecuteInSessionAsync<T>(
            Func<IDeviceSession, Task<T>> operation,
            CancellationToken cancellationToken = default) {
            if (operation == null) {
                throw new ArgumentNullException(nameof(operation));
            }

            return await this.SessionManager.ExecuteInSessionAsync(this.Device.Communication, operation, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes an operation within a session context without returning a value.
        /// </summary>
        /// <param name="operation">The operation to execute within the session.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        protected async Task ExecuteInSessionAsync(
            Func<IDeviceSession, Task> operation,
            CancellationToken cancellationToken = default) {
            if (operation == null) {
                throw new ArgumentNullException(nameof(operation));
            }

            await this.SessionManager.ExecuteInSessionAsync(this.Device.Communication, operation, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the current method execution context (secure replacement for stack frame inspection).
        /// </summary>
        /// <returns>The current method execution context, or null if not available.</returns>
        protected IMethodExecutionContext? GetCurrentMethodContext() {
            return this.ExecutionContextService.Current;
        }

        /// <summary>
        /// Gets the calling method information from the execution context.
        /// SECURITY NOTE: This is a secure replacement for stack frame reflection.
        /// </summary>
        /// <param name="skipFrames">Deprecated parameter - kept for compatibility but ignored.</param>
        /// <returns>The calling method information, or null if not available.</returns>
        [Obsolete("Use GetCurrentMethodContext() instead. Stack frame inspection is a security risk.", error: false)]
        protected MethodInfo? GetCallingMethod(int skipFrames = 2) {
            // Log warning about deprecated method usage
            this.Logger.LogWarning(
                "GetCallingMethod() is deprecated and insecure. Use GetCurrentMethodContext() instead. " +
                "Stack frame inspection is being phased out for security reasons.");

            // Return method from secure context instead of stack inspection
            return this.GetCurrentMethodContext()?.Method;
        }

        /// <summary>
        /// Generates a cache key for the given Python code and parameters.
        /// </summary>
        /// <param name="pythonCode">The Python code.</param>
        /// <param name="parameters">Optional parameters that affect the cache key.</param>
        /// <returns>A cache key string.</returns>
        protected static string GenerateCacheKey(string pythonCode, params object?[]? parameters) {
            // Use deterministic hashing instead of GetHashCode for cache stability
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var codeBytes = System.Text.Encoding.UTF8.GetBytes(pythonCode);
            var codeHash = Convert.ToHexString(sha256.ComputeHash(codeBytes))[..8];

            if (parameters == null || parameters.Length == 0) {
                return codeHash;
            }

            var paramString = string.Join("|", parameters.Select(p => p?.ToString() ?? "null"));
            var paramBytes = System.Text.Encoding.UTF8.GetBytes(paramString);
            var paramHash = Convert.ToHexString(sha256.ComputeHash(paramBytes))[..8];
            return $"{codeHash}:{paramHash}";
        }

        /// <summary>
        /// Creates a timeout cancellation token source if a timeout is specified.
        /// </summary>
        /// <param name="timeoutMs">The timeout in milliseconds, or null for no timeout.</param>
        /// <returns>A cancellation token source, or null if no timeout specified.</returns>
        protected static CancellationTokenSource? CreateTimeoutCts(int? timeoutMs) {
            return timeoutMs.HasValue ? new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs.Value)) : null;
        }

        /// <summary>
        /// Combines a cancellation token with an optional timeout cancellation token source.
        /// </summary>
        /// <param name="cancellationToken">The original cancellation token.</param>
        /// <param name="timeoutCts">Optional timeout cancellation token source.</param>
        /// <param name="linkedCts">Output parameter for the linked token source that needs disposal.</param>
        /// <returns>A combined cancellation token.</returns>
        protected static CancellationToken CombineCancellationTokens(CancellationToken cancellationToken, CancellationTokenSource? timeoutCts, out CancellationTokenSource? linkedCts) {
            linkedCts = null;

            if (timeoutCts == null) {
                return cancellationToken;
            }

            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            return linkedCts.Token;
        }

        /// <summary>
        /// Converts a result object to the expected type.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="result">The result to convert.</param>
        /// <returns>The converted result.</returns>
        protected static T ConvertResult<T>(object? result) {
            if (result is T directResult) {
                return directResult;
            }

            if (result == null) {
                return default(T)!;
            }

            try {
                return (T)Convert.ChangeType(result, typeof(T));
            }
            catch (Exception) {
                return default(T)!;
            }
        }


        /// <summary>
        /// Executes a method with attribute-specific policies applied.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="method">The method to execute.</param>
        /// <param name="instance">The instance to invoke the method on (null for static methods).</param>
        /// <param name="parameters">The parameters to pass to the method.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
        /// <returns>The result of the method execution.</returns>
        public virtual async Task<T> ExecuteAsync<T>(MethodInfo method, object? instance, object?[]? parameters = null, CancellationToken cancellationToken = default) {
            if (method == null) {
                throw new ArgumentNullException(nameof(method));
            }

            // Generate Python code for the method invocation
            var pythonCode = this.GeneratePythonMethodCall(method, instance, parameters);

            this.Logger.LogDebug(
                "Executing method {MethodName} as Python code: {Code}",
                method.Name, pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

            // Execute using the policy-based system
            return await this.ApplyPoliciesAndExecuteAsync<T>(pythonCode, cancellationToken, method.Name).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a method without returning a value.
        /// </summary>
        /// <param name="method">The method to execute.</param>
        /// <param name="instance">The instance to invoke the method on (null for static methods).</param>
        /// <param name="parameters">The parameters to pass to the method.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task ExecuteAsync(MethodInfo method, object? instance, object?[]? parameters = null, CancellationToken cancellationToken = default) {
            await this.ExecuteAsync<object>(method, instance, parameters, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Validates that a method can be handled by this executor.
        /// </summary>
        /// <param name="method">The method to validate.</param>
        /// <returns>True if the method can be handled, false otherwise.</returns>
        public abstract bool CanHandle(MethodInfo method);

        /// <summary>
        /// Generates Python code to invoke the specified method with parameters.
        /// This method handles three patterns:
        /// 1. Methods that return Python code strings (most common)
        /// 2. Methods with PythonCodeAttribute containing embedded code
        /// 3. Simple expression methods that can be transpiled.
        /// </summary>
        /// <param name="method">The method to generate code for.</param>
        /// <param name="instance">The instance to invoke on.</param>
        /// <param name="parameters">The method parameters.</param>
        /// <returns>Python code that represents the method execution.</returns>
        protected virtual string GeneratePythonMethodCall(MethodInfo method, object? instance, object?[]? parameters) {
            // Strategy 1: Check if method has embedded Python code attribute
            var pythonCode = this.GetEmbeddedPythonCode(method, parameters);
            if (pythonCode != null) {
                return pythonCode;
            }

            // Strategy 2: Try to invoke the method if it returns a string (Python code)
            pythonCode = this.TryInvokeMethodForPythonCode(method, instance, parameters);
            if (pythonCode != null) {
                return pythonCode;
            }

            // Strategy 3: Generate a device method call (assumes method is already deployed)
            pythonCode = this.GenerateDeviceMethodCall(method, parameters);
            if (pythonCode != null) {
                return pythonCode;
            }

            // Strategy 4: Fallback - simple expression transpilation (future enhancement)
            throw new InvalidOperationException(
                $"Unable to generate Python code for method '{method.Name}'. " +
                "Methods must either: " +
                "1) Return a Python code string when invoked, " +
                "2) Have a PythonCode attribute with embedded code, " +
                "3) Be deployed to the device beforehand, or " +
                "4) Be simple expressions (not yet implemented).");
        }

        /// <summary>
        /// Generates a Python parameter list from the method parameters.
        /// </summary>
        /// <param name="parameters">The parameters to convert.</param>
        /// <returns>A string representing the Python parameter list.</returns>
        protected virtual string GenerateParameterList(object?[]? parameters) {
            if (parameters == null || parameters.Length == 0) {
                return string.Empty;
            }

            var pythonParams = new List<string>();

            foreach (var param in parameters) {
                pythonParams.Add(this.ConvertToPythonValue(param));
            }

            return string.Join(", ", pythonParams);
        }

        /// <summary>
        /// Converts a .NET value to its Python string representation.
        /// Handles complex types including arrays, dictionaries, and custom objects.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>Python representation of the value.</returns>
        protected virtual string ConvertToPythonValue(object? value) {
            return this.ConvertToPythonValueWithCircularCheck(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        /// <summary>
        /// Converts a .NET value to its Python string representation with circular reference protection.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <param name="visitedObjects">Set of objects already being converted to detect circular references.</param>
        /// <returns>Python representation of the value.</returns>
        private string ConvertToPythonValueWithCircularCheck(object? value, HashSet<object> visitedObjects) {
            return value switch {
                null => "None",
                bool b => b ? "True" : "False",
                byte or sbyte or short or ushort or int or uint => value.ToString()!,
                long or ulong => value.ToString()!,
                float f => f.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
                double d => d.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
                decimal dec => dec.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
                string s => this.ConvertStringToPython(s),
                DateTime dt => $"'{dt:yyyy-MM-ddTHH:mm:ss}'",
                byte[] bytes => this.ConvertBytesToPython(bytes),
                System.Collections.IList list => this.ConvertListToPythonWithCircularCheck(list, visitedObjects),
                System.Collections.IDictionary dict => this.ConvertDictionaryToPythonWithCircularCheck(dict, visitedObjects),
                Enum enumValue => this.ConvertEnumToPython(enumValue),
                _ when this.IsSimpleType(value.GetType()) => $"'{value}'",
                _ => this.ConvertComplexObjectToPython(value),
            };
        }

        /// <summary>
        /// Converts a string to Python string literal with proper escaping.
        /// </summary>
        /// <returns></returns>
        protected virtual string ConvertStringToPython(string str) {
            if (str == null) {
                return "None";
            }

            // Handle common escape sequences
            var escaped = str
                .Replace("\\", "\\\\") // Backslash first
                .Replace("'", "\\'") // Single quotes
                .Replace("\"", "\\\"") // Double quotes
                .Replace("\n", "\\n") // Newline
                .Replace("\r", "\\r") // Carriage return
                .Replace("\t", "\\t");  // Tab

            return $"'{escaped}'";
        }

        /// <summary>
        /// Converts a byte array to Python bytes literal.
        /// </summary>
        /// <returns></returns>
        protected virtual string ConvertBytesToPython(byte[] bytes) {
            var hex = Convert.ToHexString(bytes);
            return $"bytes.fromhex('{hex}')";
        }

        /// <summary>
        /// Converts a list/array to Python list literal.
        /// </summary>
        /// <returns></returns>
        protected virtual string ConvertListToPython(System.Collections.IList list) {
            return this.ConvertListToPythonWithCircularCheck(list, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        private string ConvertListToPythonWithCircularCheck(System.Collections.IList list, HashSet<object> visitedObjects) {
            if (visitedObjects.Contains(list)) {
                throw new InvalidOperationException("Circular reference detected in collection. Cannot convert to Python.");
            }

            visitedObjects.Add(list);

            try {
                var items = new List<string>();
                foreach (var item in list) {
                    items.Add(this.ConvertToPythonValueWithCircularCheck(item, visitedObjects));
                }

                return $"[{string.Join(", ", items)}]";
            }
            finally {
                visitedObjects.Remove(list);
            }
        }

        /// <summary>
        /// Converts a dictionary to Python dict literal.
        /// </summary>
        /// <returns></returns>
        protected virtual string ConvertDictionaryToPython(System.Collections.IDictionary dict) {
            return this.ConvertDictionaryToPythonWithCircularCheck(dict, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        private string ConvertDictionaryToPythonWithCircularCheck(System.Collections.IDictionary dict, HashSet<object> visitedObjects) {
            if (visitedObjects.Contains(dict)) {
                throw new InvalidOperationException("Circular reference detected in dictionary. Cannot convert to Python.");
            }

            visitedObjects.Add(dict);

            try {
                var pairs = new List<string>();
                foreach (System.Collections.DictionaryEntry entry in dict) {
                    var key = this.ConvertToPythonValueWithCircularCheck(entry.Key, visitedObjects);
                    var value = this.ConvertToPythonValueWithCircularCheck(entry.Value, visitedObjects);
                    pairs.Add($"{key}: {value}");
                }

                return $"{{{string.Join(", ", pairs)}}}";
            }
            finally {
                visitedObjects.Remove(dict);
            }
        }

        /// <summary>
        /// Converts an enum to Python representation.
        /// </summary>
        /// <returns></returns>
        protected virtual string ConvertEnumToPython(Enum enumValue) {
            // Convert to underlying integer value
            var underlyingValue = Convert.ChangeType(enumValue, enumValue.GetTypeCode());
            return this.ConvertToPythonValue(underlyingValue);
        }

        /// <summary>
        /// Converts complex objects to Python representation.
        /// Uses JSON serialization as fallback for complex types.
        /// </summary>
        /// <returns></returns>
        protected virtual string ConvertComplexObjectToPython(object? value) {
            if (value == null) {
                return "None";
            }

            try {
                // Attempt to serialize to JSON and parse as Python dict
                var json = System.Text.Json.JsonSerializer.Serialize(value);

                // Convert JSON to Python dict syntax
                var pythonDict = json
                    .Replace("true", "True")
                    .Replace("false", "False")
                    .Replace("null", "None");

                return pythonDict;
            }
            catch (Exception ex) {
                this.Logger.LogWarning("Failed to convert complex object to Python: {Error}", ex.Message);
                throw new InvalidOperationException(
                    $"Cannot marshal type '{value.GetType().Name}' to Python. " +
                    $"Type must be JSON serializable or implement a custom converter. " +
                    $"Original error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Attempts to get embedded Python code from method attributes.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <param name="parameters">Method parameters for code templating.</param>
        /// <returns>Python code string or null if not found.</returns>
        protected virtual string? GetEmbeddedPythonCode(MethodInfo method, object?[]? parameters) {
            // Future enhancement: Check for PythonCodeAttribute with embedded code
            // var pythonAttr = method.GetCustomAttribute<PythonCodeAttribute>();
            // if (pythonAttr != null) {
            //     return ProcessPythonTemplate(pythonAttr.Code, parameters);
            // }
            return null;
        }

        /// <summary>
        /// Attempts to invoke the method to get Python code string.
        /// This supports the pattern where C# methods return Python code to execute.
        /// </summary>
        /// <param name="method">The method to invoke.</param>
        /// <param name="instance">The instance to invoke on.</param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>Python code string or null if method doesn't return string.</returns>
        protected virtual string? TryInvokeMethodForPythonCode(MethodInfo method, object? instance, object?[]? parameters) {
            try {
                // Only attempt if method returns string (likely Python code)
                if (method.ReturnType != typeof(string) && method.ReturnType != typeof(Task<string>)) {
                    return null;
                }

                // Skip if method has parameters we can't provide
                var methodParams = method.GetParameters();
                if (methodParams.Length > 0 && (parameters == null || parameters.Length != methodParams.Length)) {
                    return null;
                }

                // Attempt to invoke the method to get Python code
                var result = method.Invoke(instance, parameters);

                if (result is string pythonCode) {
                    return pythonCode;
                }

                if (result is Task<string> stringTask) {
                    // For async methods, we'd need to await, but this gets complex
                    // For now, skip async methods in this strategy
                    return null;
                }

                return result?.ToString();
            }
            catch (Exception ex) {
                this.Logger.LogDebug("Failed to invoke method '{MethodName}' for Python code: {Error}", method.Name, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Generates a device method call assuming the method is already deployed.
        /// This supports the pattern where methods represent deployed Python functions.
        /// NOTE: This strategy is disabled until method deployment infrastructure is implemented.
        /// </summary>
        /// <param name="method">The method to generate a call for.</param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>Python code string or null if method deployment not supported.</returns>
        protected virtual string? GenerateDeviceMethodCall(MethodInfo method, object?[]? parameters) {
            // TODO: Strategy 3 is temporarily disabled until method deployment infrastructure is implemented
            // Generating calls to non-existent functions would cause runtime failures
            //
            // When implemented, this should:
            // 1. Check if method is actually deployed to the device
            // 2. Generate appropriate function call only if deployment confirmed
            // 3. Handle deployment cache invalidation
            return null; // Disabled - would generate calls to non-existent functions
        }

        /// <summary>
        /// Checks if a method appears to be deployable to the device.
        /// This is a heuristic for determining if a method represents device-side code.
        /// </summary>
        /// <param name="method">The method to check.</param>
        /// <returns>True if the method might be deployable.</returns>
        protected virtual bool IsDeployableMethod(MethodInfo method) {
            // Heuristics for deployable methods:
            // 1. Method has a supported attribute
            // 2. Method name suggests device operation (starts with Get, Set, Read, Write, etc.)
            // 3. Method parameters are simple types
            // 4. Method doesn't use complex .NET features
            if (!method.HasAttribute<TaskAttribute>() &&
                !method.HasAttribute<SetupAttribute>() &&
                !method.HasAttribute<ThreadAttribute>() &&
                !method.HasAttribute<TeardownAttribute>()) {
                return false;
            }

            // Check if parameters are simple types that can be marshaled
            var parameters = method.GetParameters();
            foreach (var param in parameters) {
                if (!this.IsSimpleType(param.ParameterType)) {
                    return false;
                }
            }

            // Check if return type is deployable
            if (!this.IsSimpleType(method.ReturnType) && method.ReturnType != typeof(void) && method.ReturnType != typeof(Task)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a type is simple enough to be marshaled to Python.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type can be marshaled.</returns>
        protected virtual bool IsSimpleType(Type type) {
            // Handle nullable types
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                type = Nullable.GetUnderlyingType(type)!;
            }

            return type.IsPrimitive ||
                   type == typeof(string) ||
                   type == typeof(decimal) ||
                   type == typeof(DateTime) ||
                   type.IsEnum;
        }

        /// <summary>
        /// Executes Python code with session-based optimizations.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="session">The current device session.</param>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The result of the Python code execution.</returns>
        protected virtual async Task<T> ExecuteWithSessionOptimizationAsync<T>(IDeviceSession session, string pythonCode, CancellationToken cancellationToken) {
            try {
                // Check if device capabilities can inform execution optimization
                var capabilities = this.SessionManager.Capabilities;
                if (capabilities != null) {
                    // Optimize based on device performance tier
                    if (capabilities.PerformanceProfile.PerformanceTier == DevicePerformanceTier.Low) {
                        // For low-end devices, add small delay to prevent overwhelming
                        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                    }

                    // Log performance context
                    this.Logger.LogTrace(
                        "Executing on {DeviceType} (Tier: {Tier}, Features: {Features})",
                        capabilities.DeviceType,
                        capabilities.PerformanceProfile.PerformanceTier,
                        capabilities.SupportedFeatures);
                }

                // Execute the Python code
                return await this.Device.ExecuteAsync<T>(pythonCode, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (this.ErrorMapper != null) {
                var mappedException = this.ErrorMapper.MapException(ex, $"ExecuteWithSessionOptimization({typeof(T).Name})");
                mappedException.WithContext("python_code", pythonCode.Length > 200 ? $"{pythonCode[..200]}..." : pythonCode);
                mappedException.WithContext("session_id", session.SessionId);
                throw mappedException;
            }
        }

        /// <summary>
        /// Registers a session resource for automatic cleanup.
        /// </summary>
        /// <param name="resource">The resource to register.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected virtual async Task RegisterSessionResourceAsync(ISessionResource resource, CancellationToken cancellationToken = default) {
            if (resource == null) {
                throw new ArgumentNullException(nameof(resource));
            }

            var session = this.CurrentSession;
            if (session != null) {
                await session.Resources.RegisterResourceAsync(resource, cancellationToken).ConfigureAwait(false);
                this.Logger.LogDebug(
                    "Registered session resource {ResourceId} of type {ResourceType}",
                    resource.ResourceId, resource.ResourceType);
            }
            else
            {
                this.Logger.LogWarning(
                    "No current session available to register resource {ResourceId}",
                    resource.ResourceId);
            }
        }
    }
}
