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
    
    // Serial connection (works on both Windows and Linux in .NET 8)
    private SerialPort? serialPort;
    
    // Sophisticated Raw REPL protocol
    private AdaptiveRawReplProtocol? adaptiveProtocol;
    
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
                    this.adaptiveProtocol?.Dispose();
                    this.adaptiveProtocol = null;
                    
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
    /// Executes Python code on the device using sophisticated adaptive Raw REPL protocol.
    /// </summary>
    /// <param name="code">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    {
        try
        {
            this.logger.LogDebug("Executing code: {Code}", code);
            
            // Use sophisticated adaptive protocol for all device types
            if (this.adaptiveProtocol != null)
            {
                var response = await this.adaptiveProtocol.ExecuteCodeAsync(code, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccess)
                {
                    return response.Result ?? string.Empty;
                }
                else
                {
                    throw new DeviceException($"Device execution failed: {response.ErrorOutput}", response.Exception);
                }
            }
            else
            {
                // Fallback to basic Raw REPL for subprocess connections
                return await this.ExecuteRawReplAsync(code, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Code execution failed");
            throw;
        }
    }

    private async Task ConnectSerialAsync(CancellationToken cancellationToken)
    {
        // Use System.IO.Ports.SerialPort directly - it works on both Windows and Linux in .NET 8
        this.serialPort = new SerialPort(this.ConnectionString, 115200, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 5000,
            WriteTimeout = 5000,
            NewLine = "\r\n"
        };
        
        this.serialPort.Open();
        
        // Wait for device to be ready (using working approach from previous tag)
        await this.WaitForDeviceReadyAsync(this.serialPort.BaseStream, cancellationToken).ConfigureAwait(false);
        
        // Initialize sophisticated adaptive protocol (user requested: "restore all the sophisticated functionality")
        var config = new RawReplConfiguration { EnableVerboseLogging = false };
        var protocolLogger = this.logger as ILogger<AdaptiveRawReplProtocol> ?? 
                             Microsoft.Extensions.Logging.Abstractions.NullLogger<AdaptiveRawReplProtocol>.Instance;
        this.adaptiveProtocol = new AdaptiveRawReplProtocol(
            this.serialPort.BaseStream,
            protocolLogger,
            config);
        await this.adaptiveProtocol.InitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForDeviceReadyAsync(Stream stream, CancellationToken cancellationToken)
    {
        this.logger.LogDebug("Waiting for device to be ready");

        // CRITICAL: Device recovery sequence per user feedback
        // "Note that after using a device it may be left in raw repl mode, which can appear like it's locked up"
        await this.RecoverFromStuckModesAsync(stream, cancellationToken);

        this.logger.LogDebug("Device ready");
    }

    private async Task RecoverFromStuckModesAsync(Stream stream, CancellationToken cancellationToken)
    {
        this.logger.LogDebug("Attempting device recovery from stuck raw/paste modes");

        try
        {
            // 1. Exit raw-paste mode if stuck (Ctrl-C, Ctrl-D)
            await this.SendControlCharacterAsync(stream, 0x03, cancellationToken); // Ctrl-C
            await this.SendControlCharacterAsync(stream, 0x04, cancellationToken); // Ctrl-D
            await stream.FlushAsync(cancellationToken);
            await Task.Delay(100, cancellationToken);

            // 2. Exit raw mode if stuck (Ctrl-B)
            await this.SendControlCharacterAsync(stream, 0x02, cancellationToken); // Ctrl-B
            await stream.FlushAsync(cancellationToken);
            await Task.Delay(100, cancellationToken);

            // 3. Send additional interrupt to ensure clean state
            await this.SendControlCharacterAsync(stream, 0x03, cancellationToken); // Ctrl-C
            await stream.FlushAsync(cancellationToken);
            await Task.Delay(100, cancellationToken);

            // 4. Send newline to trigger prompt response
            await stream.WriteAsync(new byte[] { 0x0D, 0x0A }, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            // 5. Drain any pending output from recovery attempts with shorter timeout
            await this.DrainAvailableOutputAsync(stream, cancellationToken);

            this.logger.LogDebug("Device recovery completed successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Device recovery failed, continuing with normal initialization");
        }
    }

    private async Task DrainAvailableOutputAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Drain any pending output with short timeout
        var buffer = new byte[1024];
        using var drainCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, drainCts.Token);

        try
        {
            while (!combinedCts.Token.IsCancellationRequested)
            {
                var bytesRead = await stream.ReadAsync(buffer, combinedCts.Token);
                if (bytesRead == 0)
                    break;
                
                // Just discard the output - we're cleaning up
                this.logger.LogDebug("Drained {BytesRead} bytes during recovery", bytesRead);
            }
        }
        catch (OperationCanceledException) when (drainCts.Token.IsCancellationRequested)
        {
            // Normal timeout - drain complete
        }
    }

    private async Task SendControlCharacterAsync(Stream stream, byte controlChar, CancellationToken cancellationToken)
    {
        byte[] buffer = [controlChar];
        await stream.WriteAsync(buffer, cancellationToken);
        await stream.FlushAsync(cancellationToken);
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
        // Use the working Raw REPL pattern from previous tag
        try
        {
            if (this.serialPort == null)
                throw new DeviceException("Serial port not connected");

            // Enter raw mode with Ctrl-A
            await this.serialPort.BaseStream.WriteAsync(new byte[] { 0x01 }, cancellationToken);
            await this.WaitForPromptAsync(this.serialPort.BaseStream, ">", cancellationToken);

            // Send code
            var codeBytes = System.Text.Encoding.UTF8.GetBytes(code);
            await this.serialPort.BaseStream.WriteAsync(codeBytes, cancellationToken);

            // Execute with Ctrl-D
            await this.serialPort.BaseStream.WriteAsync(new byte[] { 0x04 }, cancellationToken);

            // Read result until prompt
            var result = await this.ReadUntilPromptAsync(this.serialPort.BaseStream, cancellationToken);

            // Exit raw mode with Ctrl-B
            await this.serialPort.BaseStream.WriteAsync(new byte[] { 0x02 }, cancellationToken);

            return result;
        }
        catch (Exception ex) when (!(ex is DeviceException))
        {
            throw new DeviceException($"Raw REPL execution failed: {ex.Message}", ex)
            {
                ExecutedCode = code
            };
        }
    }

    private async Task WaitForPromptAsync(Stream stream, string expectedPrompt, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var received = new System.Text.StringBuilder();
        
        // Add timeout to prevent hanging on stream reads
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        while (!combinedCts.Token.IsCancellationRequested)
        {
            try
            {
                var bytesRead = await stream.ReadAsync(buffer, combinedCts.Token);
                if (bytesRead == 0)
                    throw new DeviceException("Device disconnected while waiting for prompt");

                var text = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                received.Append(text);
                
                if (received.ToString().Contains(expectedPrompt))
                    return;
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
            {
                throw new DeviceException($"Timeout waiting for prompt '{expectedPrompt}'. Received: '{received}'");
            }
        }
        
        throw new OperationCanceledException("Timeout waiting for device prompt");
    }

    private async Task<string> ReadUntilPromptAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var result = new System.Text.StringBuilder();
        
        // Add timeout to prevent hanging on stream reads
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        
        try
        {
            while (!combinedCts.Token.IsCancellationRequested)
            {
                var bytesRead = await stream.ReadAsync(buffer, combinedCts.Token);
                if (bytesRead == 0)
                    break;

                var text = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                result.Append(text);
                
                // Check for prompt indicating end of output - look for the complete sequence
                if (text.Contains("\x04>"))
                    break;
            }
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            throw new DeviceException($"Timeout reading device output. Received so far: '{result}'");
        }
        
        var output = result.ToString();
        
        // Clean up the output using corrected parsing logic from working implementation
        // Actual format is: OK<result>\r\n\x04\x04>
        if (output.StartsWith("OK"))
        {
            output = output[2..]; // Remove "OK" prefix
        }
        
        // Remove the trailing \r\n\x04\x04> sequence  
        var endIndex = output.IndexOf("\r\n\x04\x04>");
        if (endIndex >= 0)
        {
            output = output[..endIndex];
        }
        else
        {
            // Fallback for different formats
            endIndex = output.LastIndexOf("\x04\x04>");
            if (endIndex >= 0)
            {
                output = output[..endIndex];
            }
        }
        
        return output.Trim();
    }
    
    private string ParseRawReplResponse(string output)
    {
        if (output.Contains("Traceback") || output.Contains("Error") || output.Contains("Exception"))
        {
            throw new DeviceException($"Device execution error: {output}");
        }

        string result = output;

        // Remove "OK" prefix if present
        if (result.StartsWith("OK"))
        {
            result = result.Substring(2);
        }

        // The format is: OK<result>\x04\x04>
        // Find the first \x04 character (start of end sequence) 
        int firstControlCharIndex = result.IndexOf('\x04');
        if (firstControlCharIndex >= 0)
        {
            result = result.Substring(0, firstControlCharIndex);
        }
        else if (result.EndsWith('>'))
        {
            // Fallback: remove trailing '>' if no control chars found
            result = result.Substring(0, result.Length - 1);
        }

        // Trim whitespace and control characters
        result = result.Trim('\r', '\n', ' ', '\t');
        
        // Handle empty results (like from print statements)
        if (string.IsNullOrEmpty(result))
        {
            return string.Empty;
        }
        
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
        var lastDataTime = startTime;
        var dataReceiveTimeout = TimeSpan.FromSeconds(1); // Stop waiting if no data for 1 second
        int readAttempts = 0;
        
        while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow - startTime < timeout)
        {
            readAttempts++;
            bool dataReceived = false;
            
            switch (this.Type)
            {
                case ConnectionType.Serial:
                    if (this.serialPort != null && this.serialPort.BytesToRead > 0)
                    {
                        string data = this.serialPort.ReadExisting();
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
            
            if (dataReceived)
            {
                lastDataTime = DateTime.UtcNow;
                
                // Check for completion markers like the working version
                string partial = result.ToString();
                
                // Check for Raw REPL entry completion
                if (partial.Contains("raw REPL") && (partial.Contains("CTRL-B") || partial.Contains(">")))
                {
                    break; // Complete Raw REPL entry message received
                }
                
                // Check for execution output completion - ESP32C6 sends OK<result>\x04\x04>
                if (partial.Contains("OK") && partial.Contains("\x04") && partial.Contains(">"))
                {
                    break; // Complete execution output received
                }
            }
            else
            {
                // Check if we've been waiting too long since last data
                if (result.Length > 0 && DateTime.UtcNow - lastDataTime > dataReceiveTimeout)
                {
                    // We have some data and haven't received more for a while, return what we have
                    break;
                }
                
                await Task.Delay(20, cancellationToken).ConfigureAwait(false); // Shorter delay between reads
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