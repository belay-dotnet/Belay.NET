// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// Simple Raw REPL implementation following official MicroPython mpremote patterns.
/// Direct implementation without complex state management or capability detection.
/// </summary>
/// <remarks>
/// <para>
/// This class is designed for use by a single DeviceConnection and is NOT thread-safe.
/// Each DeviceConnection should create and use its own SimpleRawRepl instance exclusively.
/// Concurrent access from multiple threads will result in race conditions and protocol corruption.
/// </para>
/// <para>
/// The class maintains internal state (inRawRepl, atPrompt, useRawPaste) that tracks the
/// device's protocol state. These state variables must be accessed sequentially to ensure
/// protocol integrity and prevent communication errors.
/// </para>
/// </remarks>
public class SimpleRawRepl : IDisposable {
    private readonly Stream stream;
    private readonly ILogger logger;
    private readonly object stateLock = new object();
    private bool inRawRepl = false;
    private bool useRawPaste = true;
    private bool disposed;
    private bool atPrompt = false; // Track if we're already at a prompt
    private volatile bool operationInProgress = false; // Detect concurrent usage

    // Control characters from official implementation
    private const byte CTRLA = 0x01; // Enter raw REPL
    private const byte CTRLB = 0x02; // Exit raw REPL
    private const byte CTRLC = 0x03; // Interrupt
    private const byte CTRLD = 0x04; // Execute/End data
    private const byte CTRLE = 0x05; // Raw-paste mode prefix

