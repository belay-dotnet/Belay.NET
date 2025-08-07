// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Exceptions;

using System;

/// <summary>
/// Exception thrown when device code execution fails.
/// </summary>
public class DeviceExecutionException : BelayException {
    /// <summary>
    /// Gets the Python code that was executed.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// Gets the device stack trace, if available.
    /// </summary>
    public string? DeviceStackTrace { get; }

    /// <summary>
    /// Gets the line number where the error occurred, if available.
    /// </summary>
    public int? LineNumber { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceExecutionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="code">The Python code that was executed, if known.</param>
    /// <param name="deviceStackTrace">The device stack trace, if available.</param>
    /// <param name="lineNumber">The line number where the error occurred, if available.</param>
    public DeviceExecutionException(string message, string? code = null, string? deviceStackTrace = null, int? lineNumber = null)
        : base(message, "BELAY_EXEC_ERROR", nameof(DeviceExecutionException)) {
        this.Code = code;
        this.DeviceStackTrace = deviceStackTrace;
        this.LineNumber = lineNumber;

        if (code != null) {
            this.WithContext("executed_code", code);
        }

        if (deviceStackTrace != null) {
            this.WithContext("device_stack_trace", deviceStackTrace);
        }

        if (lineNumber.HasValue) {
            this.WithContext("line_number", lineNumber.Value);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceExecutionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="code">The Python code that was executed, if known.</param>
    /// <param name="deviceStackTrace">The device stack trace, if available.</param>
    /// <param name="lineNumber">The line number where the error occurred, if available.</param>
    public DeviceExecutionException(string message, Exception innerException, string? code = null, string? deviceStackTrace = null, int? lineNumber = null)
        : base(message, innerException, "BELAY_EXEC_ERROR", nameof(DeviceExecutionException)) {
        this.Code = code;
        this.DeviceStackTrace = deviceStackTrace;
        this.LineNumber = lineNumber;

        if (code != null) {
            this.WithContext("executed_code", code);
        }

        if (deviceStackTrace != null) {
            this.WithContext("device_stack_trace", deviceStackTrace);
        }

        if (lineNumber.HasValue) {
            this.WithContext("line_number", lineNumber.Value);
        }
    }

    /// <inheritdoc/>
    protected override string GetDefaultErrorCode() => "BELAY_EXEC_ERROR";
}

/// <summary>
/// Exception thrown when device code has syntax errors.
/// </summary>
public class DeviceCodeSyntaxException : DeviceExecutionException {
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceCodeSyntaxException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="code">The Python code with syntax errors.</param>
    /// <param name="lineNumber">The line number where the syntax error occurred, if available.</param>
    public DeviceCodeSyntaxException(string message, string code, int? lineNumber = null)
        : base(message, code, null, lineNumber) {
        this.ErrorCode = "BELAY_SYNTAX_ERROR";
    }
}

/// <summary>
/// Exception thrown when device runs out of memory.
/// </summary>
public class DeviceMemoryException : DeviceExecutionException {
    /// <summary>
    /// Gets the available memory on the device, if known.
    /// </summary>
    public long? AvailableMemory { get; }

    /// <summary>
    /// Gets the requested memory amount, if known.
    /// </summary>
    public long? RequestedMemory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceMemoryException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="availableMemory">The available memory on the device, if known.</param>
    /// <param name="requestedMemory">The requested memory amount, if known.</param>
    public DeviceMemoryException(string message, long? availableMemory = null, long? requestedMemory = null)
        : base(message) {
        this.ErrorCode = "BELAY_MEMORY_ERROR";
        this.AvailableMemory = availableMemory;
        this.RequestedMemory = requestedMemory;

        if (availableMemory.HasValue) {
            this.WithContext("available_memory", availableMemory.Value);
        }

        if (requestedMemory.HasValue) {
            this.WithContext("requested_memory", requestedMemory.Value);
        }
    }
}
