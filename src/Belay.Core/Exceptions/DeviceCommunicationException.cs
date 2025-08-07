// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Exceptions;

using System;

/// <summary>
/// Exception thrown when device communication operations fail.
/// </summary>
public class DeviceCommunicationException : BelayException {
    /// <summary>
    /// Gets the device identifier.
    /// </summary>
    public string? DeviceId { get; }

    /// <summary>
    /// Gets the connection string used.
    /// </summary>
    public string? ConnectionString { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceCommunicationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="deviceId">The device identifier, if known.</param>
    /// <param name="connectionString">The connection string, if known.</param>
    public DeviceCommunicationException(string message, string? deviceId = null, string? connectionString = null)
        : base(message, "BELAY_COMM_ERROR", nameof(DeviceCommunicationException)) {
        this.DeviceId = deviceId;
        this.ConnectionString = connectionString;

        if (deviceId != null) {
            this.WithContext("device_id", deviceId);
        }

        if (connectionString != null) {
            this.WithContext("connection_string", connectionString);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceCommunicationException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="deviceId">The device identifier, if known.</param>
    /// <param name="connectionString">The connection string, if known.</param>
    public DeviceCommunicationException(string message, Exception innerException, string? deviceId = null, string? connectionString = null)
        : base(message, innerException, "BELAY_COMM_ERROR", nameof(DeviceCommunicationException)) {
        this.DeviceId = deviceId;
        this.ConnectionString = connectionString;

        if (deviceId != null) {
            this.WithContext("device_id", deviceId);
        }

        if (connectionString != null) {
            this.WithContext("connection_string", connectionString);
        }
    }

    /// <inheritdoc/>
    protected override string GetDefaultErrorCode() => "BELAY_COMM_ERROR";
}

/// <summary>
/// Exception thrown when device connection operations fail.
/// </summary>
public class DeviceConnectionException : DeviceCommunicationException {
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceConnectionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="deviceId">The device identifier, if known.</param>
    /// <param name="connectionString">The connection string, if known.</param>
    /// <param name="innerException">The inner exception, if any.</param>
    public DeviceConnectionException(string message, string? deviceId = null, string? connectionString = null, Exception? innerException = null)
        : base(message, innerException ?? new InvalidOperationException(), deviceId, connectionString) {
        this.ErrorCode = "BELAY_CONN_ERROR";
        if (innerException != null) {
            this.WithContext("inner_exception_type", innerException.GetType().Name)
                .WithContext("inner_exception_message", innerException.Message);
        }
    }
}

/// <summary>
/// Exception thrown when device operations timeout.
/// </summary>
public class DeviceTimeoutException : DeviceCommunicationException {
    /// <summary>
    /// Gets the timeout duration.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Gets the operation that timed out.
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceTimeoutException"/> class.
    /// </summary>
    /// <param name="operation">The operation that timed out.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <param name="deviceId">The device identifier, if known.</param>
    public DeviceTimeoutException(string operation, TimeSpan timeout, string? deviceId = null)
        : base($"Operation '{operation}' timed out after {timeout.TotalSeconds:F1}s", deviceId) {
        this.ErrorCode = "BELAY_TIMEOUT_ERROR";
        this.Operation = operation;
        this.Timeout = timeout;

        this.WithContext("operation", operation)
            .WithContext("timeout_seconds", timeout.TotalSeconds);
    }
}
