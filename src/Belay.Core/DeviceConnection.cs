// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Belay.Core;

/// <summary>
/// Unified device connection that handles both serial and subprocess communication.
/// Replaces the complex communication abstraction hierarchy with a single, direct implementation.
/// </summary>
public sealed class DeviceConnection : IDisposable
{
    private readonly ILogger<DeviceConnection> logger;
    
    /// <summary>
    /// Gets the type of connection being used.
    /// </summary>
    public ConnectionType Type { get; }
    
    /// <summary>
    /// Gets the connection string used to establish the connection.
    /// </summary>
    public string ConnectionString { get; }
    
    // Serial connection fields
    private SerialPort? serialPort;
    
    // Subprocess connection fields
    private Process? process;
    private StreamWriter? processInput;
    private StreamReader? processOutput;
    
    private bool disposed = false;

    /// <summary>
    /// Defines the type of connection to establish.
    /// </summary>
    public enum ConnectionType
    {
        /// <summary>Serial port connection.</summary>
        Serial,
        
        /// <summary>Subprocess connection.</summary>
        Subprocess
    }

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public DeviceConnectionState State { get; private set; } = DeviceConnectionState.Disconnected;

    /// <summary>
    /// Event raised when output is received from the device.
    /// </summary>
    public event EventHandler<DeviceOutputEventArgs>? OutputReceived;

    /// <summary>
    /// Event raised when device connection state changes.
    /// </summary>
    public event EventHandler<DeviceStateChangeEventArgs>? StateChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceConnection"/> class.
    /// </summary>
    /// <param name="type">The type of connection to establish.</param>
    /// <param name="connectionString">The connection string (port name for serial, command for subprocess).</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public DeviceConnection(ConnectionType type, string connectionString, ILogger<DeviceConnection>? logger = null)
    {
        this.Type = type;
        this.ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DeviceConnection>.Instance;
    }

    /// <summary>
    /// Connects to the device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (this.disposed)
            throw new ObjectDisposedException(nameof(DeviceConnection));

        this.SetState(DeviceConnectionState.Connecting);
        
        try
        {
            switch (this.Type)
            {
                case ConnectionType.Serial:
                    await this.ConnectSerialAsync(cancellationToken);
                    break;
                    
                case ConnectionType.Subprocess:
                    await this.ConnectSubprocessAsync(cancellationToken);
                    break;
                    
                default:
                    throw new ArgumentException($"Unsupported connection type: {this.Type}");
            }

            this.SetState(DeviceConnectionState.Connected);
            this.logger.LogInformation("Connected to device via {ConnectionType}: {ConnectionString}", 
                this.Type, this.ConnectionString);
        }
        catch (Exception ex)
        {
            this.SetState(DeviceConnectionState.Error);
            this.logger.LogError(ex, "Failed to connect to device");
            throw;
        }
    }

    /// <summary>
    /// Disconnects from the device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (this.disposed)
            return;

        try
        {
            switch (this.Type)
            {
                case ConnectionType.Serial:
                    this.serialPort?.Close();
                    this.serialPort?.Dispose();
                    this.serialPort = null;
                    break;
                    
                case ConnectionType.Subprocess:
                    this.processInput?.Close();
                    this.processOutput?.Close();
                    if (this.process != null && !this.process.HasExited)
                    {
                        this.process.Kill();
                        await this.process.WaitForExitAsync(cancellationToken);
                    }
                    this.process?.Dispose();
                    this.process = null;
                    this.processInput = null;
                    this.processOutput = null;
                    break;
            }

            this.SetState(DeviceConnectionState.Disconnected);
            this.logger.LogInformation("Disconnected from device");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during disconnect");
        }
    }

    /// <summary>
    /// Executes Python code on the device and returns the result.
    /// </summary>
    /// <param name="code">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result as a string.</returns>
    public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    {
        if (this.disposed)
            throw new ObjectDisposedException(nameof(DeviceConnection));

        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be null or empty", nameof(code));

        if (this.State != DeviceConnectionState.Connected)
            throw new InvalidOperationException("Device is not connected");

        this.SetState(DeviceConnectionState.Executing);
        
        try
        {
            this.logger.LogDebug("Executing code: {Code}", code.Trim());
            
            // Use raw REPL protocol for reliable execution
            string result = await this.ExecuteRawReplAsync(code, cancellationToken);
            
            this.logger.LogDebug("Execution completed, result length: {Length}", result.Length);
            return result;
        }
        finally
        {
            this.SetState(DeviceConnectionState.Connected);
        }
    }

    /// <summary>
    /// Executes Python code on the device and returns the result as a typed object.
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <param name="code">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result cast to the specified type.</returns>
    public async Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default)
    {
        string result = await this.ExecuteAsync(code, cancellationToken);
        
        if (string.IsNullOrWhiteSpace(result))
        {
            if (typeof(T) == typeof(string))
                return (T)(object)string.Empty;
                
            if (typeof(T).IsValueType)
                return default(T)!;
        }

        return ResultParser.ParseResult<T>(result);
    }

    /// <summary>
    /// Transfers a file from the local system to the device.
    /// </summary>
    /// <param name="localPath">Path to the local file.</param>
    /// <param name="remotePath">Path where the file should be stored on the device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PutFileAsync(string localPath, string remotePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(localPath))
            throw new FileNotFoundException($"Local file not found: {localPath}");

        byte[] fileContent = await File.ReadAllBytesAsync(localPath, cancellationToken);
        string base64Content = Convert.ToBase64String(fileContent);
        
        string code = $@"
