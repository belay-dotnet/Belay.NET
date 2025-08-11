// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Communication;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Belay.Core.Protocol;
using Microsoft.Extensions.Logging;

/// <summary>
/// Subprocess-based device communication using MicroPython unix port for testing.
/// </summary>
public class SubprocessDeviceCommunication : IDeviceCommunication {
    private readonly Process micropythonProcess;
    private readonly SemaphoreSlim executionSemaphore;
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly ILogger<SubprocessDeviceCommunication> logger;

    private StreamWriter? stdin;
    private StreamReader? stdout;
    private StreamReader? stderr;
    private RawReplProtocol? replProtocol;
    private Task? outputMonitorTask;
    private Task? errorMonitorTask;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubprocessDeviceCommunication"/> class.
    /// </summary>
    /// <param name="micropythonExecutablePath">Path to the MicroPython executable for subprocess communication.</param>
    /// <param name="additionalArgs">Additional command-line arguments to pass to MicroPython process.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentException">Thrown when micropythonExecutablePath is null or empty.</exception>
    public SubprocessDeviceCommunication(
        string micropythonExecutablePath = "micropython",
        string[]? additionalArgs = null, ILogger<SubprocessDeviceCommunication>? logger = null) {
        if (string.IsNullOrWhiteSpace(micropythonExecutablePath)) {
            micropythonExecutablePath = "micropython";
        }

        try {
            // Validate executable path
            if (!System.IO.File.Exists(micropythonExecutablePath)) {
                // First, try resolving using PATH
                var resolvedPath = System.Environment.GetEnvironmentVariable("PATH")
                    ?.Split(System.IO.Path.PathSeparator)
                    .Select(p => System.IO.Path.Combine(p, micropythonExecutablePath))
                    .FirstOrDefault(System.IO.File.Exists);

                if (resolvedPath != null) {
                    micropythonExecutablePath = resolvedPath;
                }
                else {
                    logger?.LogWarning($"MicroPython executable not found: {micropythonExecutablePath}");
                    throw new System.IO.FileNotFoundException($"MicroPython executable not found: {micropythonExecutablePath}");
                }
            }
        }
        catch (Exception ex) {
            logger?.LogWarning(ex, $"Error locating MicroPython executable: {ex.Message}");
        }

        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SubprocessDeviceCommunication>.Instance;
        this.executionSemaphore = new SemaphoreSlim(1, 1);
        this.cancellationTokenSource = new CancellationTokenSource();

        var startInfo = new ProcessStartInfo {
            FileName = micropythonExecutablePath,
            Arguments = "-i " + string.Join(" ", additionalArgs ??[]), // Add -i flag for interactive REPL
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory,
        };

        this.micropythonProcess = new Process {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };
        this.micropythonProcess.Exited += this.OnProcessExited;

        // Configure streams for immediate I/O to prevent buffering issues
        this.micropythonProcess.StartInfo.StandardInputEncoding = Encoding.UTF8;
        this.micropythonProcess.StartInfo.StandardOutputEncoding = Encoding.UTF8;

        this.State = DeviceConnectionState.Disconnected;
    }

    /// <summary>
    /// Gets current connection state of the device.
    /// </summary>
    public DeviceConnectionState State { get; private set; }

    /// <summary>
    /// Event raised when output is received from the device
    /// </summary>
    public event EventHandler<DeviceOutputEventArgs>? OutputReceived;

    /// <summary>
    /// Event raised when device connection state changes
    /// </summary>
    public event EventHandler<DeviceStateChangeEventArgs>? StateChanged;

