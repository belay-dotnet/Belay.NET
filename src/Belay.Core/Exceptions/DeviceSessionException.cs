// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Exceptions;

using System;
using Belay.Core.Sessions;

/// <summary>
/// Exception thrown when device session operations fail.
/// </summary>
public class DeviceSessionException : BelayException {
    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    public string SessionId { get; }

    /// <summary>
    /// Gets the session state when the exception occurred.
    /// </summary>
    public DeviceSessionState SessionState { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceSessionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="sessionState">The session state when the exception occurred.</param>
    public DeviceSessionException(string message, string sessionId, DeviceSessionState sessionState)
        : base(message, "BELAY_SESSION_ERROR", nameof(DeviceSessionException)) {
        this.SessionId = sessionId;
        this.SessionState = sessionState;

        this.WithContext("session_id", sessionId)
            .WithContext("session_state", sessionState.ToString());
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceSessionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="sessionState">The session state when the exception occurred.</param>
    public DeviceSessionException(string message, Exception innerException, string sessionId, DeviceSessionState sessionState)
        : base(message, innerException, "BELAY_SESSION_ERROR", nameof(DeviceSessionException)) {
        this.SessionId = sessionId;
        this.SessionState = sessionState;

        this.WithContext("session_id", sessionId)
            .WithContext("session_state", sessionState.ToString());
    }

    /// <inheritdoc/>
    protected override string GetDefaultErrorCode() => "BELAY_SESSION_ERROR";
}

/// <summary>
/// Exception thrown when device resource operations fail.
/// </summary>
public class DeviceResourceException : BelayException {
    /// <summary>
    /// Gets the resource identifier.
    /// </summary>
    public string ResourceId { get; }

    /// <summary>
    /// Gets the resource type.
    /// </summary>
    public string ResourceType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceResourceException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="resourceId">The resource identifier.</param>
    /// <param name="resourceType">The resource type.</param>
    public DeviceResourceException(string message, string resourceId, string resourceType)
        : base(message, "BELAY_RESOURCE_ERROR", nameof(DeviceResourceException)) {
        this.ResourceId = resourceId;
        this.ResourceType = resourceType;

        this.WithContext("resource_id", resourceId)
            .WithContext("resource_type", resourceType);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceResourceException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    /// <param name="resourceId">The resource identifier.</param>
    /// <param name="resourceType">The resource type.</param>
    public DeviceResourceException(string message, Exception innerException, string resourceId, string resourceType)
        : base(message, innerException, "BELAY_RESOURCE_ERROR", nameof(DeviceResourceException)) {
        this.ResourceId = resourceId;
        this.ResourceType = resourceType;

        this.WithContext("resource_id", resourceId)
            .WithContext("resource_type", resourceType);
    }

    /// <inheritdoc/>
    protected override string GetDefaultErrorCode() => "BELAY_RESOURCE_ERROR";
}
