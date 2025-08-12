// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;
using Belay.Core.Communication;
using Microsoft.Extensions.Logging;

namespace Belay.Core;

/// <summary>
/// Simplified device implementation using direct AttributeHandler and IDeviceConnection.
/// Replaces complex executor hierarchy with direct, documented interfaces per ICD-002.
/// </summary>
public class SimplifiedDevice : IDeviceConnection
{
    private readonly IDeviceCommunication communication;
    private readonly ILogger<SimplifiedDevice> logger;
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimplifiedDevice"/> class.
    /// </summary>
    /// <param name="communication">The device communication interface.</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public SimplifiedDevice(IDeviceCommunication communication, ILogger<SimplifiedDevice>? logger = null)
    {
        this.communication = communication ?? throw new ArgumentNullException(nameof(communication));
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SimplifiedDevice>.Instance;
    }

    /// <inheritdoc />
    public bool IsConnected => this.communication.State == DeviceConnectionState.Connected;

    /// <inheritdoc />
    public string DeviceInfo => "MicroPython Device"; // TODO: Implement actual device info detection

    /// <inheritdoc />
    public string ConnectionString => "Device Connection"; // TODO: Add connection string to IDeviceCommunication

    /// <inheritdoc />
    public async Task<T> ExecutePython<T>(string code, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        
        try
        {
            this.logger.LogDebug("Executing Python code: {Code}", code);
            
            var result = await this.communication.ExecuteAsync(code, cancellationToken);
            
            this.logger.LogDebug("Python execution completed. Output: {Output}", result);
            
            return ResultParser.ParseResult<T>(result);
        }
        catch (Exception ex) when (!(ex is DeviceException))
        {
            throw new DeviceException($"Failed to execute Python code: {ex.Message}", ex)
            {
                ExecutedCode = code,
                ConnectionString = this.ConnectionString
            };
        }
    }

    /// <inheritdoc />
    public async Task<string> ExecutePython(string code, CancellationToken cancellationToken = default)
    {
        return await this.ExecutePython<string>(code, cancellationToken);
    }

    /// <inheritdoc />
    public async Task WriteFile(string devicePath, byte[] data, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        
        try
        {
            this.logger.LogDebug("Writing file to device: {Path} ({Size} bytes)", devicePath, data.Length);
            
            // Use temporary file approach since IDeviceCommunication uses file paths
            var tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllBytesAsync(tempFile, data, cancellationToken);
                await this.communication.PutFileAsync(tempFile, devicePath, cancellationToken);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            
            this.logger.LogDebug("File write completed: {Path}", devicePath);
        }
        catch (Exception ex) when (!(ex is DeviceException))
        {
            throw new DeviceException($"Failed to write file '{devicePath}': {ex.Message}", ex)
            {
                ConnectionString = this.ConnectionString
            };
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> ReadFile(string devicePath, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        
        try
        {
            this.logger.LogDebug("Reading file from device: {Path}", devicePath);
            
            var data = await this.communication.GetFileAsync(devicePath, cancellationToken);
            
            this.logger.LogDebug("File read completed: {Path} ({Size} bytes)", devicePath, data.Length);
            
            return data;
        }
        catch (Exception ex) when (!(ex is DeviceException))
        {
            throw new DeviceException($"Failed to read file '{devicePath}': {ex.Message}", ex)
            {
                ConnectionString = this.ConnectionString
            };
        }
    }

    /// <inheritdoc />
    public async Task DeleteFile(string devicePath, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        
        try
        {
            this.logger.LogDebug("Deleting file from device: {Path}", devicePath);
            
            await this.ExecutePython($"import os; os.remove('{devicePath}')", cancellationToken);
            
            this.logger.LogDebug("File deleted: {Path}", devicePath);
        }
        catch (Exception ex) when (!(ex is DeviceException))
        {
            throw new DeviceException($"Failed to delete file '{devicePath}': {ex.Message}", ex)
            {
                ConnectionString = this.ConnectionString
            };
        }
    }

    /// <inheritdoc />
    public async Task<string[]> ListFiles(string devicePath = "/", CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        
        try
        {
            this.logger.LogDebug("Listing files in device directory: {Path}", devicePath);
            
            var result = await this.ExecutePython<string>($"import os; list(os.listdir('{devicePath}'))", cancellationToken);
            
            // Parse the Python list result into string array
            var files = ResultParser.ParseResult<string[]>(result);
            
            this.logger.LogDebug("Listed {Count} files in directory: {Path}", files.Length, devicePath);
            
            return files;
        }
        catch (Exception ex) when (!(ex is DeviceException))
        {
            throw new DeviceException($"Failed to list files in '{devicePath}': {ex.Message}", ex)
            {
                ConnectionString = this.ConnectionString
            };
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
    public async Task<T> ExecuteMethod<T>(MethodInfo method, object[] args, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        
        try
        {
            this.logger.LogDebug("Executing method: {Method}", method.Name);
            
            return await AttributeHandler.ExecuteMethod<T>(this, method, args, cancellationToken);
        }
        catch (Exception ex) when (!(ex is DeviceException))
        {
            throw new DeviceException($"Failed to execute method '{method.Name}': {ex.Message}", ex)
            {
                ConnectionString = this.ConnectionString
            };
        }
    }

    /// <summary>
    /// Executes a method without return value using AttributeHandler.
    /// </summary>
    /// <param name="method">The method to execute.</param>
    /// <param name="args">The method arguments.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task ExecuteMethod(MethodInfo method, object[] args, CancellationToken cancellationToken = default)
    {
        await this.ExecuteMethod<string>(method, args, cancellationToken);
    }

    /// <inheritdoc />
    public async Task Connect(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        
        this.logger.LogInformation("Connecting to device: {ConnectionString}", this.ConnectionString);
        
        // Wait for connected state since IDeviceCommunication doesn't have explicit Connect method
        // This is a limitation of the current interface design
        if (this.communication.State != DeviceConnectionState.Connected)
        {
            throw new DeviceException("Device communication is not in connected state");
        }
        
        this.logger.LogInformation("Connected to device successfully");
    }

    /// <inheritdoc />
    public async Task Disconnect()
    {
        await this.DisconnectAsync(CancellationToken.None);
    }

    /// <summary>
    /// Disconnects from the device with cancellation support.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (this.disposed)
            return;
            
        this.logger.LogInformation("Disconnecting from device");
        
        // Clear any cached results to prevent memory leaks
        SimpleCache.Clear();
        this.logger.LogDebug("Cleared device cache on disconnect");
        
        // IDeviceCommunication doesn't expose disconnect method
        // Disposal will handle cleanup
        await Task.CompletedTask;
        
        this.logger.LogInformation("Disconnected from device");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
            return;
            
        this.logger.LogDebug("Disposing SimplifiedDevice");
        
        try
        {
            this.Disconnect().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Error during device disconnection in Dispose");
        }
        
        this.communication?.Dispose();
        this.disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (this.disposed)
            throw new ObjectDisposedException(nameof(SimplifiedDevice));
    }
}