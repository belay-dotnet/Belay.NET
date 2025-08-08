// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Exceptions;

using System;
using System.Collections.Generic;

/// <summary>
/// Base exception class for all Belay.NET exceptions.
/// Provides context preservation and structured error information.
/// </summary>
public abstract class BelayException : Exception {
    /// <summary>
    /// Gets or sets the specific error code for this exception.
    /// </summary>
    public string ErrorCode { get; protected set; }

    /// <summary>
    /// Gets the context dictionary for additional error information.
    /// </summary>
    public Dictionary<string, object> Context { get; } = new();

    /// <summary>
    /// Gets the timestamp when the exception occurred.
    /// </summary>
    public DateTime OccurredAt { get; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the name of the component where the exception occurred.
    /// </summary>
    public string ComponentName { get; protected set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BelayException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errorCode">The error code, or null to use default.</param>
    /// <param name="componentName">The component name, or null to use type name.</param>
    protected BelayException(string message, string? errorCode = null, string? componentName = null)
        : base(message) {
        this.ErrorCode = errorCode ?? this.GetDefaultErrorCode();
        this.ComponentName = componentName ?? this.GetType().Name;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BelayException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="errorCode">The error code, or null to use default.</param>
    /// <param name="componentName">The component name, or null to use type name.</param>
    protected BelayException(string message, Exception innerException, string? errorCode = null, string? componentName = null)
        : base(message, innerException) {
        this.ErrorCode = errorCode ?? this.GetDefaultErrorCode();
        this.ComponentName = componentName ?? this.GetType().Name;
    }

    /// <summary>
    /// Adds context information to this exception.
    /// </summary>
    /// <param name="key">The context key.</param>
    /// <param name="value">The context value.</param>
    /// <returns>This exception instance for method chaining.</returns>
    public BelayException WithContext(string key, object value) {
        this.Context[key] = value;
        return this;
    }

    /// <summary>
    /// Adds multiple context entries to this exception.
    /// </summary>
    /// <param name="context">The context dictionary to merge.</param>
    /// <returns>This exception instance for method chaining.</returns>
    public BelayException WithContext(Dictionary<string, object> context) {
        foreach (var kvp in context) {
            this.Context[kvp.Key] = kvp.Value;
        }

        return this;
    }

    /// <summary>
    /// Gets the default error code for this exception type.
    /// </summary>
    /// <returns>The default error code.</returns>
    protected virtual string GetDefaultErrorCode() => "BELAY_UNKNOWN";
}
