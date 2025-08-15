// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// Unified device connection that handles both serial and subprocess communication.
/// Uses sophisticated adaptive Raw REPL protocol with graceful degradation to basic mode.
/// Implements simplified architecture principles while maintaining advanced protocol capabilities.
/// </summary>
public sealed class DeviceConnection : IDisposable {
    private readonly ILogger<DeviceConnection> logger;

    /// <summary>
    /// Connection types supported by this device connection.
    /// </summary>
    public enum ConnectionType {
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

    // Simple Raw REPL protocol
    private SimpleRawRepl? simpleRawRepl;

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
    public DeviceConnection(ConnectionType type, string connectionString, ILogger<DeviceConnection> logger) {
        this.Type = type;
        this.ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Connects to the device.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task ConnectAsync(CancellationToken cancellationToken = default) {
        try {
            switch (this.Type) {
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
        catch (Exception ex) {
            this.logger.LogError(ex, "Failed to connect to device");
            throw;
        }
    }

    /// <summary>
    /// Disconnects from the device.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default) {
        try {
            switch (this.Type) {
                case ConnectionType.Serial:
                    this.simpleRawRepl?.Dispose();
                    this.simpleRawRepl = null;

                    this.serialPort?.Close();
                    this.serialPort?.Dispose();
                    this.serialPort = null;

                    break;

                case ConnectionType.Subprocess:
                    this.processInput?.Close();
                    this.processOutput?.Close();

                    if (this.process != null && !this.process.HasExited) {
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
        catch (Exception ex) {
            this.logger.LogWarning(ex, "Error during disconnect");
        }
    }

    /// <summary>
    /// Executes Python code on the device using sophisticated adaptive Raw REPL protocol.
    /// </summary>
    /// <param name="code">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default) {
        try {
            this.logger.LogDebug("Executing code: {Code}", code);

            // Use simplified raw REPL protocol for all device types
            if (this.simpleRawRepl != null) {
                var response = await this.simpleRawRepl.ExecuteAsync(code, 10, cancellationToken);
                if (response.IsSuccess) {
                    return response.Output;
                }
                else {
                    throw new DeviceException($"Device execution failed: {response.ErrorOutput}", response.Exception);
                }
            }
            else {
                // This should never happen since simpleRawRepl is always initialized
                throw new InvalidOperationException("Simple raw REPL protocol not initialized");
            }
        }
        catch (Exception ex) {
            this.logger.LogError(ex, "Code execution failed");
            throw;
        }
    }

    private async Task ConnectSerialAsync(CancellationToken cancellationToken) {
        // Use System.IO.Ports.SerialPort directly - it works on both Windows and Linux in .NET 8
        this.serialPort = new SerialPort(this.ConnectionString, 115200, Parity.None, 8, StopBits.One) {
            ReadTimeout = 5000,
            WriteTimeout = 5000,
            NewLine = "\r\n",
        };

        this.serialPort.Open();

        // Recover from any stuck modes before initialization
        await this.RecoverFromStuckModesAsync(this.serialPort.BaseStream, cancellationToken).ConfigureAwait(false);

        // Wait for device to be ready (using working approach from previous tag)
        await this.WaitForDeviceReadyAsync(this.serialPort.BaseStream, cancellationToken).ConfigureAwait(false);

        // Initialize Simple Raw REPL protocol (skip soft reset during initial connection)
        this.simpleRawRepl = new SimpleRawRepl(this.serialPort.BaseStream, this.logger);
        await this.simpleRawRepl.EnterRawReplAsync(false, 10, cancellationToken);
    }

    private async Task WaitForDeviceReadyAsync(Stream stream, CancellationToken cancellationToken) {
        this.logger.LogDebug("Waiting for device to be ready");

        // Simplified device initialization - just send interrupt and brief wait
        await this.SendControlCharacterAsync(stream, 0x03, cancellationToken); // Ctrl-C
        await stream.FlushAsync(cancellationToken);
        await Task.Delay(200, cancellationToken);

        this.logger.LogDebug("Device ready");
    }

    private async Task RecoverFromStuckModesAsync(Stream stream, CancellationToken cancellationToken) {
        this.logger.LogDebug("Attempting device recovery from stuck raw/paste modes");

        try {
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
        catch (Exception ex) {
            this.logger.LogWarning(ex, "Device recovery failed, continuing with normal initialization");
        }
    }

    private async Task DrainAvailableOutputAsync(Stream stream, CancellationToken cancellationToken) {
        // Drain any pending output with short timeout
        var buffer = new byte[1024];
        using var drainCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, drainCts.Token);

        try {
            while (!combinedCts.Token.IsCancellationRequested) {
                var bytesRead = await stream.ReadAsync(buffer, combinedCts.Token);
                if (bytesRead == 0) {
                    break;
                }

                // Just discard the output - we're cleaning up
                this.logger.LogDebug("Drained {BytesRead} bytes during recovery", bytesRead);
            }
        }
        catch (OperationCanceledException) when (drainCts.Token.IsCancellationRequested) {
            // Normal timeout - drain complete
        }
    }

    private async Task DrainInitialOutputAsync(Stream stream, CancellationToken cancellationToken) {
        // Drain initial banner output from unix port subprocess
        var buffer = new byte[1024];
        using var drainCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, drainCts.Token);

        try {
            var totalDrained = 0;
            while (!combinedCts.Token.IsCancellationRequested) {
                var bytesRead = await stream.ReadAsync(buffer, combinedCts.Token);
                if (bytesRead == 0) {
                    break;
                }

                totalDrained += bytesRead;
                var output = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                this.logger.LogDebug("Drained initial output: {Output}", output.Replace("\r", "\\r").Replace("\n", "\\n"));

                // Stop draining if we see the friendly REPL prompt
                if (output.Contains(">>>")) {
                    break;
                }
            }

            this.logger.LogDebug("Drained {TotalBytes} bytes of initial output", totalDrained);

            if (totalDrained == 0) {
                this.logger.LogWarning("No initial output received from subprocess - this may indicate a communication issue");
            }
        }
        catch (OperationCanceledException) when (drainCts.Token.IsCancellationRequested) {
            // Normal timeout - drain complete
        }
    }

    private async Task SendControlCharacterAsync(Stream stream, byte controlChar, CancellationToken cancellationToken) {
        byte[] buffer =[controlChar];
        await stream.WriteAsync(buffer, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private async Task ConnectSubprocessAsync(CancellationToken cancellationToken) {
        var startInfo = new System.Diagnostics.ProcessStartInfo {
            FileName = this.ConnectionString,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,

            // Critical: Disable output buffering for subprocess communication
            Environment = { ["PYTHONUNBUFFERED"] = "1" },
        };

        this.process = System.Diagnostics.Process.Start(startInfo);
        if (this.process == null) {
            throw new InvalidOperationException("Failed to start subprocess");
        }

        this.processInput = this.process.StandardInput;
        this.processOutput = this.process.StandardOutput;

        // Configure streams for raw communication
        this.processInput.AutoFlush = true;

        // Wait longer for subprocess to be ready and show banner
        await Task.Delay(500, cancellationToken).ConfigureAwait(false);

        // Create bidirectional stream for subprocess communication
        var bidirectionalStream = new BidirectionalProcessStream(
            this.process.StandardInput.BaseStream,
            this.process.StandardOutput.BaseStream);

        // Initialize Simple Raw REPL protocol with subprocess-specific handling
        this.simpleRawRepl = new SimpleRawRepl(bidirectionalStream, this.logger);

        // For unix port, we may need to drain the initial banner first
        this.logger.LogDebug("Draining initial output from subprocess...");
        await this.DrainInitialOutputAsync(bidirectionalStream, cancellationToken);

        await this.simpleRawRepl.EnterRawReplAsync(true, 10, cancellationToken);
    }

    // Simple state tracking
    public DeviceConnectionState State { get; private set; } = DeviceConnectionState.Disconnected;

    // Placeholder events (simplified - no complex state management)
    public event EventHandler<DeviceOutputEventArgs>? OutputReceived;

    public event EventHandler<DeviceStateChangeEventArgs>? StateChanged;

    /// <summary>
    /// Executes code and returns typed result (simplified generic version).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default) {
        var result = await this.ExecuteAsync(code, cancellationToken).ConfigureAwait(false);
        return (T)Convert.ChangeType(result.Trim(), typeof(T));
    }

    /// <summary>
    /// Writes a file to the device using efficient adaptive chunked transfer.
    /// Automatically optimizes chunk size based on device communication performance.
    /// Based on official mpremote fs_writefile implementation with base64 encoding.
    /// </summary>
    /// <param name="localPath">Local file path.</param>
    /// <param name="remotePath">Remote file path on device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task PutFileAsync(string localPath, string remotePath, CancellationToken cancellationToken = default) {
        var fileData = await File.ReadAllBytesAsync(localPath, cancellationToken);
        await this.WriteFileAsync(remotePath, fileData, cancellationToken);
    }

    /// <summary>
    /// Writes data to a file on the device using adaptive chunked transfer.
    /// Automatically optimizes chunk size based on device communication performance.
    /// Based on official mpremote fs_writefile implementation with base64 encoding.
    /// </summary>
    /// <param name="remotePath">Remote file path on device.</param>
    /// <param name="data">Data to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when remotePath is null, empty, or contains invalid characters.</exception>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    /// <exception cref="DeviceException">Thrown when device communication fails or file operation fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task WriteFileAsync(string remotePath, byte[] data, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(remotePath)) {
            throw new ArgumentException("Remote path cannot be null or empty", nameof(remotePath));
        }

        if (data == null) {
            throw new ArgumentNullException(nameof(data));
        }

        var escapedPath = EscapePythonString(remotePath);
        this.logger.LogDebug("Writing file {Path} ({Size} bytes)", remotePath, data.Length);

        // Adaptive chunk sizing for optimized performance (internal implementation)
        const int DefaultInitialChunkSize = 256;
        var chunkOptimizer = new AdaptiveChunkOptimizer(DefaultInitialChunkSize, this.logger);

        bool fileOpened = false;
        try {
            // Open file and get write function - following official mpremote pattern
            await this.ExecuteAsync($"f=open('{escapedPath}','wb')\\nw=f.write", cancellationToken);
            fileOpened = true;

            // Write data in chunks using adaptive sizing and base64 encoding
            int totalTransferred = 0;
            while (totalTransferred < data.Length) {
                cancellationToken.ThrowIfCancellationRequested();

                var currentChunkSize = chunkOptimizer.GetOptimalChunkSize();
                var chunk = data[totalTransferred..Math.Min(totalTransferred + currentChunkSize, data.Length)];
                var chunkBase64 = Convert.ToBase64String(chunk);

                // Measure transfer performance
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Use base64 encoding which is more efficient than hex (33% vs 100% overhead)
                var pythonBytes = $"__import__('binascii').a2b_base64('{chunkBase64}')";
                await this.ExecuteAsync($"w({pythonBytes})", cancellationToken);

                stopwatch.Stop();

                // Update optimizer with performance metrics
                chunkOptimizer.RecordTransfer(chunk.Length, stopwatch.Elapsed);
                totalTransferred += chunk.Length;

                this.logger.LogTrace(
                    "Transferred chunk: {ChunkSize} bytes in {Duration}ms, total: {Total}/{Size}",
                    chunk.Length, stopwatch.ElapsedMilliseconds, totalTransferred, data.Length);
            }

            this.logger.LogDebug("Successfully wrote file {Path}", remotePath);
        }
        catch (Exception ex) {
            this.logger.LogError(ex, "Failed to write file {Path}", remotePath);
            throw new DeviceException($"Failed to write file '{remotePath}': {ex.Message}", ex);
        }
        finally {
            // Ensure file is closed even if an error occurs
            if (fileOpened) {
                try {
                    // Use timeout for cleanup to prevent hanging
                    using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await this.ExecuteAsync("try:\n    f.close()\nexcept:\n    pass", cleanupCts.Token);
                }
                catch (Exception ex) {
                    this.logger.LogWarning(ex, "Failed to close file handle during cleanup");
                }
            }
        }
    }

    /// <summary>
    /// Reads a file from the device using adaptive chunked transfer.
    /// Automatically optimizes chunk size based on device communication performance.
    /// Based on official mpremote fs_readfile implementation with base64 encoding.
    /// </summary>
    /// <param name="remotePath">Remote file path on device.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file contents.</returns>
    /// <exception cref="ArgumentException">Thrown when remotePath is null, empty, or contains invalid characters.</exception>
    /// <exception cref="DeviceException">Thrown when device communication fails or file operation fails.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled.</exception>
    public async Task<byte[]> GetFileAsync(string remotePath, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(remotePath)) {
            throw new ArgumentException("Remote path cannot be null or empty", nameof(remotePath));
        }

        var escapedPath = EscapePythonString(remotePath);
        this.logger.LogDebug("Reading file {Path}", remotePath);

        // Adaptive chunk sizing for optimized performance (internal implementation)
        const int DefaultInitialChunkSize = 256;
        var chunkOptimizer = new AdaptiveChunkOptimizer(DefaultInitialChunkSize, this.logger);

        bool fileOpened = false;
        try {
            // Use MemoryStream for efficient memory management
            using var contents = new MemoryStream();

            // Open file and get read function - following official mpremote pattern
            await this.ExecuteAsync($"f=open('{escapedPath}','rb')\\nr=f.read", cancellationToken);
            fileOpened = true;

            while (true) {
                cancellationToken.ThrowIfCancellationRequested();

                var currentChunkSize = chunkOptimizer.GetOptimalChunkSize();

                // Measure transfer performance
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Read chunk and encode as base64 for efficient transfer
                var chunkResult = await this.ExecuteAsync($"data=r({currentChunkSize});print(__import__('binascii').b2a_base64(data).decode().strip()) if data else print('EOF')", cancellationToken);

                stopwatch.Stop();

                // Check for end of file
                if (chunkResult.Trim() == "EOF" || string.IsNullOrWhiteSpace(chunkResult)) {
                    break; // End of file
                }

                // Convert base64 back to bytes (more efficient than parsing Python repr)
                try {
                    var chunkBytes = Convert.FromBase64String(chunkResult.Trim());
                    await contents.WriteAsync(chunkBytes, cancellationToken);

                    // Update optimizer with performance metrics
                    chunkOptimizer.RecordTransfer(chunkBytes.Length, stopwatch.Elapsed);

                    this.logger.LogTrace(
                        "Read chunk: {ChunkSize} bytes in {Duration}ms, total: {Total} bytes",
                        chunkBytes.Length, stopwatch.ElapsedMilliseconds, contents.Length);
                }
                catch (FormatException ex) {
                    throw new DeviceException($"Invalid base64 data received from device: {chunkResult.Trim()}", ex);
                }
            }

            this.logger.LogDebug("Successfully read file {Path} ({Size} bytes)", remotePath, contents.Length);
            return contents.ToArray();
        }
        catch (Exception ex) when (ex is not DeviceException and not OperationCanceledException) {
            this.logger.LogError(ex, "Failed to read file {Path}", remotePath);
            throw new DeviceException($"Failed to read file '{remotePath}': {ex.Message}", ex);
        }
        finally {
            // Ensure file is closed even if an error occurs
            if (fileOpened) {
                try {
                    // Use timeout for cleanup to prevent hanging
                    using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await this.ExecuteAsync("try:\n    f.close()\nexcept:\n    pass", cleanupCts.Token);
                }
                catch (Exception ex) {
                    this.logger.LogWarning(ex, "Failed to close file handle during cleanup");
                }
            }
        }
    }

    /// <summary>
    /// Escapes a string for safe use in Python code by escaping backslashes and single quotes.
    /// </summary>
    /// <param name="input">The string to escape.</param>
    /// <returns>The escaped string safe for use in Python string literals.</returns>
    private static string EscapePythonString(string input) {
        if (string.IsNullOrEmpty(input)) {
            return input;
        }

        return input.Replace("\\", "\\\\")
                   .Replace("'", "\\'")
                   .Replace("\r", "\\r")
                   .Replace("\n", "\\n")
                   .Replace("\t", "\\t");
    }

    /// <inheritdoc />
    public void Dispose() {
        _ = this.DisconnectAsync();
    }
}
