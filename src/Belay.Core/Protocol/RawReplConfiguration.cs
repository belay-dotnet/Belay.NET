// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Protocol;

/// <summary>
/// Configuration options for Raw REPL protocol behavior with auto-detection capabilities.
/// </summary>
public class RawReplConfiguration
{
    /// <summary>
    /// Gets or sets the timeout for initial protocol initialization.
    /// Auto-detection will increase this if needed.
    /// </summary>
    public TimeSpan InitializationTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the base timeout for reading responses.
    /// Auto-detection will adjust based on device performance.
    /// </summary>
    public TimeSpan BaseResponseTimeout { get; set; } = TimeSpan.FromMilliseconds(2000);

    /// <summary>
    /// Gets or sets the maximum response timeout after adaptive increases.
    /// </summary>
    public TimeSpan MaxResponseTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the preferred window size for raw-paste mode.
    /// If null, will be auto-detected from device capabilities.
    /// </summary>
    public int? PreferredWindowSize { get; set; }

    /// <summary>
    /// Gets or sets the minimum window size to accept during negotiation.
    /// </summary>
    public int MinimumWindowSize { get; set; } = 16;

    /// <summary>
    /// Gets or sets the maximum window size to request during negotiation.
    /// </summary>
    public int MaximumWindowSize { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed operations.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay between retry attempts.
    /// Each retry will use exponential backoff based on this value.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the startup delay for device initialization.
    /// Auto-detection will adjust based on device response time.
    /// </summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets or sets the maximum startup delay after adaptive increases.
    /// </summary>
    public TimeSpan MaxStartupDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the interrupt sequence delay.
    /// Some devices need more time to process interrupts.
    /// </summary>
    public TimeSpan InterruptDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets whether to enable raw-paste mode auto-detection.
    /// If false, will fall back to regular raw mode.
    /// </summary>
    public bool EnableRawPasteAutoDetection { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable adaptive timing adjustments.
    /// </summary>
    public bool EnableAdaptiveTiming { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable adaptive flow control.
    /// </summary>
    public bool EnableAdaptiveFlowControl { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable verbose protocol logging.
    /// Useful for debugging protocol issues.
    /// </summary>
    public bool EnableVerboseLogging { get; set; } = false;

    /// <summary>
    /// Creates a copy of this configuration with conservative fallback settings.
    /// Used when auto-detection fails.
    /// </summary>
    /// <returns>A new configuration with conservative settings.</returns>
    public RawReplConfiguration CreateFallbackConfiguration()
    {
        return new RawReplConfiguration
        {
            InitializationTimeout = TimeSpan.FromSeconds(10),
            BaseResponseTimeout = TimeSpan.FromSeconds(5),
            MaxResponseTimeout = MaxResponseTimeout,
            PreferredWindowSize = 32, // Conservative window size
            MinimumWindowSize = MinimumWindowSize,
            MaximumWindowSize = 256, // Conservative maximum
            MaxRetryAttempts = MaxRetryAttempts + 2, // More retries for difficult devices
            RetryDelay = TimeSpan.FromMilliseconds(500), // Longer delays
            StartupDelay = TimeSpan.FromSeconds(5),
            MaxStartupDelay = MaxStartupDelay,
            InterruptDelay = TimeSpan.FromMilliseconds(500),
            EnableRawPasteAutoDetection = false, // Disable raw-paste mode
            EnableAdaptiveTiming = false,
            EnableAdaptiveFlowControl = false,
            EnableVerboseLogging = EnableVerboseLogging
        };
    }
}

/// <summary>
/// Detected capabilities and characteristics of a MicroPython device's REPL implementation.
/// </summary>
public class DeviceReplCapabilities
{
    /// <summary>
    /// Gets or sets whether the device supports raw-paste mode.
    /// </summary>
    public bool SupportsRawPasteMode { get; set; }

    /// <summary>
    /// Gets or sets the device's preferred window size for raw-paste mode.
    /// </summary>
    public int PreferredWindowSize { get; set; }

    /// <summary>
    /// Gets or sets the device's maximum supported window size.
    /// </summary>
    public int MaxWindowSize { get; set; }

    /// <summary>
    /// Gets or sets the measured response time characteristics.
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }

    /// <summary>
    /// Gets or sets whether the device needs extended startup delays.
    /// </summary>
    public bool RequiresExtendedStartup { get; set; }

    /// <summary>
    /// Gets or sets whether the device needs extended interrupt processing time.
    /// </summary>
    public bool RequiresExtendedInterruptDelay { get; set; }

    /// <summary>
    /// Gets or sets the device platform/type if detectable.
    /// </summary>
    public string? DetectedPlatform { get; set; }

    /// <summary>
    /// Gets or sets the MicroPython version if detectable.
    /// </summary>
    public string? MicroPythonVersion { get; set; }

    /// <summary>
    /// Gets or sets whether flow control bytes are processed correctly.
    /// </summary>
    public bool HasReliableFlowControl { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the device properly handles large code transfers.
    /// </summary>
    public bool SupportsLargeCodeTransfers { get; set; } = true;

    /// <summary>
    /// Gets or sets the timestamp when capabilities were detected.
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Protocol metrics and performance tracking.
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
    /// Gets or sets the number of retry attempts made.
    /// </summary>
    public int RetryAttempts { get; set; }

    /// <summary>
    /// Gets or sets the total number of adaptive adjustments made.
    /// </summary>
    public int AdaptiveAdjustments { get; set; }

    /// <summary>
    /// Gets or sets the average operation duration.
    /// </summary>
    public TimeSpan AverageOperationTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last operation.
    /// </summary>
    public DateTime LastOperationTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Calculates the success rate as a percentage.
    /// </summary>
    public double SuccessRate =>
        SuccessfulOperations + FailedOperations == 0 ?
        100.0 :
        (double)SuccessfulOperations / (SuccessfulOperations + FailedOperations) * 100.0;
}