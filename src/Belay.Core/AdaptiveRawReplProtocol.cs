// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Belay.Core;

/// <summary>
/// Adaptive implementation of MicroPython Raw REPL protocol that auto-detects device capabilities
/// and adjusts protocol parameters for optimal compatibility and performance.
/// Restored from working tagged release with full sophisticated functionality.
/// </summary>
public class AdaptiveRawReplProtocol : IDisposable
{
    private readonly Stream stream;
    private readonly ILogger<AdaptiveRawReplProtocol> logger;
    private readonly SemaphoreSlim protocolSemaphore;
    private readonly RawReplConfiguration configuration;
    private readonly ReplProtocolMetrics metrics;

    private bool disposed;
    private DeviceReplCapabilities? detectedCapabilities;
    private RawReplState currentState;
    private TimeSpan adaptiveResponseTimeout;
    private TimeSpan adaptiveStartupDelay;

    // Control characters for Raw REPL protocol
    private const byte CTRLA = 0x01; // Enter raw REPL
    private const byte CTRLB = 0x02; // Exit raw REPL
    private const byte CTRLC = 0x03; // KeyboardInterrupt
    private const byte CTRLD = 0x04; // Execute/End data
    private const byte CTRLE = 0x05; // Raw-paste mode prefix

    // Raw-paste mode initialization sequence
    private static readonly byte[] RAWPASTEINIT = [CTRLE, (byte)'A', CTRLA];

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveRawReplProtocol"/> class.
    /// </summary>
    /// <param name="stream">The communication stream to the device.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    /// <param name="configuration">Protocol configuration options.</param>
    public AdaptiveRawReplProtocol(Stream stream, ILogger<AdaptiveRawReplProtocol> logger, RawReplConfiguration? configuration = null)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.configuration = configuration ?? new RawReplConfiguration();
        this.metrics = new ReplProtocolMetrics();
        this.protocolSemaphore = new SemaphoreSlim(1, 1);

