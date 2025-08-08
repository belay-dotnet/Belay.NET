// Copyright (c) 2024 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for more information.
    using System.Collections.Concurrent;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Belay.Core.Exceptions;
    using Belay.Core.Sessions;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Base class for all method executors that provides common functionality for applying policies around Device.ExecuteAsync calls.
    /// </summary>
    public abstract class BaseExecutor {
        /// <summary>
        /// Gets the device instance to execute Python code on.
        /// </summary>
        protected Device Device { get; }

        /// <summary>
        /// Gets the session manager for coordinating device sessions.
        /// </summary>
        protected IDeviceSessionManager SessionManager { get; }

        /// <summary>
        /// Gets logger for diagnostic information.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the error mapper for mapping exceptions.
        /// </summary>
        protected IErrorMapper? ErrorMapper { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseExecutor"/> class.
        /// </summary>
        /// <param name="device">The device to execute Python code on.</param>
        /// <param name="sessionManager">The session manager for device coordination.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="errorMapper">Optional error mapper for exception handling.</param>
        protected BaseExecutor(Device device, IDeviceSessionManager sessionManager, ILogger logger, IErrorMapper? errorMapper = null) {
            this.Device = device ?? throw new ArgumentNullException(nameof(device));
            this.SessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            this.Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.ErrorMapper = errorMapper;
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
            this.Logger.LogDebug("Executing Python code on device: {Code}", pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

            try {
                return await this.Device.ExecuteAsync<T>(pythonCode, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (this.ErrorMapper != null) {
                var mappedException = this.ErrorMapper.MapException(ex, $"ExecuteOnDevice({typeof(T).Name})");
                mappedException.WithContext("python_code", pythonCode.Length > 200 ? $"{pythonCode[..200]}..." : pythonCode);
                throw mappedException;
            }
        }

        /// <summary>
        /// Executes Python code directly on the device without returning a value.
        /// </summary>
        /// <param name="pythonCode">The Python code to execute.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        protected async Task ExecuteOnDeviceAsync(string pythonCode, CancellationToken cancellationToken = default) {
            this.Logger.LogDebug("Executing Python code on device: {Code}", pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

            try {
                await this.Device.ExecuteAsync(pythonCode, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (this.ErrorMapper != null) {
                var mappedException = this.ErrorMapper.MapException(ex, "ExecuteOnDevice");
                mappedException.WithContext("python_code", pythonCode.Length > 200 ? $"{pythonCode[..200]}..." : pythonCode);
                throw mappedException;
            }
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

            return await this.SessionManager.ExecuteInSessionAsync(operation, cancellationToken).ConfigureAwait(false);
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

            await this.SessionManager.ExecuteInSessionAsync(operation, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the calling method information from the stack frame.
        /// </summary>
        /// <param name="skipFrames">Number of frames to skip (default is 2 to skip this method and the caller).</param>
        /// <returns>The calling method information, or null if not available.</returns>
        protected MethodInfo? GetCallingMethod(int skipFrames = 2) {
            try {
                var stackTrace = new System.Diagnostics.StackTrace();
                if (stackTrace.FrameCount <= skipFrames) {
                    return null;
                }

                var frame = stackTrace.GetFrame(skipFrames);
                return frame?.GetMethod() as MethodInfo;
            }
            catch {
                // Stack trace inspection failed - return null
                return null;
            }
        }

        /// <summary>
        /// Generates a cache key for the given Python code and parameters.
        /// </summary>
        /// <param name="pythonCode">The Python code.</param>
        /// <param name="parameters">Optional parameters that affect the cache key.</param>
        /// <returns>A cache key string.</returns>
        protected static string GenerateCacheKey(string pythonCode, params object?[]? parameters) {
            var codeHash = pythonCode.GetHashCode().ToString("X8");

            if (parameters == null || parameters.Length == 0) {
                return codeHash;
            }

            var paramHash = string.Join("|", parameters.Select(p => p?.GetHashCode().ToString("X8") ?? "null"));
            return $"{codeHash}:{paramHash.GetHashCode():X8}";
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
        /// <returns>A combined cancellation token.</returns>
        protected static CancellationToken CombineCancellationTokens(CancellationToken cancellationToken, CancellationTokenSource? timeoutCts) {
            if (timeoutCts == null) {
                return cancellationToken;
            }

            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token).Token;
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
    }
}
