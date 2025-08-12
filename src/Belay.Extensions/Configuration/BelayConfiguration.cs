// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Extensions.Configuration;

/// <summary>
/// Root configuration model for Belay.NET.
/// </summary>
public class BelayConfiguration {
    /// <summary>
    /// Gets or sets the device configuration.
    /// </summary>
    public DeviceConfiguration Device { get; set; } = new();

    /// <summary>
    /// Gets or sets the communication configuration.
    /// </summary>
    public CommunicationConfiguration Communication { get; set; } = new();

    /// <summary>
    /// Gets or sets the executor configuration.
    /// </summary>
    public ExecutorConfiguration Executor { get; set; } = new();

    /// <summary>
    /// Gets or sets the exception handling configuration.
    /// </summary>
    public ExceptionHandlingConfiguration ExceptionHandling { get; set; } = new();
}

/// <summary>
/// Configuration for device connection and discovery.
/// </summary>
public class DeviceConfiguration {
    /// <summary>
    /// Gets or sets the default connection timeout in milliseconds.
    /// </summary>
    public int DefaultConnectionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the default command timeout in milliseconds.
    /// </summary>
    public int DefaultCommandTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the device discovery configuration.
    /// </summary>
    public DeviceDiscoveryConfiguration Discovery { get; set; } = new();

    /// <summary>
    /// Gets or sets the retry configuration.
    /// </summary>
    public RetryConfiguration Retry { get; set; } = new();
}

/// <summary>
/// Configuration for device discovery.
/// </summary>
public class DeviceDiscoveryConfiguration {
    /// <summary>
    /// Gets or sets a value indicating whether auto-discovery is enabled.
    /// </summary>
    public bool EnableAutoDiscovery { get; set; } = true;

    /// <summary>
    /// Gets or sets the discovery timeout in milliseconds.
    /// </summary>
    public int DiscoveryTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the list of serial port patterns to scan.
    /// </summary>
    public string[] SerialPortPatterns { get; set; } = { "COM*", "/dev/ttyUSB*", "/dev/ttyACM*" };
}

/// <summary>
/// Configuration for communication protocols.
/// </summary>
public class CommunicationConfiguration {
    /// <summary>
    /// Gets or sets the serial communication configuration.
    /// </summary>
    public SerialConfiguration Serial { get; set; } = new();

    /// <summary>
    /// Gets or sets the raw REPL configuration.
    /// </summary>
    public RawReplConfiguration RawRepl { get; set; } = new();
}

/// <summary>
/// Configuration for serial communication.
/// </summary>
public class SerialConfiguration {
    /// <summary>
    /// Gets or sets the default baud rate.
    /// </summary>
    public int DefaultBaudRate { get; set; } = 115200;

    /// <summary>
    /// Gets or sets the read timeout in milliseconds.
    /// </summary>
    public int ReadTimeoutMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the write timeout in milliseconds.
    /// </summary>
    public int WriteTimeoutMs { get; set; } = 1000;
}

/// <summary>
/// Configuration for Raw REPL protocol.
/// </summary>
public class RawReplConfiguration {
    /// <summary>
    /// Gets or sets the initialization timeout in milliseconds.
    /// </summary>
    public int InitializationTimeoutMs { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the window size for flow control.
    /// </summary>
    public int WindowSize { get; set; } = 256;

    /// <summary>
    /// Gets or sets the maximum retries for protocol operations.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Configuration for executor behavior.
/// </summary>
public class ExecutorConfiguration {
    /// <summary>
    /// Gets or sets the default task timeout in milliseconds.
    /// </summary>
    public int DefaultTaskTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the maximum cache size for task results.
    /// </summary>
    public int MaxCacheSize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether caching is enabled by default.
    /// </summary>
    public bool EnableCachingByDefault { get; set; } = false;

    /// <summary>
    /// Gets or sets the cache expiration time in milliseconds.
    /// </summary>
    public int CacheExpirationMs { get; set; } = 600000; // 10 minutes
}

/// <summary>
/// Configuration for exception handling behavior.
/// </summary>
public class ExceptionHandlingConfiguration {
    /// <summary>
    /// Gets or sets a value indicating whether exceptions should be rethrown after handling.
    /// </summary>
    public bool RethrowExceptions { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether exceptions should be logged.
    /// </summary>
    public bool LogExceptions { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether stack traces should be included in logs.
    /// </summary>
    public bool IncludeStackTraces { get; set; } = true;

    /// <summary>
    /// Gets or sets the log level for exceptions.
    /// </summary>
    public Microsoft.Extensions.Logging.LogLevel ExceptionLogLevel { get; set; } = Microsoft.Extensions.Logging.LogLevel.Error;

    /// <summary>
    /// Gets or sets a value indicating whether context should be preserved in exceptions.
    /// </summary>
    public bool PreserveContext { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of context entries to preserve.
    /// </summary>
    public int MaxContextEntries { get; set; } = 50;
}

/// <summary>
/// Configuration for retry behavior.
/// </summary>
public class RetryConfiguration {
    /// <summary>
    /// Gets or sets the maximum number of retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial retry delay in milliseconds.
    /// </summary>
    public int InitialRetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the retry backoff multiplier.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the maximum retry delay in milliseconds.
    /// </summary>
    public int MaxRetryDelayMs { get; set; } = 30000;
}
