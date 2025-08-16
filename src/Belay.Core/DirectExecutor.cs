// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

using System.Reflection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Direct executor that handles all attribute types via AttributeHandler.
/// Replaces the complex executor hierarchy with a single, focused implementation.
/// </summary>
public sealed class DirectExecutor : IDisposable {
    private readonly IDeviceConnection device;
    private readonly ILogger<DirectExecutor> logger;
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectExecutor"/> class.
    /// </summary>
    /// <param name="device">The device connection to execute on.</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public DirectExecutor(IDeviceConnection device, ILogger<DirectExecutor>? logger = null) {
        device = device ?? throw new ArgumentNullException(nameof(device));
        logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DirectExecutor>.Instance;
    }

    /// <summary>
    /// Executes a method with return value using AttributeHandler.
    /// </summary>
    /// <typeparam name="T">The return type of the method.</typeparam>
    /// <param name="method">The method to execute.</param>
    /// <param name="args">The method arguments.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of the method execution.</returns>
    public async Task<T> ExecuteAsync<T>(MethodInfo method, object[] args, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        logger.LogDebug("Executing method: {Method} with {ArgCount} arguments", method.Name, args.Length);

        try {
            return await AttributeHandler.ExecuteMethod<T>(device, method, args, cancellationToken);
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to execute method: {Method}", method.Name);
            throw;
        }
    }

    /// <summary>
    /// Executes a method without return value using AttributeHandler.
    /// </summary>
    /// <param name="method">The method to execute.</param>
    /// <param name="args">The method arguments.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task ExecuteAsync(MethodInfo method, object[] args, CancellationToken cancellationToken = default) {
        await ExecuteAsync<string>(method, args, cancellationToken);
    }

    /// <summary>
    /// Executes Python code directly on the device.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="pythonCode">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of the Python code execution.</returns>
    public async Task<T> ExecutePythonAsync<T>(string pythonCode, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        logger.LogDebug("Executing Python code directly: {Code}", pythonCode);

        return await device.ExecutePython<T>(pythonCode, cancellationToken);
    }

    /// <summary>
    /// Executes Python code directly on the device without return value.
    /// </summary>
    /// <param name="pythonCode">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task ExecutePythonAsync(string pythonCode, CancellationToken cancellationToken = default) {
        await ExecutePythonAsync<string>(pythonCode, cancellationToken);
    }

    /// <summary>
    /// Clears any cached execution state.
    /// </summary>
    public void ClearCache() {
        SimpleCache.Clear();
        logger.LogDebug("Cleared execution cache");
    }

    /// <inheritdoc />
    public void Dispose() {
        if (disposed) {
            return;
        }

        logger.LogDebug("Disposing DirectExecutor");
        disposed = true;
    }

    private void ThrowIfDisposed() {
        if (disposed) {
            throw new ObjectDisposedException(nameof(DirectExecutor));
        }
    }
}
