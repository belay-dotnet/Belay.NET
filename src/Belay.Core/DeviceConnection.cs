// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Belay.Core;

/// <summary>
/// Simplified device connection that handles both serial and subprocess communication.
/// Uses only basic Raw REPL protocol per aggressive simplification strategy.
/// </summary>
public sealed class DeviceConnection : IDisposable
{
    private readonly ILogger<DeviceConnection> logger;
    
    /// <summary>
    /// Connection types supported by this device connection.
    /// </summary>
    public enum ConnectionType
    {
        /// <summary>Serial port connection (USB, UART, etc.).</summary>
        Serial,
        
        /// <summary>Subprocess connection (local MicroPython process).</summary>
        Subprocess,
    }
    
    // Connection state
    public readonly ConnectionType Type;
    public readonly string ConnectionString;
    
    // Windows serial connection
    private SerialPort? serialPort;
    
    // Linux serial connection
    private LinuxSerialConnection? linuxSerial;
    
    // Subprocess connection
    private System.Diagnostics.Process? process;
    private StreamReader? processOutput;
    private StreamWriter? processInput;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceConnection"/> class.
    /// </summary>
    /// <param name="type">The connection type.</param>
    /// <param name="connectionString">The connection string (port name or executable path).</param>
    /// <param name="logger">The logger instance.</param>
    public DeviceConnection(ConnectionType type, string connectionString, ILogger<DeviceConnection> logger)
    {
        this.Type = type;
        this.ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Connects to the device.
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            switch (this.Type)
            {
                case ConnectionType.Serial:
                    await this.ConnectSerialAsync(cancellationToken).ConfigureAwait(false);
                    break;
                    
                case ConnectionType.Subprocess:
                    await this.ConnectSubprocessAsync(cancellationToken).ConfigureAwait(false);
                    break;
            }
            
            this.State = DeviceConnectionState.Connected;
            this.logger.LogInformation("Connected to device via {Type}: {Connection}", this.Type, this.ConnectionString);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to connect to device");
            throw;
        }
    }

    /// <summary>
    /// Disconnects from the device.
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            switch (this.Type)
            {
                case ConnectionType.Serial:
                    this.serialPort?.Close();
                    this.serialPort?.Dispose();
                    this.serialPort = null;
                    
                    this.linuxSerial?.Close();
                    this.linuxSerial?.Dispose();
                    this.linuxSerial = null;
                    break;
                    
                case ConnectionType.Subprocess:
                    this.processInput?.Close();
                    this.processOutput?.Close();
                    
                    if (this.process != null && !this.process.HasExited)
                    {
                        this.process.Kill();
                        await this.process.WaitForExitAsync().ConfigureAwait(false);
                    }
                    
                    this.process?.Dispose();
                    this.process = null;
                    this.processInput = null;
                    this.processOutput = null;
                    break;
            }
            
            this.State = DeviceConnectionState.Disconnected;
            this.logger.LogInformation("Disconnected from device");
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Error during disconnect");
        }
    }

    /// <summary>
    /// Executes Python code on the device using basic Raw REPL protocol.
    /// </summary>
    /// <param name="code">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    {
        try
        {
            this.logger.LogDebug("Executing code: {Code}", code);
            
            // Simple Raw REPL protocol - no adaptive logic
            return await this.ExecuteRawReplAsync(code, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Code execution failed");
            throw;
        }
    }

    private async Task ConnectSerialAsync(CancellationToken cancellationToken)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Use System.IO.Ports on Windows
            this.serialPort = new SerialPort(this.ConnectionString, 115200, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 5000,
                WriteTimeout = 5000,
                NewLine = "\r\n"
            };
            
            this.serialPort.Open();
            
            // Wait for device to be ready
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            
            // Send Ctrl-B to ensure we're in normal REPL mode
            this.serialPort.Write("\x02");
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Use Linux-compatible serial connection
            this.linuxSerial = new LinuxSerialConnection(this.ConnectionString);
            await this.linuxSerial.OpenAsync().ConfigureAwait(false);
            
            // Wait for device to be ready
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            
            // Send Ctrl-B to ensure we're in normal REPL mode
            await this.linuxSerial.WriteAsync("\x02").ConfigureAwait(false);
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ConnectSubprocessAsync(CancellationToken cancellationToken)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = this.ConnectionString,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        this.process = System.Diagnostics.Process.Start(startInfo);
        if (this.process == null)
            throw new InvalidOperationException("Failed to start subprocess");

        this.processInput = this.process.StandardInput;
        this.processOutput = this.process.StandardOutput;
        
        // Wait for subprocess to be ready
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ExecuteRawReplAsync(string code, CancellationToken cancellationToken)
    {
        // Enter raw mode
        await this.WriteAsync("\x01").ConfigureAwait(false);
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        
        // Send code
        await this.WriteAsync(code).ConfigureAwait(false);
        
        // Execute
        await this.WriteAsync("\x04").ConfigureAwait(false);
        
        // Read result with simple timeout
        var result = await this.ReadWithTimeoutAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        
        // Exit raw mode
        await this.WriteAsync("\x02").ConfigureAwait(false);
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        
        return result;
    }

    private async Task WriteAsync(string data)
    {
        switch (this.Type)
        {
            case ConnectionType.Serial:
                if (this.serialPort != null)
                {
                    this.serialPort.Write(data);
                }
                else if (this.linuxSerial != null)
                {
                    await this.linuxSerial.WriteAsync(data).ConfigureAwait(false);
                }
                break;
                
            case ConnectionType.Subprocess:
                if (this.processInput != null)
                {
                    await this.processInput.WriteAsync(data).ConfigureAwait(false);
                    await this.processInput.FlushAsync().ConfigureAwait(false);
                }
                break;
        }
    }

    private async Task<string> ReadWithTimeoutAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var result = new StringBuilder();
        var startTime = DateTime.UtcNow;
        
        while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow - startTime < timeout)
        {
            bool dataReceived = false;
            
            switch (this.Type)
            {
                case ConnectionType.Serial:
                    if (this.serialPort != null && this.serialPort.BytesToRead > 0)
                    {
                        string data = this.serialPort.ReadExisting();
                        result.Append(data);
                        dataReceived = true;
                    }
                    else if (this.linuxSerial != null)
                    {
                        string data = await this.linuxSerial.ReadExistingAsync().ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(data))
                        {
                            result.Append(data);
                            dataReceived = true;
                        }
                    }
                    break;
                    
                case ConnectionType.Subprocess:
                    try
                    {
                        if (this.processOutput!.Peek() >= 0)
                        {
                            char ch = (char)this.processOutput.Read();
                            result.Append(ch);
                            dataReceived = true;
                        }
                    }
                    catch
                    {
                        // End of stream or process ended
                        break;
                    }
                    break;
            }
            
            if (!dataReceived)
            {
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }
        
        return result.ToString();
    }

    // Simple state tracking
    public DeviceConnectionState State { get; private set; } = DeviceConnectionState.Disconnected;

    // Placeholder events (simplified - no complex state management)  
    public event EventHandler<DeviceOutputEventArgs>? OutputReceived;
    public event EventHandler<DeviceStateChangeEventArgs>? StateChanged;

    /// <summary>
    /// Executes code and returns typed result (simplified generic version).
    /// </summary>
    public async Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default)
    {
        var result = await this.ExecuteAsync(code, cancellationToken).ConfigureAwait(false);
        return (T)Convert.ChangeType(result.Trim(), typeof(T));
    }

    /// <summary>
    /// Simplified file upload - basic implementation.
    /// </summary>
    public async Task PutFileAsync(string localPath, string remotePath, CancellationToken cancellationToken = default)
    {
        var content = await File.ReadAllBytesAsync(localPath, cancellationToken).ConfigureAwait(false);
        var base64 = Convert.ToBase64String(content);
        
        var code = $@"
import binascii
with open('{remotePath}', 'wb') as f:
    f.write(binascii.a2b_base64('{base64}'))";
    
        await this.ExecuteAsync(code, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Simplified file download - basic implementation.
    /// </summary>
    public async Task<byte[]> GetFileAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        var code = $@"
import binascii
with open('{remotePath}', 'rb') as f:
    content = f.read()
    print(binascii.b2a_base64(content).decode().strip())";
    
        var base64Content = await this.ExecuteAsync(code, cancellationToken).ConfigureAwait(false);
        return Convert.FromBase64String(base64Content.Trim());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ = this.DisconnectAsync();
    }
}