    /// <summary>
    /// Start the subprocess and establish communication.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default) {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(SubprocessDeviceCommunication));
        }

        if (this.State != DeviceConnectionState.Disconnected) {
            throw new InvalidOperationException("Subprocess is already started");
        }

        this.SetState(DeviceConnectionState.Connecting, "Starting MicroPython subprocess");

        try {
            this.logger.LogDebug(
                "Starting MicroPython subprocess: {FileName} {Arguments}",
                this.micropythonProcess.StartInfo.FileName, this.micropythonProcess.StartInfo.Arguments);

            // Start the process
            if (!this.micropythonProcess.Start()) {
                throw new InvalidOperationException("Failed to start MicroPython process");
            }

            // Initialize stream wrappers
            this.stdin = this.micropythonProcess.StandardInput;
            this.stdout = this.micropythonProcess.StandardOutput;
            this.stderr = this.micropythonProcess.StandardError;

            // Create duplex stream for basic REPL protocol (MVP fallback)
            var duplexStream = new DuplexStream(
                this.micropythonProcess.StandardInput.BaseStream,
                this.micropythonProcess.StandardOutput.BaseStream);
            this.replProtocol = new RawReplProtocol(
                duplexStream,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<RawReplProtocol>.Instance);

            // Initialize raw REPL protocol
            await this.replProtocol.InitializeAsync(cancellationToken);

            // Start background output monitoring
            this.outputMonitorTask = Task.Run(() => this.MonitorOutputAsync(this.cancellationTokenSource.Token), cancellationToken);
            this.errorMonitorTask = Task.Run(() => this.MonitorErrorAsync(this.cancellationTokenSource.Token), cancellationToken);

            // Wait for MicroPython to be ready
            await this.WaitForReadyStateAsync(cancellationToken);

            this.SetState(DeviceConnectionState.Connected, "MicroPython subprocess started successfully");
            this.logger.LogInformation("Successfully started MicroPython subprocess");
        }
        catch (Exception ex) {
            this.SetState(DeviceConnectionState.Error, $"Failed to start subprocess: {ex.Message}", ex);
            this.logger.LogError(ex, "Failed to start MicroPython subprocess");

            await this.StopAsync(CancellationToken.None);
            throw new DeviceConnectionException("Failed to start MicroPython subprocess", ex);
        }
    }

    /// <summary>
    /// Stop the subprocess.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default) {
        if (this.State == DeviceConnectionState.Disconnected) {
            return;
        }

        this.SetState(DeviceConnectionState.Disconnected, "Stopping subprocess");

        try {
            this.cancellationTokenSource.Cancel();

            if (!this.micropythonProcess.HasExited) {
                try {
                    // Try graceful shutdown first
                    if (this.stdin != null) {
                        await this.stdin.WriteLineAsync("exit()");
                        await this.stdin.FlushAsync(cancellationToken);
                    }

                    // Wait a bit for graceful shutdown
                    if (!this.micropythonProcess.WaitForExit(2000)) {
                        this.logger.LogWarning("Subprocess did not exit gracefully, killing process");
                        this.micropythonProcess.Kill();
                        await this.micropythonProcess.WaitForExitAsync(cancellationToken);
                    }
                }
                catch (Exception ex) {
                    this.logger.LogWarning(ex, "Error during graceful subprocess shutdown");

                    if (!this.micropythonProcess.HasExited) {
                        this.micropythonProcess.Kill();
                    }
                }
            }

            // Wait for monitoring tasks to complete
            if (this.outputMonitorTask != null) {
                try {
                    await this.outputMonitorTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (TimeoutException) {
                    this.logger.LogWarning("Output monitor task did not complete in time");
                }
            }

            if (this.errorMonitorTask != null) {
                try {
                    await this.errorMonitorTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
                }
                catch (TimeoutException) {
                    this.logger.LogWarning("Error monitor task did not complete in time");
                }
            }
        }
        catch (Exception ex) {
            this.logger.LogError(ex, "Error stopping subprocess");
        }

        this.logger.LogInformation("MicroPython subprocess stopped");
    }

    /// <summary>
    /// Execute Python code on the device and return the result as a string.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default) {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(SubprocessDeviceCommunication));
        }

        if (string.IsNullOrWhiteSpace(code)) {
            throw new ArgumentException("Code cannot be null or empty", nameof(code));
        }

        await this.EnsureConnectedAsync(cancellationToken);
        await this.executionSemaphore.WaitAsync(cancellationToken);

        try {
            this.SetState(DeviceConnectionState.Executing, "Executing code");

            // Execute via raw REPL protocol
            if (this.replProtocol == null) {
                throw new InvalidOperationException("Device not connected");
            }

            RawReplResponse response = await this.replProtocol.ExecuteCodeAsync(code, useRawPasteMode: true, cancellationToken);

            if (!response.IsSuccess) {
                var exception = new DeviceExecutionException("Code execution failed on device") {
                    DeviceOutput = response.ErrorOutput,
                    ExecutedCode = code,
                    DeviceTraceback = response.ErrorOutput,
                };

                if (response.Exception != null) {
                    exception = new DeviceExecutionException("Code execution failed on device", response.Exception) {
                        DeviceOutput = response.ErrorOutput,
                        ExecutedCode = code,
                        DeviceTraceback = response.ErrorOutput,
                    };
                }

                throw exception;
            }

            // Forward any output to event handlers
            if (!string.IsNullOrEmpty(response.Output)) {
                this.OutputReceived?.Invoke(this, new DeviceOutputEventArgs(response.Output));
            }

            this.SetState(DeviceConnectionState.Connected, "Execution completed");
            return response.Result;
        }
        finally {
            this.executionSemaphore.Release();
        }
    }

    /// <summary>
    /// Execute Python code on the device and return the result as typed object.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default) {
        string result = await this.ExecuteAsync(code, cancellationToken);

        if (string.IsNullOrWhiteSpace(result)) {
            if (typeof(T) == typeof(string)) {
                return (T)(object)string.Empty;
            }

            if (Nullable.GetUnderlyingType(typeof(T)) != null) {
                return default!;
            }

            // For object type (used by void methods), return null when no result
            if (typeof(T) == typeof(object)) {
                return (T)(object?)null!;
            }

            throw new InvalidOperationException($"Cannot convert empty result to {typeof(T).Name}");
        }

        try {
            // Try JSON deserialization first for complex types
            if (typeof(T) != typeof(string) && (result.StartsWith('{') || result.StartsWith('['))) {
                return JsonSerializer.Deserialize<T>(result)!;
            }

            // Fallback to simple type conversion for basic types
            return (T)Convert.ChangeType(result.Trim(), typeof(T))!;
        }
        catch (JsonException ex) {
            throw new InvalidOperationException($"Failed to deserialize result '{result}' to type {typeof(T).Name}", ex);
        }
        catch (InvalidCastException ex) {
            throw new InvalidOperationException($"Failed to convert result '{result}' to type {typeof(T).Name}", ex);
        }
    }

    /// <summary>
    /// Transfer a file from local system to device (simulated for subprocess).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task PutFileAsync(string localPath, string remotePath, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(localPath)) {
            throw new ArgumentException("Local path cannot be null or empty", nameof(localPath));
        }

        if (string.IsNullOrWhiteSpace(remotePath)) {
            throw new ArgumentException("Remote path cannot be null or empty", nameof(remotePath));
        }

        if (!File.Exists(localPath)) {
            throw new FileNotFoundException($"Local file not found: {localPath}");
        }

        // For subprocess, we simulate file operations using the working directory
        string workingDirPath = Path.Combine(Environment.CurrentDirectory, remotePath.TrimStart('/'));
        string? workingDirDir = Path.GetDirectoryName(workingDirPath);

        if (!string.IsNullOrEmpty(workingDirDir) && !Directory.Exists(workingDirDir)) {
            Directory.CreateDirectory(workingDirDir);
        }

        await Task.Run(() => File.Copy(localPath, workingDirPath, true), cancellationToken);

        this.logger.LogDebug(
            "Simulated file transfer from {LocalPath} to {RemotePath} (working dir: {WorkingPath})",
            localPath, remotePath, workingDirPath);
    }

    /// <summary>
    /// Retrieve a file from device (simulated for subprocess).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<byte[]> GetFileAsync(string remotePath, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(remotePath)) {
            throw new ArgumentException("Remote path cannot be null or empty", nameof(remotePath));
        }

        // For subprocess, we simulate file operations using the working directory
        string workingDirPath = Path.Combine(Environment.CurrentDirectory, remotePath.TrimStart('/'));

        if (!File.Exists(workingDirPath)) {
            throw new FileNotFoundException($"Simulated remote file not found: {remotePath}");
        }

        return await File.ReadAllBytesAsync(workingDirPath, cancellationToken);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken) {
        if (this.State != DeviceConnectionState.Connected) {
            await this.StartAsync(cancellationToken);
        }
    }

    private async Task WaitForReadyStateAsync(CancellationToken cancellationToken) {
        this.logger.LogDebug("Waiting for MicroPython subprocess to be ready");

        // Give MicroPython additional time to complete initialization
        await Task.Delay(3000, cancellationToken);

        // Simple test to ensure the subprocess can enter Raw REPL
        try {
            if (this.replProtocol == null) {
                throw new InvalidOperationException("Protocol not initialized");
            }

            this.logger.LogDebug("Testing subprocess adaptive REPL protocol");

            // Test basic execution to verify the adaptive protocol is working
            var testResponse = await this.replProtocol.ExecuteCodeAsync("1+1", useRawPasteMode: true, cancellationToken);
            if (!testResponse.IsSuccess) {
                throw new InvalidOperationException($"Adaptive REPL protocol test failed: {testResponse.ErrorOutput}");
            }

            this.logger.LogDebug("MicroPython subprocess adaptive REPL protocol test successful");
            return;
        }
        catch (Exception ex) {
            this.logger.LogWarning(ex, "Raw REPL test failed - this may be normal during startup");
        }

        this.logger.LogDebug("MicroPython subprocess startup sequence completed");
    }

    private async Task MonitorOutputAsync(CancellationToken cancellationToken) {
        try {
            while (!cancellationToken.IsCancellationRequested &&
                   !this.micropythonProcess.HasExited &&
                   this.stdout != null) {
                string? line = await this.stdout.ReadLineAsync(cancellationToken);
                if (line != null) {
                    this.OutputReceived?.Invoke(this, new DeviceOutputEventArgs(line));
                }
            }
        }
        catch (OperationCanceledException) {
            // Expected during shutdown
        }
        catch (Exception ex) {
            this.logger.LogError(ex, "Error monitoring subprocess output");
        }
    }

    private async Task MonitorErrorAsync(CancellationToken cancellationToken) {
        try {
            while (!cancellationToken.IsCancellationRequested &&
                   !this.micropythonProcess.HasExited &&
                   this.stderr != null) {
                string? line = await this.stderr.ReadLineAsync(cancellationToken);
                if (line != null) {
                    // Treat stderr output as device errors
                    this.OutputReceived?.Invoke(this, new DeviceOutputEventArgs(line, isError: true));
                    this.logger.LogWarning("Subprocess stderr: {ErrorLine}", line);
                }
            }
        }
        catch (OperationCanceledException) {
            // Expected during shutdown
        }
        catch (Exception ex) {
            this.logger.LogError(ex, "Error monitoring subprocess stderr");
        }
    }

    private void OnProcessExited(object? sender, EventArgs e) {
        int exitCode = this.micropythonProcess.ExitCode;
        this.logger.LogWarning("MicroPython subprocess exited with code {ExitCode}", exitCode);

        this.SetState(DeviceConnectionState.Error, $"Subprocess exited unexpectedly with code {exitCode}");
    }

    private void SetState(DeviceConnectionState newState, string? reason = null, Exception? exception = null) {
        DeviceConnectionState oldState = this.State;
        this.State = newState;

        this.StateChanged?.Invoke(this, new DeviceStateChangeEventArgs(oldState, newState, reason, exception));
        this.logger.LogDebug(
            "Subprocess device state changed from {OldState} to {NewState}: {Reason}",
            oldState, newState, reason ?? "No reason provided");
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="SubprocessDeviceCommunication"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing) {
        if (!this.disposed) {
            if (disposing) {
                this.cancellationTokenSource.Cancel();

                try {
                    this.StopAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex) {
                    this.logger.LogWarning(ex, "Error during dispose");
                }

                this.micropythonProcess?.Dispose();
                this.replProtocol?.Dispose();
                this.executionSemaphore?.Dispose();
                this.cancellationTokenSource?.Dispose();
            }

            this.disposed = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose() {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Duplex stream implementation for combining stdin and stdout streams.
/// </summary>
/// <inheritdoc/>
public class DuplexStream : Stream {
    private readonly Stream inputStream;
    private readonly Stream outputStream;

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplexStream"/> class.
    /// </summary>
    /// <param name="inputStream"></param>
    /// <param name="outputStream"></param>
    public DuplexStream(Stream inputStream, Stream outputStream) {
        this.inputStream = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        this.outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
    }

    /// <inheritdoc/>
    public override bool CanRead => this.outputStream.CanRead;

    /// <inheritdoc/>
    public override bool CanWrite => this.inputStream.CanWrite;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) {
        return this.outputStream.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) {
        this.inputStream.Write(buffer, offset, count);
        this.inputStream.Flush(); // Ensure immediate transmission
    }

    /// <inheritdoc/>
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
        return await this.outputStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
        await this.inputStream.WriteAsync(buffer, offset, count, cancellationToken);
        await this.inputStream.FlushAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override void Flush() {
        this.inputStream.Flush();
    }

    /// <inheritdoc/>
    public override async Task FlushAsync(CancellationToken cancellationToken) {
        await this.inputStream.FlushAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void SetLength(long value) {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) {
        if (disposing) {
            this.inputStream?.Dispose();
            this.outputStream?.Dispose();
        }

        base.Dispose(disposing);
    }
}
