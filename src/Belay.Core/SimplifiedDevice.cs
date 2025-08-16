// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

using System.Reflection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Simplified device implementation using direct AttributeHandler and IDeviceConnection.
/// Replaces complex executor hierarchy with direct, documented interfaces per ICD-002.
/// </summary>
public class SimplifiedDevice : IDeviceConnection {
    private readonly DeviceConnection connection;
    private readonly ILogger<SimplifiedDevice> logger;
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimplifiedDevice"/> class.
    /// </summary>
    /// <param name="connection">The device connection interface.</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public SimplifiedDevice(DeviceConnection connection, ILogger<SimplifiedDevice>? logger = null) {
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SimplifiedDevice>.Instance;
    }

    /// <inheritdoc />
    public bool IsConnected => connection.State == DeviceConnectionState.Connected;

    /// <inheritdoc />
    public string DeviceInfo => "MicroPython Device"; // TODO: Implement actual device info detection

    /// <inheritdoc />
    public string ConnectionString => connection.ConnectionString;

    /// <inheritdoc />
    public async Task<T> ExecutePython<T>(string code, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        try {
            logger.LogDebug("Executing Python code: {Code}", code);

            var result = await connection.ExecuteAsync(code, cancellationToken);

            logger.LogDebug("Python execution completed. Output: {Output}", result);

            return ResultParser.ParseResult<T>(result);
        }
        catch (Exception ex) when (!(ex is DeviceException)) {
            throw new DeviceException($"Failed to execute Python code: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<string> ExecutePython(string code, CancellationToken cancellationToken = default) {
        return await this.ExecutePython<string>(code, cancellationToken);
    }

    /// <inheritdoc />
    public async Task WriteFile(string devicePath, byte[] data, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        try {
            logger.LogDebug("Writing file to device: {Path} ({Size} bytes)", devicePath, data.Length);

            // Use temporary file approach since IDeviceCommunication uses file paths
            var tempFile = Path.GetTempFileName();
            try {
                await File.WriteAllBytesAsync(tempFile, data, cancellationToken);
                await connection.PutFileAsync(tempFile, devicePath, cancellationToken);
            }
            finally {
                if (File.Exists(tempFile)) {
                    File.Delete(tempFile);
                }
            }

            logger.LogDebug("File write completed: {Path}", devicePath);
        }
        catch (Exception ex) when (!(ex is DeviceException)) {
            throw new DeviceException($"Failed to write file '{devicePath}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> ReadFile(string devicePath, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        try {
            logger.LogDebug("Reading file from device: {Path}", devicePath);

            var data = await connection.GetFileAsync(devicePath, cancellationToken);

            logger.LogDebug("File read completed: {Path} ({Size} bytes)", devicePath, data.Length);

            return data;
        }
        catch (Exception ex) when (!(ex is DeviceException)) {
            throw new DeviceException($"Failed to read file '{devicePath}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteFile(string devicePath, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        try {
            logger.LogDebug("Deleting file from device: {Path}", devicePath);

            await this.ExecutePython($"import os; os.remove('{devicePath}')", cancellationToken);

            logger.LogDebug("File deleted: {Path}", devicePath);
        }
        catch (Exception ex) when (!(ex is DeviceException)) {
            throw new DeviceException($"Failed to delete file '{devicePath}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<string[]> ListFiles(string devicePath = "/", CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        try {
            logger.LogDebug("Listing files in device directory: {Path}", devicePath);

            var result = await this.ExecutePython<string>($"import os; list(os.listdir('{devicePath}'))", cancellationToken);

            // Parse the Python list result into string array
            var files = ResultParser.ParseResult<string[]>(result);

            logger.LogDebug("Listed {Count} files in directory: {Path}", files.Length, devicePath);

            return files;
        }
        catch (Exception ex) when (!(ex is DeviceException)) {
            throw new DeviceException($"Failed to list files in '{devicePath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a method with attribute-based behavior using AttributeHandler.
    /// </summary>
    /// <typeparam name="T">The return type of the method.</typeparam>
    /// <param name="method">The method to execute.</param>
    /// <param name="args">The method arguments.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The result of the method execution.</returns>
    public async Task<T> ExecuteMethod<T>(MethodInfo method, object[] args, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        try {
            logger.LogDebug("Executing method: {Method}", method.Name);

            return await AttributeHandler.ExecuteMethod<T>(this, method, args, cancellationToken);
        }
        catch (Exception ex) when (!(ex is DeviceException)) {
            throw new DeviceException($"Failed to execute method '{method.Name}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a method without return value using AttributeHandler.
    /// </summary>
    /// <param name="method">The method to execute.</param>
    /// <param name="args">The method arguments.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task ExecuteMethod(MethodInfo method, object[] args, CancellationToken cancellationToken = default) {
        await this.ExecuteMethod<string>(method, args, cancellationToken);
    }

    /// <inheritdoc />
    public async Task Connect(CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        logger.LogInformation("Connecting to device: {ConnectionString}", this.ConnectionString);

        // Use the DeviceConnection's connect method
        await connection.ConnectAsync(cancellationToken);

        logger.LogInformation("Connected to device successfully");
    }

    /// <inheritdoc />
    public async Task Disconnect() {
        await this.DisconnectAsync(CancellationToken.None);
    }

    /// <summary>
    /// Disconnects from the device with cancellation support.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default) {
        if (disposed) {
            return;
        }

        logger.LogInformation("Disconnecting from device");

        // Clear any cached results to prevent memory leaks
        SimpleCache.Clear();
        logger.LogDebug("Cleared device cache on disconnect");

        // Use the DeviceConnection's disconnect method
        await connection.DisconnectAsync(cancellationToken);

        logger.LogInformation("Disconnected from device");
    }

    /// <inheritdoc />
    public void Dispose() {
        if (disposed) {
            return;
        }

        logger.LogDebug("Disposing SimplifiedDevice");

        try {
            this.Disconnect().GetAwaiter().GetResult();
        }
        catch (Exception ex) {
            logger.LogWarning(ex, "Error during device disconnection in Dispose");
        }

        connection?.Dispose();
        disposed = true;
    }

    private void ThrowIfDisposed() {
        if (disposed) {
            throw new ObjectDisposedException(nameof(SimplifiedDevice));
        }
    }
}
