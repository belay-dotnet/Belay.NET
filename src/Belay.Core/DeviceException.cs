namespace Belay.Core;

/// <summary>
/// Simplified exception for all device communication errors.
/// Replaces complex exception hierarchy with single, informative exception type.
/// </summary>
public class DeviceException : Exception
{
    /// <summary>
    /// Gets the raw output from the device when the error occurred.
    /// </summary>
    public string? DeviceOutput { get; init; }

    /// <summary>
    /// Gets the Python code that was being executed when the error occurred.
    /// </summary>
    public string? ExecutedCode { get; init; }

    /// <summary>
    /// Gets the connection string for the device where the error occurred.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DeviceException(string message, Exception? innerException = null) 
        : base(message, innerException) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceException"/> class with device context.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="deviceOutput">The raw output from the device.</param>
    /// <param name="executedCode">The Python code that was being executed.</param>
    /// <param name="connectionString">The connection string for the device.</param>
    public DeviceException(string message, string? deviceOutput, string? executedCode = null, string? connectionString = null) 
        : base(message)
    {
        DeviceOutput = deviceOutput;
        ExecutedCode = executedCode;
        ConnectionString = connectionString;
    }
}

/// <summary>
/// Exception thrown when device connection fails.
/// </summary>
public class DeviceConnectionException : DeviceException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceConnectionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="connectionString">The connection string that failed.</param>
    public DeviceConnectionException(string message, string connectionString) 
        : base(message) 
    {
        ConnectionString = connectionString;
    }
}

/// <summary>
/// Exception thrown when device operation times out.
/// </summary>
public class DeviceTimeoutException : DeviceException
{
    /// <summary>
    /// Gets the timeout duration that was exceeded.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceTimeoutException"/> class.
    /// </summary>
    /// <param name="timeout">The timeout duration that was exceeded.</param>
    /// <param name="executedCode">The Python code that was being executed.</param>
    public DeviceTimeoutException(TimeSpan timeout, string? executedCode = null) 
        : base($"Device operation timed out after {timeout}")
    {
        Timeout = timeout;
        ExecutedCode = executedCode;
    }
}