    // Raw-paste mode initialization sequence
    private static readonly byte[] RAWPASTEINIT = [CTRLE, (byte)'A', CTRLA];

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleRawRepl"/> class.
    /// </summary>
    /// <param name="stream">The communication stream to the device.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public SimpleRawRepl(Stream stream, ILogger logger) {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Enters raw REPL mode with optional soft reset.
    /// Based on official mpremote enter_raw_repl implementation.
    /// </summary>
    /// <param name="softReset">Whether to perform a soft reset after entering raw mode.</param>
    /// <param name="timeoutSeconds">Timeout in seconds for the operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when concurrent operations are detected or object is disposed.</exception>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task EnterRawReplAsync(bool softReset = true, int timeoutSeconds = 10, CancellationToken cancellationToken = default) {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(SimpleRawRepl));
        }

        lock (this.stateLock) {
            if (this.inRawRepl) {
                return;
            }
        }

        // Note: We don't check operationInProgress here since this method is called from ExecuteAsync
        // which already has the protection. EnterRawReplAsync can be called independently but should
        // not conflict with ExecuteAsync due to the sequential usage pattern.
        this.logger.LogDebug("Entering raw REPL mode (softReset: {SoftReset})", softReset);

        // Follow official mpremote sequence exactly
        // Step 1: Send Ctrl-C to interrupt any running program
        await this.stream.WriteAsync(new byte[] { (byte)'\r', CTRLC }, cancellationToken);
        await this.stream.FlushAsync(cancellationToken);

        // Step 2: Flush input (without relying on serial.flushInput())
        await this.DrainInputAsync(cancellationToken);

        // Step 3: Send Ctrl-A to enter raw REPL (with carriage return as per official mpremote)
        await this.stream.WriteAsync(new byte[] { (byte)'\r', CTRLA }, cancellationToken);
        await this.stream.FlushAsync(cancellationToken);

        // Small delay to allow response
        await Task.Delay(200, cancellationToken);

        if (softReset) {
            // Wait for raw REPL prompt - be flexible with line endings and content
            var data = await this.ReadUntilAsync(">", timeoutSeconds, "prompt", cancellationToken);
            this.logger.LogDebug("Raw REPL prompt response: '{Data}' (length: {Length})", data.Replace("\r", "\\r").Replace("\n", "\\n"), data.Length);

            // Accept either full prompt or just the ">" indicating raw REPL mode
            bool isRawReplMode = data.Contains("raw REPL; CTRL-B to exit") ||
                                 (data.EndsWith('>') && data.Length >= 1);

            if (!isRawReplMode) {
                throw new DeviceException($"Could not enter raw REPL: '{data}' (length: {data.Length})");
            }

            this.logger.LogDebug("Raw REPL mode detected (full prompt: {FullPrompt})", data.Contains("raw REPL; CTRL-B to exit"));

            // Send Ctrl-D for soft reset
            await this.stream.WriteAsync(new byte[] { CTRLD }, cancellationToken);

            // Wait for "soft reboot" - be flexible with line endings
            data = await this.ReadUntilAsync("soft reboot", timeoutSeconds, "soft_reboot", cancellationToken);
            if (!data.Contains("soft reboot")) {
                throw new DeviceException($"Could not perform soft reboot: {data}");
            }
        }

        // Wait for final raw REPL prompt - be flexible with line endings and content
        var finalData = await this.ReadUntilAsync(">", timeoutSeconds, "prompt", cancellationToken);

        // Accept either full prompt or just the ">" indicating raw REPL mode
        bool isFinalRawReplMode = finalData.Contains("raw REPL; CTRL-B to exit") ||
                                  (finalData.EndsWith('>') && finalData.Length >= 1);

        if (!isFinalRawReplMode) {
            throw new DeviceException($"Could not establish raw REPL: {finalData}");
        }

        this.logger.LogDebug("Final raw REPL mode confirmed (full prompt: {FullPrompt})", finalData.Contains("raw REPL; CTRL-B to exit"));

        lock (this.stateLock) {
            this.inRawRepl = true;
            this.atPrompt = true; // We're now at a prompt after entering raw REPL
        }

        this.logger.LogDebug("Successfully entered raw REPL mode");
    }

    /// <summary>
    /// Exits raw REPL mode to friendly REPL.
    /// Based on official mpremote exit_raw_repl implementation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task ExitRawReplAsync(CancellationToken cancellationToken = default) {
        lock (this.stateLock) {
            if (!this.inRawRepl) {
                return;
            }
        }

        this.logger.LogDebug("Exiting raw REPL mode");

        // Send Ctrl-B to enter friendly REPL
        await this.stream.WriteAsync(new byte[] { (byte)'\r', CTRLB }, cancellationToken);
        await this.stream.FlushAsync(cancellationToken);

        lock (this.stateLock) {
            this.inRawRepl = false;
            this.atPrompt = false; // No longer at a raw REPL prompt
        }

        this.logger.LogDebug("Successfully exited raw REPL mode");
    }

    /// <summary>
    /// Executes Python code and returns the result.
    /// Based on official mpremote exec_raw implementation.
    /// </summary>
    /// <param name="command">The Python code to execute.</param>
    /// <param name="timeoutSeconds">Timeout in seconds for the operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The execution result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when concurrent operations are detected or object is disposed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when command is null.</exception>
    public async Task<RawReplResult> ExecuteAsync(string command, int timeoutSeconds = 10, CancellationToken cancellationToken = default) {
        if (command == null) {
            throw new ArgumentNullException(nameof(command));
        }

        if (this.disposed) {
            throw new ObjectDisposedException(nameof(SimpleRawRepl));
        }

        // Detect concurrent usage - this class is not thread-safe
        lock (this.stateLock) {
            if (this.operationInProgress) {
                throw new InvalidOperationException(
                    "SimpleRawRepl does not support concurrent operations. " +
                    "Each DeviceConnection should use its own SimpleRawRepl instance for sequential operations only.");
            }

            this.operationInProgress = true;
        }

        try {
            bool needsEnterRawRepl;
            lock (this.stateLock) {
                needsEnterRawRepl = !this.inRawRepl;
            }

            if (needsEnterRawRepl) {
                await this.EnterRawReplAsync(true, timeoutSeconds, cancellationToken);
            }

            this.logger.LogDebug("Executing command: {Command}", command);

            var commandBytes = Encoding.UTF8.GetBytes(command);
            bool usedRawPaste = false;

            // Only check for prompt if we're not already at one
            bool needsPromptCheck;
            lock (this.stateLock) {
                needsPromptCheck = !this.atPrompt;
            }

            if (needsPromptCheck) {
                this.logger.LogDebug("Not at prompt, checking device state");

                // Add small delay to ensure device is ready
                await Task.Delay(50, cancellationToken);

                // Generate a fresh prompt by sending newline
                await this.stream.WriteAsync(new byte[] { (byte)'\r' }, cancellationToken);
                await this.stream.FlushAsync(cancellationToken);
                await Task.Delay(100, cancellationToken);

                // Check we have a prompt
                var promptData = await this.ReadUntilAsync(">", 5, "prompt", cancellationToken);
                this.logger.LogDebug(
                    "Prompt check response: '{Data}' (length: {Length})",
                    promptData.Replace("\r", "\\r").Replace("\n", "\\n"), promptData.Length);

                if (!promptData.EndsWith('>')) {
                    this.logger.LogWarning("No prompt found, attempting to re-enter raw REPL mode");

                    // Try to re-enter raw REPL mode
                    lock (this.stateLock) {
                        this.inRawRepl = false;
                        this.atPrompt = false;
                    }

                    await this.EnterRawReplAsync(false, timeoutSeconds, cancellationToken);

                    // After re-entering, we should be at a prompt
                    lock (this.stateLock) {
                        if (!this.atPrompt) {
                            throw new DeviceException("Failed to establish raw REPL prompt after re-entry");
                        }
                    }
                }
                else {
                    lock (this.stateLock) {
                        this.atPrompt = true;
                    }
                }
            }
            else {
                this.logger.LogDebug("Already at prompt, proceeding with execution");
            }

            // Try raw-paste mode first if enabled
            if (this.useRawPaste) {
                try {
                    await this.ExecuteWithRawPasteAsync(commandBytes, cancellationToken);
                    usedRawPaste = true;
                    return await this.ReadExecutionResultAsync(timeoutSeconds, usedRawPaste, cancellationToken);
                }
                catch (DeviceException ex) when (ex.Message.Contains("Raw-paste mode not supported")) {
                    this.logger.LogDebug("Raw-paste mode not supported, falling back to standard raw REPL");
                    this.useRawPaste = false;

                    // Need to get back to a clean prompt state before trying standard raw REPL
                    await this.ExitRawReplAsync(cancellationToken);
                    await this.EnterRawReplAsync(false, timeoutSeconds, cancellationToken);

                    // Re-check for prompt after re-entering
                    var promptData2 = await this.ReadUntilAsync(">", 2, "prompt", cancellationToken);
                    if (!promptData2.EndsWith('>')) {
                        throw new DeviceException($"No raw REPL prompt found after fallback: {promptData2}");
                    }
                }
            }

            // Standard raw REPL execution
            await this.ExecuteWithStandardRawReplAsync(commandBytes, cancellationToken);
            return await this.ReadExecutionResultAsync(timeoutSeconds, usedRawPaste, cancellationToken);
        }
        finally {
            lock (this.stateLock) {
                this.operationInProgress = false;
            }
        }
    }

    private async Task ExecuteWithRawPasteAsync(byte[] commandBytes, CancellationToken cancellationToken) {
        // Execution will consume the prompt
        lock (this.stateLock) {
            this.atPrompt = false;
        }

        // Try to enter raw-paste mode
        await this.stream.WriteAsync(RAWPASTEINIT, cancellationToken);
        await this.stream.FlushAsync(cancellationToken);

        // Read response to check if raw-paste is supported
        var response = new byte[2];
        await this.stream.ReadExactlyAsync(response, cancellationToken);

        if (response[0] == (byte)'R' && response[1] == 0x00) {
            // Device understood raw-paste command but doesn't support it
            throw new DeviceException("Raw-paste mode not supported");
        }
        else if (response[0] == (byte)'R' && response[1] == 0x01) {
            // Device supports raw-paste mode
            await this.RawPasteWriteAsync(commandBytes, cancellationToken);
        }
        else {
            // Device doesn't understand raw-paste, disable it and fall back
            this.logger.LogDebug("Device does not support raw-paste mode, response: {Response:X2}{Response2:X2}", response[0], response[1]);
            throw new DeviceException("Raw-paste mode not supported");
        }
    }

    private async Task RawPasteWriteAsync(byte[] commandBytes, CancellationToken cancellationToken) {
        // Read initial header with window size
        var windowSizeBytes = new byte[2];
        await this.stream.ReadExactlyAsync(windowSizeBytes, cancellationToken);
        var windowSize = (int)BitConverter.ToUInt16(windowSizeBytes, 0);
        var windowRemain = windowSize;

        this.logger.LogDebug("Raw-paste mode window size: {WindowSize}", windowSize);

        // Write out the command bytes with flow control
        int i = 0;
        while (i < commandBytes.Length) {
            // Handle flow control
            while (windowRemain == 0) {
                var flowControlByte = new byte[1];
                await this.stream.ReadExactlyAsync(flowControlByte, cancellationToken);

                if (flowControlByte[0] == 0x01) {
                    // Device indicated that a new window of data can be sent
                    windowRemain += windowSize;
                }
                else if (flowControlByte[0] == 0x04) {
                    // Device indicated abrupt end. Acknowledge it and finish.
                    await this.stream.WriteAsync(new byte[] { CTRLD }, cancellationToken);
                    return;
                }
                else {
                    throw new DeviceException($"Unexpected raw-paste flow control byte: 0x{flowControlByte[0]:X2}");
                }
            }

            // Send out as much data as possible within the allowed window
            var chunkSize = Math.Min(windowRemain, commandBytes.Length - i);
            await this.stream.WriteAsync(commandBytes.AsMemory(i, chunkSize), cancellationToken);
            windowRemain -= chunkSize;
            i += chunkSize;
        }

        // Indicate end of data
        await this.stream.WriteAsync(new byte[] { CTRLD }, cancellationToken);

        // Wait for device to acknowledge end of data
        var ackData = await this.ReadUntilAsync("\x04", 2, "file_transfer", cancellationToken);
        if (!ackData.EndsWith('\u0004')) {
            throw new DeviceException($"Could not complete raw paste: {ackData}");
        }

        this.logger.LogDebug("Raw-paste write completed, device acknowledged with EOF");

        // Small delay to allow device to start executing before we start reading results
        // The device needs time to compile and start executing the code
        await Task.Delay(50, cancellationToken);
    }

    private async Task ExecuteWithStandardRawReplAsync(byte[] commandBytes, CancellationToken cancellationToken) {
        // Execution will consume the prompt
        lock (this.stateLock) {
            this.atPrompt = false;
        }

        // Write command using standard raw REPL, 256 bytes every 10ms
        for (int i = 0; i < commandBytes.Length; i += 256) {
            var chunkSize = Math.Min(256, commandBytes.Length - i);
            await this.stream.WriteAsync(commandBytes.AsMemory(i, chunkSize), cancellationToken);
            await Task.Delay(10, cancellationToken); // 10ms delay as per official implementation
        }

        // Send Ctrl-D to execute
        await this.stream.WriteAsync(new byte[] { CTRLD }, cancellationToken);

        // Check if we could exec command
        var response = new byte[2];
        await this.stream.ReadExactlyAsync(response, cancellationToken);
        if (response[0] != (byte)'O' || response[1] != (byte)'K') {
            throw new DeviceException($"Could not exec command (response: {response[0]:X2}{response[1]:X2})");
        }
    }

    private async Task<RawReplResult> ReadExecutionResultAsync(int timeoutSeconds, bool wasRawPaste, CancellationToken cancellationToken) {
        // IMPORTANT: Both raw-paste and standard raw REPL use the SAME result reading pattern!
        // This follows the official mpremote implementation where raw_paste_write returns early
        // and then follow() method is called which uses standard EOF pattern for both modes.
        this.logger.LogDebug("Reading execution result (wasRawPaste: {WasRawPaste})", wasRawPaste);

        // Read normal output until first EOF (follows official mpremote follow() method)
        this.logger.LogDebug("Reading normal output until first EOF...");
        var normalOutput = await this.ReadUntilAsync("\x04", timeoutSeconds, "execution", cancellationToken);
        this.logger.LogDebug("Normal output: '{Output}' (ends with EOF: {EndsWithEOF})", normalOutput, normalOutput.EndsWith('\u0004'));

        if (!normalOutput.EndsWith('\u0004')) {
            throw new DeviceException("Timeout waiting for first EOF reception");
        }

        normalOutput = normalOutput[..^1]; // Remove EOF

        // Read error output until second EOF
        this.logger.LogDebug("Reading error output until second EOF...");
        var errorOutput = await this.ReadUntilAsync("\x04", timeoutSeconds, "execution", cancellationToken);
        this.logger.LogDebug("Error output: '{Output}' (ends with EOF: {EndsWithEOF})", errorOutput, errorOutput.EndsWith('\u0004'));

        if (!errorOutput.EndsWith('\u0004')) {
            throw new DeviceException("Timeout waiting for second EOF reception");
        }

        errorOutput = errorOutput[..^1]; // Remove EOF

        // After execution, we should get a new prompt (">")
        this.logger.LogDebug("Waiting for prompt after execution...");
        var promptAfterExec = await this.ReadUntilAsync(">", timeoutSeconds, "prompt", cancellationToken);
        lock (this.stateLock) {
            if (promptAfterExec.EndsWith('>')) {
                this.atPrompt = true;
                this.logger.LogDebug("Received prompt after execution");
            }
            else {
                this.logger.LogWarning("No prompt received after execution: '{Data}'", promptAfterExec);
                this.atPrompt = false;
            }
        }

        // Use enhanced error detection and classification
        var enhancedResult = ExecutionErrorParser.ParseExecutionResult(
            normalOutput, errorOutput, null, this.logger);

        this.logger.LogDebug(
            "Execution result - Type: {ErrorType}, Success: {Success}, Output: '{Output}', Error: '{Error}'",
            enhancedResult.ErrorType, enhancedResult.IsSuccess,
            enhancedResult.Output, enhancedResult.ErrorOutput);

        if (enhancedResult.ErrorType != ExecutionErrorType.None) {
            this.logger.LogWarning(
                "Execution error classified as {ErrorType}: {DiagnosticInfo}. Suggested action: {SuggestedAction}",
                enhancedResult.ErrorType, enhancedResult.DiagnosticInfo, enhancedResult.SuggestedAction);
        }

        return new RawReplResult {
            IsSuccess = enhancedResult.IsSuccess,
            Output = enhancedResult.Output,
            ErrorOutput = enhancedResult.ErrorOutput,
            Exception = enhancedResult.IsSuccess ? null :
                new DeviceException($"Execution error ({enhancedResult.ErrorType}): {enhancedResult.DiagnosticInfo ?? enhancedResult.ErrorOutput}"),
            ErrorType = enhancedResult.ErrorType,
            DiagnosticInfo = enhancedResult.DiagnosticInfo,
            SuggestedAction = enhancedResult.SuggestedAction,
            IsRecoverable = enhancedResult.IsRecoverable,
        };
    }

    private async Task<string> ReadUntilAsync(string ending, int timeoutSeconds, string operationType, CancellationToken cancellationToken) {
        var data = new StringBuilder();
        var buffer = new byte[1];
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        // Adaptive timeout based on operation type and data received
        var adaptiveTimeout = this.GetAdaptiveTimeout(operationType, timeoutSeconds);
        var actualTimeout = adaptiveTimeout ?? timeout;

        this.logger.LogTrace(
            "Starting ReadUntilAsync for '{Ending}' with {Timeout}s timeout (operation: {Operation})",
            ending.Replace("\r", "\\r").Replace("\n", "\\n"), actualTimeout.TotalSeconds, operationType);

        while (DateTime.UtcNow - startTime < actualTimeout) {
            cancellationToken.ThrowIfCancellationRequested();

            try {
                // Use adaptive read timeout based on operation type
                var readTimeoutMs = this.GetReadTimeoutForOperation(operationType);
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readCts.CancelAfter(TimeSpan.FromMilliseconds(readTimeoutMs));

                await this.stream.ReadExactlyAsync(buffer, readCts.Token);
                data.Append(Encoding.UTF8.GetString(buffer));

                // Check if we've received the ending
                var currentData = data.ToString();
                if (currentData.EndsWith(ending)) {
                    this.logger.LogTrace(
                        "Successfully read until '{Ending}' in {Duration}ms",
                        ending.Replace("\r", "\\r").Replace("\n", "\\n"),
                        (DateTime.UtcNow - startTime).TotalMilliseconds);
                    return currentData;
                }

                // For prompt detection, be more aggressive about early detection
                if (operationType == "prompt" && currentData.TrimEnd().EndsWith('>')) {
                    this.logger.LogTrace("Early prompt detection for operation: {Operation}", operationType);
                    return currentData;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            }
            catch (OperationCanceledException) {
                // Short timeout expired, continue reading with adaptive delay
                var delayMs = this.GetRetryDelayForOperation(operationType);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        this.logger.LogWarning(
            "ReadUntilAsync timed out after {Timeout}s waiting for '{Ending}' (operation: {Operation}, received: {ReceivedLength} chars)",
            actualTimeout.TotalSeconds, ending.Replace("\r", "\\r").Replace("\n", "\\n"), operationType, data.Length);

        return data.ToString();
    }

    private TimeSpan? GetAdaptiveTimeout(string operationType, int baseTimeoutSeconds) {
        return operationType switch {
            "prompt" => TimeSpan.FromSeconds(Math.Min(baseTimeoutSeconds, 2)), // Quick prompt detection
            "execution" => TimeSpan.FromSeconds(Math.Max(baseTimeoutSeconds, 10)), // Allow more time for execution
            "file_transfer" => TimeSpan.FromSeconds(Math.Max(baseTimeoutSeconds, 30)), // Extended for large files
            "soft_reboot" => TimeSpan.FromSeconds(Math.Max(baseTimeoutSeconds, 15)), // Device restart takes time
            _ => null, // Use default timeout
        };
    }

    private int GetReadTimeoutForOperation(string operationType) {
        return operationType switch {
            "prompt" => 50,        // Very fast for prompt detection
            "execution" => 100,    // Standard for execution
            "file_transfer" => 200, // Allow more time for file operations
            "soft_reboot" => 500,  // Device restart can be slow
            _ => 100, // Default timeout
        };
    }

    private int GetRetryDelayForOperation(string operationType) {
        return operationType switch {
            "prompt" => 5,         // Quick retry for prompts
            "execution" => 10,     // Standard retry
            "file_transfer" => 20, // Longer delay for file operations
            "soft_reboot" => 50,   // Much longer for device restart
            _ => 10, // Default delay
        };
    }

    private async Task DrainInputAsync(CancellationToken cancellationToken) {
        var buffer = new byte[1024];

        try {
            using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            drainCts.CancelAfter(TimeSpan.FromMilliseconds(100));

            while (!drainCts.Token.IsCancellationRequested) {
                await this.stream.ReadAsync(buffer, drainCts.Token);
            }
        }
        catch (OperationCanceledException) {
            // Normal timeout - drain complete
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    /// <remarks>
    /// This method uses a timeout to prevent hanging during disposal if the device is unresponsive.
    /// Any pending operations will be cancelled and the raw REPL state will be reset safely.
    /// </remarks>
    public void Dispose() {
        if (this.disposed) {
            return;
        }

        try {
            // Use timeout-protected cleanup to prevent hanging indefinitely
            this.DisposeWithTimeout();
        }
        catch (Exception ex) {
            // Log disposal errors for diagnostics but don't throw
            this.logger.LogWarning(ex, "Error during SimpleRawRepl disposal - cleanup may be incomplete");
        }
        finally {
            this.disposed = true;
            lock (this.stateLock) {
                this.operationInProgress = false; // Reset operation flag
            }

            // Don't dispose stream - caller owns it
        }
    }

    private void DisposeWithTimeout() {
        bool shouldCleanup;
        lock (this.stateLock) {
            shouldCleanup = this.inRawRepl && this.stream.CanWrite;
        }

        if (!shouldCleanup) {
            return;
        }

        // Use a reasonable timeout for cleanup operations to prevent indefinite hanging
        using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        try {
            // Attempt graceful exit from raw REPL mode
            var exitTask = this.ExitRawReplGracefullyAsync(cleanupCts.Token);

            // Wait for completion with timeout protection
            exitTask.Wait(cleanupCts.Token);

            this.logger.LogDebug("Successfully exited raw REPL during disposal");
        }
        catch (OperationCanceledException) {
            this.logger.LogWarning("Raw REPL exit timed out during disposal - forcing synchronous cleanup");

            // Fallback to immediate synchronous cleanup if async approach times out
            this.ForceImmediateCleanup();
        }
        catch (Exception ex) {
            this.logger.LogWarning(ex, "Graceful raw REPL exit failed during disposal - attempting immediate cleanup");

            // Fallback to immediate cleanup if graceful exit fails
            this.ForceImmediateCleanup();
        }
    }

    private async Task ExitRawReplGracefullyAsync(CancellationToken cancellationToken) {
        // Send Ctrl-B to exit raw REPL mode
        await this.stream.WriteAsync(new byte[] { CTRLB }, cancellationToken);
        await this.stream.FlushAsync(cancellationToken);

        // Small delay to allow device to process the exit command
        await Task.Delay(50, cancellationToken);

        lock (this.stateLock) {
            this.inRawRepl = false;
            this.atPrompt = false;
        }
    }

    private void ForceImmediateCleanup() {
        try {
            // Immediate synchronous cleanup as last resort
            if (this.stream.CanWrite) {
                this.stream.WriteByte(CTRLB);
                this.stream.Flush();
            }
        }
        catch {
            // Ignore any errors during forced cleanup
        }
        finally {
            lock (this.stateLock) {
                this.inRawRepl = false;
                this.atPrompt = false;
            }
        }
    }
}

/// <summary>
/// Result from Raw REPL code execution with enhanced error classification.
/// </summary>
public class RawReplResult {
    /// <summary>
    /// Gets or sets a value indicating whether the execution was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the normal output from execution.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error output from execution.
    /// </summary>
    public string ErrorOutput { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exception if execution failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the type of error that occurred during execution.
    /// </summary>
    public ExecutionErrorType ErrorType { get; set; } = ExecutionErrorType.None;

    /// <summary>
    /// Gets or sets diagnostic information about the error.
    /// </summary>
    public string? DiagnosticInfo { get; set; }

    /// <summary>
    /// Gets or sets suggested actions for resolving the error.
    /// </summary>
    public string? SuggestedAction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the error is recoverable.
    /// </summary>
    public bool IsRecoverable { get; set; } = true;
}
