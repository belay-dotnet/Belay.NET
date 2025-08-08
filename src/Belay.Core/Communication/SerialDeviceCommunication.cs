// Copyright (c) 2024 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for more information.
using System.IO.Ports;
using System.Text;
using System.Text.Json;
using Belay.Core.Protocol;
using Microsoft.Extensions.Logging;

/// <summary>
/// Serial/USB implementation of device communication for MicroPython devices.
/// </summary>
public class SerialDeviceCommunication : IDeviceCommunication {
    private readonly SerialPort serialPort;
    private RawReplProtocol? replProtocol;
    private readonly SemaphoreSlim executionSemaphore;
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly ILogger<SerialDeviceCommunication> logger;
    private readonly ReconnectionPolicy reconnectionPolicy;
    private readonly List<string> commandHistory;
    private bool disposed;

    private const int DefaultTimeout = 30000; // 30 seconds
    private const int MaxCommandHistoryLength = 1000;

    /// <inheritdoc/>
    public SerialDeviceCommunication(string portName, int baudRate = 115200,
        int timeout = DefaultTimeout, ILogger<SerialDeviceCommunication>? logger = null) {
        if (string.IsNullOrWhiteSpace(portName)) {
            throw new ArgumentException("Port name cannot be null or empty", nameof(portName));
        }

        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SerialDeviceCommunication>.Instance;
        this.executionSemaphore = new SemaphoreSlim(1, 1);
        this.cancellationTokenSource = new CancellationTokenSource();
        this.commandHistory =[];
        this.reconnectionPolicy = new ReconnectionPolicy();

        this.serialPort = new SerialPort(portName, baudRate) {
            Parity = Parity.None,
            DataBits = 8,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = timeout,
            WriteTimeout = timeout,
            NewLine = "\n",
        };

        // RawReplProtocol will be created when the port is opened
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
    /// Connect to the device.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task ConnectAsync(CancellationToken cancellationToken = default) {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(SerialDeviceCommunication));
        }

        if (this.State == DeviceConnectionState.Connected) {
            return;
        }

        this.SetState(DeviceConnectionState.Connecting, "Initiating connection");