        this.currentState = RawReplState.Normal;
        this.adaptiveResponseTimeout = this.configuration.BaseResponseTimeout;
        this.adaptiveStartupDelay = this.configuration.StartupDelay;
    }

    /// <summary>
    /// Gets the current state of the protocol.
    /// </summary>
    public RawReplState CurrentState => this.currentState;

    /// <summary>
    /// Gets the detected device capabilities, if available.
    /// </summary>
    public DeviceReplCapabilities? DetectedCapabilities => this.detectedCapabilities;

    /// <summary>
    /// Gets the current protocol metrics.
    /// </summary>
    public ReplProtocolMetrics Metrics => this.metrics;

    /// <summary>
    /// Gets the current configuration being used.
    /// </summary>
    public RawReplConfiguration Configuration => this.configuration;

    /// <summary>
    /// Initialize the adaptive Raw REPL protocol with capability detection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await this.protocolSemaphore.WaitAsync(cancellationToken);

        try
        {
            this.logger.LogInformation("Initializing adaptive Raw REPL protocol");

            // Perform device capability detection
            await this.DetectDeviceCapabilitiesAsync(cancellationToken);

            // Apply detected capabilities to configuration
            this.ApplyCapabilitiesToConfiguration();

            this.logger.LogInformation("Adaptive Raw REPL protocol initialized successfully. Capabilities: {@Capabilities}", this.detectedCapabilities);
        }
        finally
        {
            this.protocolSemaphore.Release();
        }
    }

    /// <summary>
    /// Execute code using the adaptive Raw REPL protocol with automatic optimization.
    /// </summary>
    /// <param name="code">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The result of code execution.</returns>
    public async Task<RawReplResponse> ExecuteCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(AdaptiveRawReplProtocol));
        }

        await this.protocolSemaphore.WaitAsync(cancellationToken);

        try
        {
            var stopwatch = Stopwatch.StartNew();

            if (this.configuration.EnableVerboseLogging)
            {
                this.logger.LogDebug("Executing code with adaptive protocol: {Code}", code);
            }

            RawReplResponse response = await this.ExecuteWithRetryAsync(code, cancellationToken);

            stopwatch.Stop();
            this.RecordOperationMetrics(response.IsSuccess, stopwatch.Elapsed);

            return response;
        }
        finally
        {
            this.protocolSemaphore.Release();
        }
    }

    private async Task DetectDeviceCapabilitiesAsync(CancellationToken cancellationToken)
    {
        this.logger.LogDebug("Starting device capability detection");

        this.detectedCapabilities = new DeviceReplCapabilities();

        // Step 1: Initialize basic REPL connection
        await this.InitializeBasicReplAsync(cancellationToken);

        // Step 2: Detect device platform and version
        await this.DetectDeviceInfoAsync(cancellationToken);

        // Step 3: Test raw-paste mode support
        await this.DetectRawPasteModeCapabilitiesAsync(cancellationToken);

        // Step 4: Measure response time characteristics
        await this.MeasureResponseTimingAsync(cancellationToken);

        // Step 5: Test flow control reliability
        await this.TestFlowControlReliabilityAsync(cancellationToken);

        this.logger.LogInformation("Device capability detection completed: {@Capabilities}", this.detectedCapabilities);
    }

    /// <summary>
    /// Recovers device from stuck raw/paste modes that may be left from previous sessions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    private async Task RecoverFromStuckModesAsync(CancellationToken cancellationToken)
    {
        this.logger.LogDebug("Attempting device recovery from stuck raw/paste modes");

        try
        {
            // Strategy: Send multiple exit sequences to handle various stuck states
            
            // 1. Exit raw-paste mode if stuck (Ctrl-C, Ctrl-D)
            await this.stream.WriteAsync(new byte[] { CTRLC, CTRLD }, cancellationToken);
            await this.stream.FlushAsync(cancellationToken);
            await Task.Delay(100, cancellationToken);

            // 2. Exit raw mode if stuck (Ctrl-B)
            await this.stream.WriteAsync(new byte[] { CTRLB }, cancellationToken);
            await this.stream.FlushAsync(cancellationToken);
            await Task.Delay(100, cancellationToken);

            // 3. Send additional interrupt to ensure clean state
            await this.stream.WriteAsync(new byte[] { CTRLC }, cancellationToken);
            await this.stream.FlushAsync(cancellationToken);
            await Task.Delay(100, cancellationToken);

            // 4. Send newline to trigger prompt response
            await this.stream.WriteAsync(new byte[] { 0x0D, 0x0A }, cancellationToken);
            await this.stream.FlushAsync(cancellationToken);

            // 5. Drain any pending output from recovery attempts
            await this.DrainAvailableOutputAsync(cancellationToken);

            this.logger.LogDebug("Device recovery completed successfully");
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "Device recovery failed, continuing with normal initialization");
        }
    }

    private async Task InitializeBasicReplAsync(CancellationToken cancellationToken)
    {
        this.logger.LogDebug("Initializing basic REPL connection with device recovery");

        // DEVICE RECOVERY: Handle devices stuck in raw/paste modes
        await this.RecoverFromStuckModesAsync(cancellationToken);

        // Adaptive startup delay
        var startupDelay = this.adaptiveStartupDelay;
        var maxAttempts = 3;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                this.logger.LogDebug(
                    "Startup attempt {Attempt}/{MaxAttempts} with delay {Delay}ms",
                    attempt, maxAttempts, startupDelay.TotalMilliseconds);

                await Task.Delay(startupDelay, cancellationToken);
                await this.DrainAvailableOutputAsync(cancellationToken);

                // Send interrupt sequence with adaptive delay
                await this.stream.WriteAsync(new byte[] { 0x0D, CTRLC }, cancellationToken);
                await this.stream.FlushAsync(cancellationToken);

                await Task.Delay(this.configuration.InterruptDelay, cancellationToken);
                await this.DrainAvailableOutputAsync(cancellationToken);

                this.currentState = RawReplState.Normal;
                this.logger.LogDebug("Basic REPL initialized successfully on attempt {Attempt}", attempt);

                // Record if extended startup was needed
                if (attempt > 1 || startupDelay > this.configuration.StartupDelay)
                {
                    this.detectedCapabilities!.RequiresExtendedStartup = true;
                    this.adaptiveStartupDelay = startupDelay;
                }

                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                this.logger.LogWarning("Startup attempt {Attempt} failed: {Error}. Increasing delays.", attempt, ex.Message);

                // Increase delays for next attempt
                startupDelay = TimeSpan.FromMilliseconds(Math.Min(startupDelay.TotalMilliseconds * 1.5, this.configuration.MaxStartupDelay.TotalMilliseconds));
                this.configuration.InterruptDelay = TimeSpan.FromMilliseconds(Math.Min(this.configuration.InterruptDelay.TotalMilliseconds * 1.5, 1000));

                this.detectedCapabilities!.RequiresExtendedStartup = true;
                this.detectedCapabilities.RequiresExtendedInterruptDelay = true;
            }
        }

        throw new DeviceException("Failed to initialize basic REPL after all attempts");
    }

    private async Task DetectDeviceInfoAsync(CancellationToken cancellationToken)
    {
        if (!this.configuration.EnableRawPasteAutoDetection)
        {
            this.logger.LogDebug("Device info detection skipped (auto-detection disabled)");
            return;
        }

        this.logger.LogDebug("Detecting device platform and version");

        try
        {
            // Try to get device info using simple commands
            var platformResponse = await this.ExecuteSimpleCommandAsync("import sys; sys.platform", cancellationToken);
            if (platformResponse.IsSuccess)
            {
                this.detectedCapabilities!.DetectedPlatform = platformResponse.Result?.Trim().Trim('"');
                this.logger.LogDebug("Detected platform: {Platform}", this.detectedCapabilities.DetectedPlatform);
            }

            var versionResponse = await this.ExecuteSimpleCommandAsync("import sys; sys.version", cancellationToken);
            if (versionResponse.IsSuccess)
            {
                this.detectedCapabilities!.MicroPythonVersion = versionResponse.Result?.Split('\n').FirstOrDefault()?.Trim();
                this.logger.LogDebug("Detected version: {Version}", this.detectedCapabilities.MicroPythonVersion);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning("Device info detection failed: {Error}", ex.Message);
        }
    }

    private async Task DetectRawPasteModeCapabilitiesAsync(CancellationToken cancellationToken)
    {
        if (!this.configuration.EnableRawPasteAutoDetection)
        {
            this.logger.LogDebug("Raw-paste mode detection skipped (auto-detection disabled)");
            return;
        }

        this.logger.LogDebug("Testing raw-paste mode capabilities");

        try
        {
            await this.EnterRawModeAsync(cancellationToken);

            try
            {
                // Try to enter raw-paste mode
                await this.stream.WriteAsync(RAWPASTEINIT, cancellationToken);
                await this.stream.FlushAsync(cancellationToken);

                // Try to read the response with a short timeout
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(1000));

                var response = await this.ReadLineAsync(timeoutCts.Token);

                if (response.StartsWith('R'))
                {
                    this.detectedCapabilities!.SupportsRawPasteMode = true;
                    this.logger.LogDebug("Raw-paste mode is supported");

                    // Read window size increment
                    byte[] windowSizeBytes = new byte[2];
                    await this.stream.ReadExactlyAsync(windowSizeBytes, timeoutCts.Token);
                    ushort windowSizeIncrement = BitConverter.ToUInt16(windowSizeBytes, 0);

                    this.detectedCapabilities.PreferredWindowSize = windowSizeIncrement;
                    this.detectedCapabilities.MaxWindowSize = Math.Max(windowSizeIncrement, this.configuration.MaximumWindowSize);

                    this.logger.LogDebug("Detected window size increment: {WindowSize}", windowSizeIncrement);

                    // Exit raw-paste mode cleanly
                    await this.WriteByteAsync(CTRLD, cancellationToken);
                    await this.ReadUntilPromptAsync(cancellationToken);
                }
                else
                {
                    this.detectedCapabilities!.SupportsRawPasteMode = false;
                    this.logger.LogDebug("Raw-paste mode is not supported");
                }
            }
            finally
            {
                await this.ExitRawModeAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning("Raw-paste mode detection failed: {Error}", ex.Message);
            this.detectedCapabilities!.SupportsRawPasteMode = false;
        }
    }

    private async Task MeasureResponseTimingAsync(CancellationToken cancellationToken)
    {
        this.logger.LogDebug("Measuring device response timing");

        var timings = new List<TimeSpan>();
        const int testCount = 3;

        for (int i = 0; i < testCount; i++)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var response = await this.ExecuteSimpleCommandAsync("1+1", cancellationToken);
                stopwatch.Stop();

                if (response.IsSuccess)
                {
                    timings.Add(stopwatch.Elapsed);
                    this.logger.LogDebug("Response timing test {Test}: {Duration}ms", i + 1, stopwatch.Elapsed.TotalMilliseconds);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning("Response timing test {Test} failed: {Error}", i + 1, ex.Message);
            }
        }

        if (timings.Count > 0)
        {
            var averageTime = TimeSpan.FromMilliseconds(timings.Average(t => t.TotalMilliseconds));
            this.detectedCapabilities!.AverageResponseTime = averageTime;

            // Adjust adaptive timeout based on measured performance
            var suggestedTimeout = TimeSpan.FromMilliseconds(averageTime.TotalMilliseconds * 5); // 5x average for safety
            if (suggestedTimeout > this.adaptiveResponseTimeout)
            {
                this.adaptiveResponseTimeout = TimeSpan.FromMilliseconds(Math.Min(suggestedTimeout.TotalMilliseconds, this.configuration.MaxResponseTimeout.TotalMilliseconds));
                this.logger.LogDebug("Adjusted response timeout to {Timeout}ms based on measured performance", this.adaptiveResponseTimeout.TotalMilliseconds);
            }
        }
    }

    private async Task TestFlowControlReliabilityAsync(CancellationToken cancellationToken)
    {
        if (!this.detectedCapabilities!.SupportsRawPasteMode)
        {
            this.logger.LogDebug("Skipping flow control test (raw-paste mode not supported)");
            return;
        }

        this.logger.LogDebug("Testing flow control reliability");

        try
        {
            // Test with a moderately-sized code block
            const string testCode = @"
# Flow control reliability test
for i in range(10):
    print(f'Test iteration {i}')
    if i % 3 == 0:
        continue
    else:
        result = i * 2
        print(f'Result: {result}')
print('Flow control test completed')
";

            var response = await this.ExecuteWithRawPasteModeAsync(testCode, cancellationToken);

            if (response.IsSuccess && response.Result?.Contains("Flow control test completed") == true)
            {
                this.detectedCapabilities.HasReliableFlowControl = true;
                this.detectedCapabilities.SupportsLargeCodeTransfers = true;
                this.logger.LogDebug("Flow control test passed");
            }
            else
            {
                this.detectedCapabilities.HasReliableFlowControl = false;
                this.logger.LogWarning("Flow control test failed or incomplete");
            }
        }
        catch (Exception ex)
        {
            this.logger.LogWarning("Flow control reliability test failed: {Error}", ex.Message);
            this.detectedCapabilities.HasReliableFlowControl = false;
        }
    }

    private void ApplyCapabilitiesToConfiguration()
    {
        if (this.detectedCapabilities == null)
        {
            return;
        }

        this.logger.LogDebug("Applying detected capabilities to configuration");

        // Update timeouts based on device performance
        if (this.detectedCapabilities.AverageResponseTime > TimeSpan.Zero)
        {
            this.adaptiveResponseTimeout = TimeSpan.FromMilliseconds(
                Math.Max(
                    this.adaptiveResponseTimeout.TotalMilliseconds,
                    this.detectedCapabilities.AverageResponseTime.TotalMilliseconds * 3));
        }

        // Update window size preferences
        if (this.detectedCapabilities.SupportsRawPasteMode && this.configuration.PreferredWindowSize == null)
        {
            this.configuration.PreferredWindowSize = this.detectedCapabilities.PreferredWindowSize;
        }

        // Disable raw-paste mode if not supported or unreliable
        if (!this.detectedCapabilities.SupportsRawPasteMode || !this.detectedCapabilities.HasReliableFlowControl)
        {
            this.configuration.EnableRawPasteAutoDetection = false;
            this.logger.LogDebug("Disabled raw-paste mode based on capability detection");
        }

        this.logger.LogDebug(
            "Configuration applied: Timeout={Timeout}ms, WindowSize={WindowSize}, RawPaste={RawPaste}",
            this.adaptiveResponseTimeout.TotalMilliseconds,
            this.configuration.PreferredWindowSize,
            this.configuration.EnableRawPasteAutoDetection);
    }

    private async Task<RawReplResponse> ExecuteWithRetryAsync(string code, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= this.configuration.MaxRetryAttempts; attempt++)
        {
            try
            {
                if (this.configuration.EnableVerboseLogging)
                {
                    this.logger.LogDebug("Execute attempt {Attempt}/{MaxAttempts}", attempt, this.configuration.MaxRetryAttempts);
                }

                // Choose execution method based on capabilities
                RawReplResponse response;
                if (this.detectedCapabilities?.SupportsRawPasteMode == true && this.configuration.EnableRawPasteAutoDetection)
                {
                    response = await this.ExecuteWithRawPasteModeAsync(code, cancellationToken);
                }
                else
                {
                    response = await this.ExecuteWithRawModeAsync(code, cancellationToken);
                }

                if (response.IsSuccess)
                {
                    if (attempt > 1)
                    {
                        this.logger.LogDebug("Execute succeeded on retry attempt {Attempt}", attempt);
                    }

                    return response;
                }
                else if (attempt < this.configuration.MaxRetryAttempts)
                {
                    this.logger.LogWarning(
                        "Execute attempt {Attempt} failed, will retry: {Error}",
                        attempt, response.Exception?.Message);
                    lastException = response.Exception;
                }
            }
            catch (Exception ex) when (attempt < this.configuration.MaxRetryAttempts)
            {
                this.logger.LogWarning("Execute attempt {Attempt} threw exception, will retry: {Error}", attempt, ex.Message);
                lastException = ex;
                this.metrics.RetryAttempts++;
            }

            // Apply exponential backoff delay before retry
            if (attempt < this.configuration.MaxRetryAttempts)
            {
                var delay = TimeSpan.FromMilliseconds(this.configuration.RetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                await Task.Delay(delay, cancellationToken);
            }
        }

        // All retries failed
        var finalResponse = new RawReplResponse
        {
            IsSuccess = false,
            ErrorOutput = "All retry attempts failed",
            Exception = lastException ?? new Exception("Execute failed after all retry attempts"),
        };

        return finalResponse;
    }

    private async Task<RawReplResponse> ExecuteSimpleCommandAsync(string code, CancellationToken cancellationToken)
    {
        // Simple execution for capability detection - use basic raw mode
        await this.EnterRawModeAsync(cancellationToken);

        try
        {
            // Send the code
            byte[] codeBytes = Encoding.UTF8.GetBytes(code);
            await this.stream.WriteAsync(codeBytes, cancellationToken);
            await this.stream.FlushAsync(cancellationToken);

            // Send Ctrl-D to execute
            await this.WriteByteAsync(CTRLD, cancellationToken);

            // Read response with adaptive timeout
            string response = await this.ReadWithTimeoutAsync((int)this.adaptiveResponseTimeout.TotalMilliseconds, cancellationToken);

            if (!response.Contains("OK"))
            {
                throw new DeviceException($"Expected 'OK' response, got: {response}");
            }

            // Read execution output
            string output = await this.ReadWithTimeoutAsync((int)this.adaptiveResponseTimeout.TotalMilliseconds, cancellationToken);

            return ParseResponse(output);
        }
        finally
        {
            await this.ExitRawModeAsync(cancellationToken);
        }
    }

    private void RecordOperationMetrics(bool success, TimeSpan duration)
    {
        if (success)
        {
            this.metrics.SuccessfulOperations++;
        }
        else
        {
            this.metrics.FailedOperations++;
        }

        // Update average operation time with exponential moving average
        if (this.metrics.AverageOperationTime == TimeSpan.Zero)
        {
            this.metrics.AverageOperationTime = duration;
        }
        else
        {
            var alpha = 0.1; // Smoothing factor
            var newAverage = TimeSpan.FromMilliseconds(
                (alpha * duration.TotalMilliseconds) +
                ((1 - alpha) * this.metrics.AverageOperationTime.TotalMilliseconds));
            this.metrics.AverageOperationTime = newAverage;
        }

        this.metrics.LastOperationTime = DateTime.UtcNow;
    }

    // Implementation of core protocol methods with adaptive enhancements
    private async Task EnterRawModeAsync(CancellationToken cancellationToken)
    {
        if (this.currentState == RawReplState.Raw)
        {
            return;
        }

        this.logger.LogDebug("Entering raw REPL mode");

        // Send Ctrl-A to enter raw mode
        await this.stream.WriteAsync(new byte[] { CTRLA }, cancellationToken);
        await this.stream.FlushAsync(cancellationToken);

        // Read response with adaptive timeout
        string response = await this.ReadWithTimeoutAsync((int)this.adaptiveResponseTimeout.TotalMilliseconds, cancellationToken);
        this.logger.LogDebug("Raw REPL response: '{Response}' (length: {Length})", response, response.Length);

        if (!response.Contains("raw REPL"))
        {
            // Try waiting a bit longer for the complete response
            this.logger.LogDebug("Raw REPL not found in first read, trying additional read...");
            await Task.Delay(this.configuration.InterruptDelay, cancellationToken);
            string additional = await this.ReadWithTimeoutAsync(1000, cancellationToken);
            response += additional;
            this.logger.LogDebug("Combined response: '{Response}' (length: {Length})", response, response.Length);

            if (!response.Contains("raw REPL"))
            {
                throw new DeviceException($"Failed to enter raw REPL mode - response: {response}");
            }
        }

        this.currentState = RawReplState.Raw;
        this.logger.LogDebug("Successfully entered raw REPL mode");
    }

    private async Task ExitRawModeAsync(CancellationToken cancellationToken)
    {
        if (this.currentState == RawReplState.Normal)
        {
            return;
        }

        this.logger.LogDebug("Exiting raw REPL mode");

        // Send Ctrl-B to exit raw mode
        await this.WriteByteAsync(CTRLB, cancellationToken);

        // Wait for normal prompt ">>>"
        await this.ReadUntilPromptAsync(cancellationToken);

        this.currentState = RawReplState.Normal;
        this.logger.LogDebug("Successfully exited raw REPL mode");
    }

    private async Task<RawReplResponse> ExecuteWithRawModeAsync(string code, CancellationToken cancellationToken)
    {
        await this.EnterRawModeAsync(cancellationToken);

        try
        {
            this.logger.LogDebug("Executing code in Raw REPL mode: {Code}", code);

            // Preprocess code for Raw REPL compatibility
            string processedCode = PreprocessCodeForRawRepl(code);

            if (this.configuration.EnableVerboseLogging && processedCode != code)
            {
                this.logger.LogDebug("Code transformed for Raw REPL: {OriginalCode} -> {ProcessedCode}", code, processedCode);
            }

            // Send the processed code
            byte[] codeBytes = Encoding.UTF8.GetBytes(processedCode);
            await this.stream.WriteAsync(codeBytes, cancellationToken);
            await this.stream.FlushAsync(cancellationToken);

            // Send Ctrl-D to execute
            await this.WriteByteAsync(CTRLD, cancellationToken);

            // Wait for "OK" response with adaptive timeout
            string response = await this.ReadWithTimeoutAsync((int)(this.adaptiveResponseTimeout.TotalMilliseconds * 0.5), cancellationToken);

            if (!response.Contains("OK"))
            {
                throw new DeviceException($"Expected 'OK' response, got: {response}");
            }

            this.logger.LogDebug("Received OK confirmation from MicroPython");

            // Read execution output until we get back to raw REPL prompt
            string output = await this.ReadWithTimeoutAsync((int)this.adaptiveResponseTimeout.TotalMilliseconds, cancellationToken);

            return ParseResponse(output);
        }
        finally
        {
            await this.ExitRawModeAsync(cancellationToken);
        }
    }

    private async Task<RawReplResponse> ExecuteWithRawPasteModeAsync(string code, CancellationToken cancellationToken)
    {
        await this.EnterRawModeAsync(cancellationToken);

        try
        {
            // Enter raw-paste mode
            await this.stream.WriteAsync(RAWPASTEINIT, cancellationToken);
            await this.stream.FlushAsync(cancellationToken);

            // Read response to confirm raw-paste mode support
            string response = await this.ReadLineAsync(cancellationToken);
            if (!response.StartsWith('R'))
            {
                throw new DeviceException("Raw-paste mode not supported");
            }

            // Read window size increment (16-bit little-endian)
            byte[] windowSizeBytes = new byte[2];
            await this.stream.ReadExactlyAsync(windowSizeBytes, cancellationToken);
            ushort windowSizeIncrement = BitConverter.ToUInt16(windowSizeBytes, 0);

            // Use configured preferred window size if available
            int effectiveWindowSize = this.configuration.PreferredWindowSize ?? windowSizeIncrement;

            this.currentState = RawReplState.RawPaste;
            this.logger.LogDebug("Entered raw-paste mode with window size: {WindowSize}", effectiveWindowSize);

            // Send code with adaptive flow control
            await this.SendCodeWithAdaptiveFlowControlAsync(code, effectiveWindowSize, cancellationToken);

            // Send end-of-data marker
            await this.WriteByteAsync(CTRLD, cancellationToken);

            // Read execution output
            string output = await this.ReadUntilPromptAsync(cancellationToken);

            return ParseResponse(output);
        }
        finally
        {
            this.currentState = RawReplState.Raw;
            await this.ExitRawModeAsync(cancellationToken);
        }
    }

    private async Task SendCodeWithAdaptiveFlowControlAsync(string code, int windowSizeIncrement, CancellationToken cancellationToken)
    {
        byte[] codeBytes = Encoding.UTF8.GetBytes(code);
        int remainingWindowSize = windowSizeIncrement;
        int offset = 0;
        int flowControlWaits = 0;

        while (offset < codeBytes.Length)
        {
            if (remainingWindowSize == 0)
            {
                // Wait for flow control signal with adaptive timeout
                remainingWindowSize = await this.WaitForFlowControlAsync(cancellationToken);
                flowControlWaits++;
            }

            int chunkSize = Math.Min(remainingWindowSize, codeBytes.Length - offset);
            await this.stream.WriteAsync(codeBytes.AsMemory(offset, chunkSize), cancellationToken);
            await this.stream.FlushAsync(cancellationToken);

            offset += chunkSize;
            remainingWindowSize -= chunkSize;

            // Small delay to prevent overwhelming slow devices
            if (this.detectedCapabilities?.RequiresExtendedInterruptDelay == true && chunkSize > 64)
            {
                await Task.Delay(10, cancellationToken);
            }
        }

        this.logger.LogDebug("Sent {Bytes} bytes with {FlowControlWaits} flow control waits", codeBytes.Length, flowControlWaits);
    }

    private async Task<int> WaitForFlowControlAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1];

        // Use adaptive timeout for flow control
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(this.adaptiveResponseTimeout);

        await this.stream.ReadExactlyAsync(buffer, timeoutCts.Token);

        if (buffer[0] != 0x01)
        {
            throw new DeviceException($"Unexpected flow control byte: 0x{buffer[0]:X2}");
        }

        // Read window size increment (16-bit little-endian)
        byte[] windowSizeBytes = new byte[2];
        await this.stream.ReadExactlyAsync(windowSizeBytes, timeoutCts.Token);

        return BitConverter.ToUInt16(windowSizeBytes, 0);
    }

    private async Task<string> ReadWithTimeoutAsync(int timeoutMs, CancellationToken cancellationToken)
    {
        this.logger.LogDebug("ReadWithTimeoutAsync starting with timeout {Timeout}ms", timeoutMs);
        var buffer = new List<byte>();
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        int readAttempts = 0;

        while (DateTime.UtcNow - startTime < timeout)
        {
            readAttempts++;

            // Use ReadAvailableAsync which handles non-blocking reads with a short timeout
            byte[] readBuffer = new byte[1024];
            int bytesRead = await this.ReadAvailableAsync(readBuffer, cancellationToken);

            if (bytesRead > 0)
            {
                buffer.AddRange(readBuffer.Take(bytesRead));
                this.logger.LogDebug("Read {Bytes} bytes on attempt {Attempts}", bytesRead, readAttempts);

                // Check for completion markers
                string partial = Encoding.UTF8.GetString(buffer.ToArray());
                if (partial.Contains("raw REPL") && (partial.Contains("CTRL-B") || partial.Contains(">")))
                {
                    this.logger.LogDebug("Found complete Raw REPL entry message");
                    break;
                }
                else if (partial.Contains("OK") && (partial.Contains("\x04") || partial.Contains(">")))
                {
                    this.logger.LogDebug("Found execution output");
                    break;
                }
            }

            // Adaptive delay between read attempts
            var delay = this.detectedCapabilities?.RequiresExtendedInterruptDelay == true ? 20 : 10;
            await Task.Delay(delay, cancellationToken);
        }

        string result = Encoding.UTF8.GetString(buffer.ToArray());
        this.logger.LogDebug("ReadWithTimeoutAsync completed after {Attempts} attempts, returning {Length} chars", readAttempts, result.Length);
        return result;
    }

    private async Task<int> ReadAvailableAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        // Use a timeout-based approach instead of ReadExactlyAsync
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(100)); // Short timeout

        try
        {
            return await this.stream.ReadAsync(buffer, cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return 0; // Timeout - no data available
        }
    }

    private async Task DrainAvailableOutputAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1024];
        var totalRead = new StringBuilder();
        int drainAttempts = 0;
        const int maxDrainAttempts = 10; // Prevent infinite loops

        while (drainAttempts < maxDrainAttempts)
        {
            drainAttempts++;
            int bytesRead = await this.ReadAvailableAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                // No data available - wait a bit and try once more to be sure
                if (drainAttempts == 1)
                {
                    // Adaptive wait time based on device characteristics
                    var waitTime = this.detectedCapabilities?.RequiresExtendedStartup == true ? 100 : 50;
                    await Task.Delay(waitTime, cancellationToken);
                    continue;
                }

                break; // Truly no more data
            }

            totalRead.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
        }

        this.logger.LogDebug("Drained {Length} characters in {Attempts} attempts", totalRead.Length, drainAttempts);
    }

    private async Task<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        var result = new StringBuilder();
        var buffer = new byte[1];

        while (!cancellationToken.IsCancellationRequested)
        {
            await this.stream.ReadExactlyAsync(buffer, cancellationToken);
            char ch = (char)buffer[0];

            if (ch == '\n')
            {
                break;
            }

            if (ch != '\r')
            {
                result.Append(ch);
            }
        }

        return result.ToString();
    }

    private async Task<string> ReadUntilPromptAsync(CancellationToken cancellationToken)
    {
        return await this.ReadWithTimeoutAsync((int)this.adaptiveResponseTimeout.TotalMilliseconds, cancellationToken);
    }

    private async Task WriteByteAsync(byte value, CancellationToken cancellationToken)
    {
        await this.stream.WriteAsync(new byte[] { value }, cancellationToken);
        await this.stream.FlushAsync(cancellationToken);
    }

    private static string PreprocessCodeForRawRepl(string code)
    {
        // Remove any trailing whitespace/newlines that might interfere
        code = code.TrimEnd();

        // Ensure consistent line endings
        code = code.Replace("\r\n", "\n").Replace("\r", "\n");

        return code;
    }

    private static RawReplResponse ParseResponse(string output)
    {
        var response = new RawReplResponse();

        // Enhanced response parsing
        if (output.Contains("Traceback") || output.Contains("Error") || output.Contains("Exception"))
        {
            response.IsSuccess = false;
            response.ErrorOutput = output;
            response.Exception = new Exception($"Device execution error: {output}");
        }
        else
        {
            response.IsSuccess = true;
            response.Output = output;

            // Parse Raw REPL response format: "OK<content>\x04\x04>"
            string result = output;

            // Remove "OK" prefix if present
            if (result.StartsWith("OK"))
            {
                result = result.Substring(2);
            }

            // Remove trailing control characters and prompt
            // Find the first \x04 character (start of end sequence)
            int firstControlCharIndex = result.IndexOf('\x04');

            if (firstControlCharIndex >= 0)
            {
                result = result.Substring(0, firstControlCharIndex);
            }
            else if (result.EndsWith('>'))
            {
                result = result.Substring(0, result.Length - 1);
            }

            // Trim whitespace and control characters
            response.Result = result.Trim('\r', '\n', ' ', '\t');
        }

        return response;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.protocolSemaphore.Dispose();
        this.disposed = true;
    }
}

