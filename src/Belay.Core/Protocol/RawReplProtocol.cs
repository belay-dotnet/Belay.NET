// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Protocol;

using System.Linq;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents the current state of the Raw REPL protocol.
/// </summary>
public enum RawReplState {
    /// <summary>Normal interactive REPL mode.</summary>
    Normal,

    /// <summary>Raw mode for programmatic code execution.</summary>
    Raw,

    /// <summary>Raw-paste mode with flow control.</summary>
    RawPaste,
}

/// <summary>
/// Represents the response from a Raw REPL command execution.
/// </summary>
public class RawReplResponse {
    /// <inheritdoc/>
    public bool IsSuccess { get; set; }

    /// <inheritdoc/>
    public string Output { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string ErrorOutput { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string Result { get; set; } = string.Empty;

    /// <inheritdoc/>
    public Exception? Exception { get; set; }
}

/// <summary>
/// Exception thrown when Raw REPL protocol encounters an error.
/// </summary>
/// <inheritdoc/>
public class RawReplProtocolException(string message, RawReplState expectedState, RawReplState actualState) : Exception(message) {
    /// <inheritdoc/>
    public RawReplState ExpectedState { get; }

    /// <inheritdoc/>
    public RawReplState ActualState { get; }
}

/// <summary>
/// Exception thrown when flow control encounters an error.
/// </summary>
/// <inheritdoc/>
public class FlowControlException(string message, int windowSize, byte receivedByte) : RawReplProtocolException(message, RawReplState.RawPaste, RawReplState.RawPaste) {
    /// <inheritdoc/>
    public int WindowSize { get; }

    /// <inheritdoc/>
    public byte ReceivedByte { get; }
}

/// <summary>
/// Implementation of MicroPython Raw REPL protocol with support for both raw mode and raw-paste mode.
/// </summary>
/// <inheritdoc/>
public class RawReplProtocol : IDisposable {
    private readonly Stream stream;
    private readonly ILogger<RawReplProtocol> logger;
    private readonly SemaphoreSlim protocolSemaphore;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RawReplProtocol"/> class.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="logger"></param>
    public RawReplProtocol(Stream stream, ILogger<RawReplProtocol> logger) {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.protocolSemaphore = new SemaphoreSlim(1, 1);
        this.CurrentState = RawReplState.Normal;
    }

    // Control characters for Raw REPL protocol
    private const byte CTRLA = 0x01; // Enter raw REPL
    private const byte CTRLB = 0x02; // Exit raw REPL
    private const byte CTRLC = 0x03; // KeyboardInterrupt
    private const byte CTRLD = 0x04; // Execute/End data
    private const byte CTRLE = 0x05; // Raw-paste mode prefix

    // Raw-paste mode initialization sequence
    private static readonly byte[] RAWPASTEINIT =[CTRLE, (byte)'A', CTRLA];

    /// <summary>
    /// Gets current state of the Raw REPL protocol.
    /// </summary>
    public RawReplState CurrentState { get; private set; }

    /// <summary>
    /// Initialize the Raw REPL protocol.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default) {
        await this.protocolSemaphore.WaitAsync(cancellationToken);

        try {
            this.logger.LogDebug("Initializing Raw REPL protocol");

            // Wait for initial startup output and banner
            this.logger.LogDebug("Waiting for MicroPython startup...");
            await Task.Delay(1000, cancellationToken); // Give MicroPython time to start

            // Drain any startup output
            this.logger.LogDebug("Draining startup output...");
            await this.DrainAvailableOutputAsync(cancellationToken);

            // Send \r\x03 (carriage return + Ctrl-C) to interrupt any running program
            this.logger.LogDebug("Sending interrupt sequence...");
            await this.stream.WriteAsync(new byte[] { 0x0D, CTRLC }, cancellationToken);
            await this.stream.FlushAsync(cancellationToken);

            // Small delay for interrupt to process
            await Task.Delay(100, cancellationToken);

            // Drain interrupt response
            this.logger.LogDebug("Draining interrupt response...");
            await this.DrainAvailableOutputAsync(cancellationToken);

            this.CurrentState = RawReplState.Normal;
            this.logger.LogDebug("Raw REPL protocol initialized successfully");
        }
        finally {
            this.protocolSemaphore.Release();
        }
    }

    /// <summary>
    /// Execute code using the Raw REPL protocol.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<RawReplResponse> ExecuteCodeAsync(string code, bool useRawPasteMode = true,
        CancellationToken cancellationToken = default) {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(RawReplProtocol));
        }

        await this.protocolSemaphore.WaitAsync(cancellationToken);

        try {
            this.logger.LogDebug("Executing code with Raw REPL: {Code}", code);

            if (useRawPasteMode && await SupportsRawPasteModeAsync()) {
                return await this.ExecuteWithRawPasteModeAsync(code, cancellationToken);
            }
            else {
                return await this.ExecuteWithRawModeAsync(code, cancellationToken);
            }
        }
        finally {
            this.protocolSemaphore.Release();
        }
    }

    /// <summary>
    /// Enter raw REPL mode.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task EnterRawModeAsync(CancellationToken cancellationToken = default) {
        if (this.CurrentState == RawReplState.Raw) {
            return;
        }

        this.logger.LogDebug("Entering raw REPL mode");

        // Send Ctrl-A to enter raw mode
        await this.stream.WriteAsync(new byte[] { CTRLA }, cancellationToken);
        await this.stream.FlushAsync(cancellationToken);

        // Read response - MicroPython unix port may send banner first, then "raw REPL"
        this.logger.LogDebug("Starting to read Raw REPL response...");
        string response = await this.ReadWithTimeoutAsync(2000, cancellationToken);
        this.logger.LogDebug($"Raw REPL response: '{response}' (length: {response.Length})");

        if (!response.Contains("raw REPL")) {
            // Try waiting a bit longer for the complete response
            this.logger.LogDebug("Raw REPL not found in first read, trying additional read...");
            await Task.Delay(500, cancellationToken);
            string additional = await this.ReadWithTimeoutAsync(1000, cancellationToken);
            response += additional;
            this.logger.LogDebug($"Additional response: '{additional}' (length: {additional.Length})");
            this.logger.LogDebug($"Combined response: '{response}' (length: {response.Length})");

            if (!response.Contains("raw REPL")) {
                throw new RawReplProtocolException(
                    $"Failed to enter raw REPL mode - response: {response}",
                    RawReplState.Raw, this.CurrentState);
            }
        }

        this.CurrentState = RawReplState.Raw;
        this.logger.LogDebug("Successfully entered raw REPL mode");
    }

    /// <summary>
    /// Exit raw REPL mode back to normal mode.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ExitRawModeAsync(CancellationToken cancellationToken = default) {
        if (this.CurrentState == RawReplState.Normal) {
            return;
        }

        this.logger.LogDebug("Exiting raw REPL mode");

        // Send Ctrl-B to exit raw mode
        await this.WriteByteAsync(CTRLB, cancellationToken);

        // Wait for normal prompt ">>>"
        await this.ReadUntilPromptAsync(cancellationToken);

        this.CurrentState = RawReplState.Normal;
        this.logger.LogDebug("Successfully exited raw REPL mode");
    }

    private static Task<bool> SupportsRawPasteModeAsync() {
        // For now, assume raw-paste mode is supported
        // In a full implementation, this would probe the device
        return Task.FromResult(true);
    }

    private async Task<RawReplResponse> ExecuteWithRawModeAsync(string code, CancellationToken cancellationToken) {
        await this.EnterRawModeAsync(cancellationToken);

        try {
            this.logger.LogDebug("Executing code in Raw REPL mode: {Code}", code);

            // Send the code
            byte[] codeBytes = System.Text.Encoding.UTF8.GetBytes(code);
            await this.stream.WriteAsync(codeBytes, cancellationToken);
            await this.stream.FlushAsync(cancellationToken);

            // Send Ctrl-D to execute
            await this.WriteByteAsync(CTRLD, cancellationToken);

            // Wait for "OK" response with timeout
            string response = await this.ReadWithTimeoutAsync(500, cancellationToken);

            if (!response.Contains("OK")) {
                throw new RawReplProtocolException(
                    $"Expected 'OK' response, got: {response}",
                    RawReplState.Raw, this.CurrentState);
            }

            this.logger.LogDebug("Received OK confirmation from MicroPython");

            // Read execution output until we get back to raw REPL prompt
            string output = await this.ReadWithTimeoutAsync(2000, cancellationToken);

            return ParseResponse(output);
        }
        finally {
            await this.ExitRawModeAsync(cancellationToken);
        }
    }

    private async Task<RawReplResponse> ExecuteWithRawPasteModeAsync(string code, CancellationToken cancellationToken) {
        await this.EnterRawModeAsync(cancellationToken);

        try {
            // Enter raw-paste mode
            await this.stream.WriteAsync(RAWPASTEINIT, cancellationToken);

            // Read response to confirm raw-paste mode support
            string response = await this.ReadLineAsync(cancellationToken);
            if (!response.StartsWith('R')) {
                throw new RawReplProtocolException(
                    "Raw-paste mode not supported",
                    RawReplState.RawPaste, this.CurrentState);
            }

            // Read window size increment (16-bit little-endian)
            byte[] windowSizeBytes = new byte[2];
            await this.stream.ReadExactlyAsync(windowSizeBytes, cancellationToken);
            ushort windowSizeIncrement = BitConverter.ToUInt16(windowSizeBytes, 0);

            this.CurrentState = RawReplState.RawPaste;
            this.logger.LogDebug("Entered raw-paste mode with window size increment: {WindowSize}", windowSizeIncrement);

            // Send code with flow control
            await this.SendCodeWithFlowControlAsync(code, windowSizeIncrement, cancellationToken);

            // Send end-of-data marker
            await this.WriteByteAsync(CTRLD, cancellationToken);

            // Read execution output
            string output = await this.ReadUntilPromptAsync(cancellationToken);

            return ParseResponse(output);
        }
        finally {
            this.CurrentState = RawReplState.Raw;
            await this.ExitRawModeAsync(cancellationToken);
        }
    }

    private async Task SendCodeWithFlowControlAsync(string code, int windowSizeIncrement,
        CancellationToken cancellationToken) {
        byte[] codeBytes = System.Text.Encoding.UTF8.GetBytes(code);
        int remainingWindowSize = windowSizeIncrement;
        int offset = 0;

        while (offset < codeBytes.Length) {
            if (remainingWindowSize == 0) {
                // Wait for flow control signal
                remainingWindowSize = await this.WaitForFlowControlAsync(cancellationToken);
            }

            int chunkSize = Math.Min(remainingWindowSize, codeBytes.Length - offset);
            await this.stream.WriteAsync(codeBytes.AsMemory(offset, chunkSize), cancellationToken);

            offset += chunkSize;
            remainingWindowSize -= chunkSize;
        }
    }

    private async Task<int> WaitForFlowControlAsync(CancellationToken cancellationToken) {
        byte[] buffer = new byte[1];
        await this.stream.ReadExactlyAsync(buffer, cancellationToken);

        return buffer[0] switch {
            0x01 => 256, // Increase window size
            0x04 => throw new FlowControlException("Unexpected end-of-data signal", 0, buffer[0]),
            _ => throw new FlowControlException($"Unexpected flow control byte: 0x{buffer[0]:X2}", 0, buffer[0]),
        };
    }

    private async Task WriteByteAsync(byte value, CancellationToken cancellationToken) {
        await this.stream.WriteAsync(new[] { value }, cancellationToken);
        await this.stream.FlushAsync(cancellationToken);
    }

    private async Task<string> ReadLineAsync(CancellationToken cancellationToken) {
        var buffer = new List<byte>();
        byte[] byteBuffer = new byte[1];

        while (true) {
            await this.stream.ReadExactlyAsync(byteBuffer, cancellationToken);
            byte b = byteBuffer[0];

            if (b == '\n') {
                break;
            }

            if (b != '\r') {
                buffer.Add(b);
            }
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private async Task<string> ReadUntilAsync(string marker, CancellationToken cancellationToken) {
        var buffer = new List<byte>();
        byte[] markerBytes = System.Text.Encoding.UTF8.GetBytes(marker);
        int markerIndex = 0;
        byte[] byteBuffer = new byte[1];

        while (markerIndex < markerBytes.Length) {
            await this.stream.ReadExactlyAsync(byteBuffer, cancellationToken);
            byte b = byteBuffer[0];
            buffer.Add(b);

            if (b == markerBytes[markerIndex]) {
                markerIndex++;
            }
            else {
                markerIndex = (b == markerBytes[0]) ? 1 : 0;
            }
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private async Task<string> ReadUntilPromptAsync(CancellationToken cancellationToken) {
        return await this.ReadUntilAsync(">", cancellationToken);
    }

    private async Task<int> ReadAvailableAsync(byte[] buffer, CancellationToken cancellationToken) {
        // Use a timeout-based approach instead of ReadExactlyAsync
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Short timeout

        try {
            return await this.stream.ReadAsync(buffer, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested) {
            return 0; // Timeout - no data available
        }
    }

    private async Task DrainAvailableOutputAsync(CancellationToken cancellationToken) {
        byte[] buffer = new byte[1024];
        var totalRead = new System.Text.StringBuilder();
        int drainAttempts = 0;
        const int maxDrainAttempts = 10; // Prevent infinite loops

        while (drainAttempts < maxDrainAttempts) {
            drainAttempts++;
            int bytesRead = await this.ReadAvailableAsync(buffer, cancellationToken);
            if (bytesRead == 0) {
                // No data available - wait a bit and try once more to be sure
                if (drainAttempts == 1) {
                    await Task.Delay(50, cancellationToken);
                    continue;
                }

                break; // No more data available
            }

            string text = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
            totalRead.Append(text);
            this.logger.LogDebug("Drained output (attempt {Attempt}): {Output}", drainAttempts, text.Replace("\r", "\\r").Replace("\n", "\\n"));
        }

        if (totalRead.Length > 0) {
            this.logger.LogDebug("Total drained output: '{Output}' (length: {Length})", totalRead.ToString().Replace("\r", "\\r").Replace("\n", "\\n"), totalRead.Length);
        }
        else {
            this.logger.LogDebug("No output to drain after {Attempts} attempts", drainAttempts);
        }
    }

    private async Task<string> ReadWithTimeoutAsync(int timeoutMs, CancellationToken cancellationToken) {
        this.logger.LogDebug($"ReadWithTimeoutAsync starting with timeout {timeoutMs}ms");
        var buffer = new List<byte>();
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        int readAttempts = 0;

        while (DateTime.UtcNow - startTime < timeout) {
            readAttempts++;

            // Use ReadAvailableAsync which handles non-blocking reads with a short timeout
            byte[] readBuffer = new byte[1024];
            int bytesRead = await this.ReadAvailableAsync(readBuffer, cancellationToken);

            if (bytesRead > 0) {
                buffer.AddRange(readBuffer.Take(bytesRead));
                this.logger.LogDebug($"Read {bytesRead} bytes on attempt {readAttempts}");

                // Check if we have enough data - look for specific completion markers
                string partial = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
                if (partial.Contains("raw REPL") && (partial.Contains("CTRL-B") || partial.Contains(">"))) {
                    // We have the complete Raw REPL entry message
                    this.logger.LogDebug("Found complete Raw REPL entry message");
                    break;
                }
                else if (partial.Contains("OK") && (partial.Contains("\x04") || partial.Contains(">"))) {
                    // We have execution output
                    this.logger.LogDebug("Found execution output");
                    break;
                }
            }

            // Small delay between read attempts
            await Task.Delay(10, cancellationToken);
        }

        var result = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
        this.logger.LogDebug($"ReadWithTimeoutAsync completed after {readAttempts} attempts, returning {result.Length} chars");
        return result;
    }

    private static RawReplResponse ParseResponse(string output) {
        var response = new RawReplResponse();

        // Simple response parsing - in a full implementation this would be more sophisticated
        if (output.Contains("Traceback") || output.Contains("Error")) {
            response.IsSuccess = false;
            response.ErrorOutput = output;
            response.Exception = new Exception($"Device execution error: {output}");
        }
        else {
            response.IsSuccess = true;
            response.Output = output;
            response.Result = output.Trim();
        }

        return response;
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (this.disposed) {
            return;
        }

        this.protocolSemaphore.Dispose();
        this.disposed = true;
    }
}