        try {
            this.logger.LogDebug(
                "Opening serial port {PortName} at {BaudRate} baud",
                this.serialPort.PortName, this.serialPort.BaudRate);

            // Open serial port
            this.serialPort.Open();

            // Create raw REPL protocol now that port is open
            this.replProtocol = new RawReplProtocol(
                this.serialPort.BaseStream,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<RawReplProtocol>.Instance);

            // Wait for device to be ready (may need soft reset)
            await this.WaitForDeviceReadyAsync(cancellationToken);

            // Initialize raw REPL protocol
            await this.replProtocol.InitializeAsync(cancellationToken);

            this.SetState(DeviceConnectionState.Connected, "Successfully connected");
            this.logger.LogInformation("Successfully connected to device on {PortName}", this.serialPort.PortName);
        }
        catch (Exception ex) {
            this.SetState(DeviceConnectionState.Error, $"Connection failed: {ex.Message}", ex);
            this.logger.LogError(ex, "Failed to connect to device on {PortName}", this.serialPort.PortName);

            if (this.serialPort.IsOpen) {
                this.serialPort.Close();
            }

            throw new DeviceConnectionException($"Failed to connect to device on {this.serialPort.PortName}", ex) {
                PortName = this.serialPort.PortName,
            };
        }
    }

    /// <summary>
    /// Disconnect from the device.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public virtual async Task DisconnectAsync(CancellationToken cancellationToken = default) {
        if (this.State == DeviceConnectionState.Disconnected) {
            return;
        }

        this.SetState(DeviceConnectionState.Disconnected, "Disconnecting");

        try {
            if (this.serialPort.IsOpen) {
                // Try to exit raw mode gracefully
                if (this.replProtocol != null) {
                    await this.replProtocol.ExitRawModeAsync(cancellationToken);
                }

                this.serialPort.Close();
            }
        }
        catch (Exception ex) {
            this.logger.LogWarning(ex, "Error during graceful disconnect");
        }

        this.logger.LogInformation("Disconnected from device on {PortName}", this.serialPort.PortName);
    }

    /// <summary>
    /// Execute Python code on the device and return the result as a string.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default) {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(SerialDeviceCommunication));
        }

        if (string.IsNullOrWhiteSpace(code)) {
            throw new ArgumentException("Code cannot be null or empty", nameof(code));
        }

        await this.EnsureConnectedAsync(cancellationToken);
        await this.executionSemaphore.WaitAsync(cancellationToken);

        try {
            this.SetState(DeviceConnectionState.Executing, "Executing code");

            // Record command for potential replay
            this.RecordCommand(code);

            // Execute via raw REPL protocol
            if (this.replProtocol == null) {
                throw new InvalidOperationException("Device not connected. Call ConnectAsync() first.");
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
        catch (Exception ex) when (IsConnectionError(ex)) {
            _ = Task.Run(() => this.HandleConnectionLostAsync(ex), cancellationToken);
            throw;
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
    /// Transfer a file from local system to device.
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

        byte[] fileContent = await File.ReadAllBytesAsync(localPath, cancellationToken);
        string base64Content = Convert.ToBase64String(fileContent);

        string code = $@"
import binascii
with open('{remotePath}', 'wb') as f:
    f.write(binascii.a2b_base64('{base64Content}'))
";

        await this.ExecuteAsync(code, cancellationToken);
        this.logger.LogDebug("Successfully transferred file from {LocalPath} to {RemotePath}", localPath, remotePath);
    }

    /// <summary>
    /// Retrieve a file from device to local system.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<byte[]> GetFileAsync(string remotePath, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(remotePath)) {
            throw new ArgumentException("Remote path cannot be null or empty", nameof(remotePath));
        }

        string code = $@"
import binascii
try:
    with open('{remotePath}', 'rb') as f:
        print(binascii.b2a_base64(f.read()).decode().strip())
except OSError:
    print('FILE_NOT_FOUND')
";

        string result = await this.ExecuteAsync(code, cancellationToken);

        if (result.Trim() == "FILE_NOT_FOUND") {
            throw new FileNotFoundException($"Remote file not found: {remotePath}");
        }

        try {
            return Convert.FromBase64String(result.Trim());
        }
        catch (FormatException ex) {
            throw new InvalidOperationException($"Failed to decode file content from device: {remotePath}", ex);
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken) {
        if (this.State != DeviceConnectionState.Connected) {
            await this.ConnectAsync(cancellationToken);
        }
    }

    private async Task WaitForDeviceReadyAsync(CancellationToken cancellationToken) {
        this.logger.LogDebug("Waiting for device to be ready");

        // Send interrupt (Ctrl-C) to get to clean state
        await this.SendControlCharacterAsync(0x03, cancellationToken); // Ctrl-C

        // Give device time to process interrupt
        await Task.Delay(100, cancellationToken);

        // Send soft reset if needed
        await this.SendControlCharacterAsync(0x04, cancellationToken); // Ctrl-D

        // Wait a bit more for reset to complete
        await Task.Delay(500, cancellationToken);

        this.logger.LogDebug("Device ready");
    }

    private async Task SendControlCharacterAsync(byte controlChar, CancellationToken cancellationToken) {
        byte[] buffer =[controlChar];
        await this.serialPort.BaseStream.WriteAsync(buffer, cancellationToken);
        await this.serialPort.BaseStream.FlushAsync(cancellationToken);
    }

    private void RecordCommand(string code) {
        if (this.commandHistory.Count >= MaxCommandHistoryLength) {
            this.commandHistory.RemoveAt(0);
        }

        this.commandHistory.Add(code);
    }

    private static bool IsConnectionError(Exception ex) {
        return ex is InvalidOperationException ||
               ex is IOException ||
               ex is TimeoutException ||
               ex is UnauthorizedAccessException;
    }

    private async Task HandleConnectionLostAsync(Exception ex) {
        if (!this.reconnectionPolicy.EnableAutoReconnect) {
            this.SetState(DeviceConnectionState.Error, "Connection lost", ex);
            return;
        }

        this.SetState(DeviceConnectionState.Reconnecting, "Attempting to reconnect");
        this.logger.LogWarning(ex, "Connection lost, attempting to reconnect");

        for (int attempt = 1; attempt <= this.reconnectionPolicy.MaxReconnectAttempts; attempt++) {
            try {
                await Task.Delay(this.CalculateReconnectDelay(attempt), this.cancellationTokenSource.Token);

                // Close existing connection
                if (this.serialPort.IsOpen) {
                    this.serialPort.Close();
                }

                // Attempt reconnection
                await this.ConnectAsync(this.cancellationTokenSource.Token);

                // Replay command history for state reconstruction
                await this.ReplayCommandHistoryAsync(this.cancellationTokenSource.Token);

                this.logger.LogInformation("Successfully reconnected after {Attempts} attempts", attempt);
                return; // Success
            }
            catch (Exception reconnectEx) {
                this.logger.LogWarning(reconnectEx, "Reconnection attempt {Attempt} failed", attempt);

                if (attempt == this.reconnectionPolicy.MaxReconnectAttempts) {
                    this.SetState(DeviceConnectionState.Error, "All reconnection attempts failed", reconnectEx);
                    throw new DeviceConnectionException("Device reconnection failed after maximum attempts", reconnectEx);
                }
            }
        }
    }

    private TimeSpan CalculateReconnectDelay(int attempt) {
        if (!this.reconnectionPolicy.ExponentialBackoff) {
            return this.reconnectionPolicy.ReconnectDelay;
        }

        double baseDelay = this.reconnectionPolicy.ReconnectDelay.TotalMilliseconds;
        double exponentialDelay = baseDelay * Math.Pow(2, attempt - 1);
        double maxDelay = Math.Min(exponentialDelay, 30000); // Cap at 30 seconds

        return TimeSpan.FromMilliseconds(maxDelay);
    }

    private async Task ReplayCommandHistoryAsync(CancellationToken cancellationToken) {
        this.logger.LogDebug("Replaying {Count} commands for state reconstruction", this.commandHistory.Count);

        foreach (string? command in this.commandHistory.ToList()) {
            try {
                if (this.replProtocol != null) {
                    await this.replProtocol.ExecuteCodeAsync(command, useRawPasteMode: true, cancellationToken);
                }
            }
            catch (Exception ex) {
                this.logger.LogWarning(ex, "Failed to replay command during reconnection: {Command}", command);

                // Continue with other commands
            }
        }
    }

    private void SetState(DeviceConnectionState newState, string? reason = null, Exception? exception = null) {
        DeviceConnectionState oldState = this.State;
        this.State = newState;

        this.StateChanged?.Invoke(this, new DeviceStateChangeEventArgs(oldState, newState, reason, exception));
        this.logger.LogDebug(
            "Device state changed from {OldState} to {NewState}: {Reason}",
            oldState, newState, reason ?? "No reason provided");
    }

    /// <inheritdoc/>
    protected virtual void Dispose(bool disposing) {
        if (!this.disposed) {
            if (disposing) {
                this.cancellationTokenSource.Cancel();

                try {
                    if (this.serialPort.IsOpen) {
                        this.serialPort.Close();
                    }
                }
                catch (Exception ex) {
                    this.logger.LogWarning(ex, "Error closing serial port during dispose");
                }

                this.serialPort.Dispose();
                this.replProtocol?.Dispose();
                this.executionSemaphore.Dispose();
                this.cancellationTokenSource.Dispose();
            }

            this.disposed = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Reconnection policy configuration for serial device communication.
/// </summary>
public class ReconnectionPolicy {
    /// <summary>
    /// Gets or sets a value indicating whether enable automatic reconnection on connection loss.
    /// </summary>
    public bool EnableAutoReconnect { get; set; } = true;

    /// <summary>
    /// Gets or sets delay between reconnection attempts.
    /// </summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets maximum number of reconnection attempts.
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether use exponential backoff for reconnection delays.
    /// </summary>
    public bool ExponentialBackoff { get; set; } = true;
}

/// <summary>
/// Exception thrown when device connection fails.
/// </summary>
public class DeviceConnectionException : Exception {
    /// <inheritdoc/>
    public DeviceConnectionException(string message)
        : base(message) {
    }

    /// <inheritdoc/>
    public DeviceConnectionException(string message, Exception innerException)
        : base(message, innerException) {
    }

    /// <summary>
    /// Gets or sets name of the port that failed to connect.
    /// </summary>
    public string? PortName { get; set; }
}

/// <summary>
/// Exception thrown when device code execution fails.
/// </summary>
public class DeviceExecutionException : Exception {
    /// <inheritdoc/>
    public DeviceExecutionException(string message)
        : base(message) {
    }

    /// <inheritdoc/>
    public DeviceExecutionException(string message, Exception innerException)
        : base(message, innerException) {
    }

    /// <summary>
    /// Gets or sets the code that was being executed when the error occurred.
    /// </summary>
    public string? ExecutedCode { get; set; }

    /// <summary>
    /// Gets or sets output received from the device.
    /// </summary>
    public string? DeviceOutput { get; set; }

    /// <summary>
    /// Gets or sets traceback information from the device.
    /// </summary>
    public string? DeviceTraceback { get; set; }
}