import binascii
with open('{remotePath}', 'wb') as f:
    f.write(binascii.a2b_base64('{base64Content}'))
";
        
        await this.ExecuteAsync(code, cancellationToken);
        this.logger.LogDebug("File transferred: {LocalPath} -> {RemotePath}", localPath, remotePath);
    }

    /// <summary>
    /// Retrieves a file from the device to the local system.
    /// </summary>
    /// <param name="remotePath">Path to the file on the device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file contents as a byte array.</returns>
    public async Task<byte[]> GetFileAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        string code = $@"
import binascii
with open('{remotePath}', 'rb') as f:
    content = f.read()
    print(binascii.b2a_base64(content).decode().strip())
";
        
        string base64Content = await this.ExecuteAsync(code, cancellationToken);
        byte[] fileContent = Convert.FromBase64String(base64Content.Trim());
        
        this.logger.LogDebug("File retrieved: {RemotePath} ({Size} bytes)", remotePath, fileContent.Length);
        return fileContent;
    }

    private async Task ConnectSerialAsync(CancellationToken cancellationToken)
    {
        this.serialPort = new SerialPort(this.ConnectionString, 115200, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 5000,
            WriteTimeout = 5000,
            NewLine = "\r\n"
        };
        
        this.serialPort.Open();
        
        // Wait for device to be ready
        await Task.Delay(100, cancellationToken);
        
        // Send Ctrl-B to ensure we're in normal REPL mode
        this.serialPort.Write("\x02");
        await Task.Delay(100, cancellationToken);
    }

    private async Task ConnectSubprocessAsync(CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = this.ConnectionString,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        this.process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start subprocess");
        this.processInput = this.process.StandardInput;
        this.processOutput = this.process.StandardOutput;
        
        // Wait for process to be ready
        await Task.Delay(500, cancellationToken);
        
        // Send Ctrl-B to ensure we're in normal REPL mode
        await this.processInput.WriteAsync("\x02");
        await this.processInput.FlushAsync();
        await Task.Delay(100, cancellationToken);
    }

    /// <summary>
    /// Executes code using the raw REPL protocol.
    /// </summary>
    private async Task<string> ExecuteRawReplAsync(string code, CancellationToken cancellationToken)
    {
        // Enter raw mode: Ctrl-A
        await this.WriteAsync("\x01");
        
        // Wait for raw REPL prompt
        await Task.Delay(50, cancellationToken);
        
        // Send the code
        await this.WriteAsync(code);
        
        // Execute: Ctrl-D
        await this.WriteAsync("\x04");
        
        // Read the result
        string result = await this.ReadUntilPromptAsync(cancellationToken);
        
        // Exit raw mode: Ctrl-B
        await this.WriteAsync("\x02");
        
        return this.ParseRawReplResult(result);
    }

    private async Task WriteAsync(string data)
    {
        switch (this.Type)
        {
            case ConnectionType.Serial:
                this.serialPort!.Write(data);
                break;
                
            case ConnectionType.Subprocess:
                await this.processInput!.WriteAsync(data);
                await this.processInput.FlushAsync();
                break;
        }
    }

    private async Task<string> ReadUntilPromptAsync(CancellationToken cancellationToken)
    {
        var result = new StringBuilder();
        var buffer = new char[1024];
        
        while (!cancellationToken.IsCancellationRequested)
        {
            int bytesRead = 0;
            
            switch (this.Type)
            {
                case ConnectionType.Serial:
                    if (this.serialPort!.BytesToRead > 0)
                    {
                        string data = this.serialPort.ReadExisting();
                        result.Append(data);
                        
                        // Check for completion markers
                        if (data.Contains(">") || data.Contains(">>>"))
                            return result.ToString();
                    }
                    break;
                    
                case ConnectionType.Subprocess:
                    bytesRead = await this.processOutput!.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string data = new string(buffer, 0, bytesRead);
                        result.Append(data);
                        
                        // Check for completion markers
                        if (data.Contains(">") || data.Contains(">>>"))
                            return result.ToString();
                    }
                    break;
            }
            
            // Small delay to prevent busy waiting
            await Task.Delay(10, cancellationToken);
        }
        
        return result.ToString();
    }

    private string ParseRawReplResult(string rawResult)
    {
        // Simple parsing - extract the actual result between control characters
        // Remove common REPL artifacts
        string result = rawResult
            .Replace("\r", "")
            .Replace("raw REPL; CTRL-B to exit\n", "")
            .Replace(">>>", "")
            .Replace(">", "")
            .Trim();
            
        // Find the actual output (usually after the first newline)
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 1)
        {
            // Skip the first line (usually echo of command) and take the rest
            return string.Join("\n", lines.Skip(1)).Trim();
        }
        
        return result;
    }

    private void SetState(DeviceConnectionState newState)
    {
        var oldState = this.State;
        this.State = newState;
        
        this.StateChanged?.Invoke(this, new DeviceStateChangeEventArgs(oldState, newState));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
            return;

        try
        {
            this.DisconnectAsync(CancellationToken.None).Wait(1000);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error during dispose");
        }

        this.disposed = true;
    }
}