/// <summary>
/// Represents the current state of the Raw REPL protocol.
/// </summary>
public enum RawReplState
{
    /// <summary>Normal REPL mode with >>> prompt.</summary>
    Normal,

    /// <summary>Raw REPL mode for programmatic code execution.</summary>
    Raw,

    /// <summary>Raw-paste REPL mode with flow control.</summary>
    RawPaste,
}

/// <summary>
/// Response from Raw REPL code execution.
/// </summary>
public class RawReplResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the execution was successful.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the execution result.
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Gets or sets the full output from the device.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Gets or sets error output if execution failed.
    /// </summary>
    public string? ErrorOutput { get; set; }

    /// <summary>
    /// Gets or sets the exception if execution failed.
    /// </summary>
    public Exception? Exception { get; set; }
}

/// <summary>
/// Configuration options for the Raw REPL protocol.
/// </summary>
public class RawReplConfiguration
{
    /// <summary>
    /// Gets or sets the base response timeout.
    /// </summary>
    public TimeSpan BaseResponseTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum response timeout.
    /// </summary>
    public TimeSpan MaxResponseTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the startup delay.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the maximum startup delay.
    /// </summary>
    public TimeSpan MaxStartupDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the interrupt delay.
    /// </summary>
    public TimeSpan InterruptDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the retry delay.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets or sets the maximum retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum window size.
    /// </summary>
    public int MaximumWindowSize { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the preferred window size.
    /// </summary>
    public int? PreferredWindowSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to enable raw-paste auto-detection.
    /// </summary>
    public bool EnableRawPasteAutoDetection { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable verbose logging.
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;
}

/// <summary>
/// Detected capabilities of a MicroPython device.
/// </summary>
public class DeviceReplCapabilities
{
    /// <summary>
    /// Gets or sets a value indicating whether the device supports raw-paste mode.
    /// </summary>
    public bool SupportsRawPasteMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the device has reliable flow control.
    /// </summary>
    public bool HasReliableFlowControl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the device supports large code transfers.
    /// </summary>
    public bool SupportsLargeCodeTransfers { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the device requires extended startup.
    /// </summary>
    public bool RequiresExtendedStartup { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the device requires extended interrupt delay.
    /// </summary>
    public bool RequiresExtendedInterruptDelay { get; set; }

    /// <summary>
    /// Gets or sets the preferred window size for flow control.
    /// </summary>
    public int PreferredWindowSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum window size for flow control.
    /// </summary>
    public int MaxWindowSize { get; set; }

    /// <summary>
    /// Gets or sets the average response time.
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the detected platform.
    /// </summary>
    public string? DetectedPlatform { get; set; }

    /// <summary>
    /// Gets or sets the MicroPython version.
    /// </summary>
    public string? MicroPythonVersion { get; set; }
}

/// <summary>
/// Metrics for Raw REPL protocol operations.
/// </summary>
public class ReplProtocolMetrics
{
    /// <summary>
    /// Gets or sets the number of successful operations.
    /// </summary>
    public int SuccessfulOperations { get; set; }

    /// <summary>
    /// Gets or sets the number of failed operations.
    /// </summary>
    public int FailedOperations { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts.
    /// </summary>
    public int RetryAttempts { get; set; }

    /// <summary>
    /// Gets or sets the average operation time.
    /// </summary>
    public TimeSpan AverageOperationTime { get; set; }

    /// <summary>
    /// Gets or sets the last operation time.
    /// </summary>
    public DateTime LastOperationTime { get; set; }
}