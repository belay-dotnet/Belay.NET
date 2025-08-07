// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Exceptions;

using System;
using System.Collections.Generic;

/// <summary>
/// Interface for mapping exceptions and device errors to structured Belay exceptions.
/// </summary>
public interface IErrorMapper {
    /// <summary>
    /// Maps a general exception to a Belay exception.
    /// </summary>
    /// <param name="exception">The exception to map.</param>
    /// <param name="context">Additional context information.</param>
    /// <returns>The mapped Belay exception.</returns>
    BelayException MapException(Exception exception, string? context = null);

    /// <summary>
    /// Maps device error output to a Belay exception.
    /// </summary>
    /// <param name="deviceOutput">The device error output.</param>
    /// <param name="executedCode">The code that was executed, if known.</param>
    /// <returns>The mapped Belay exception.</returns>
    BelayException MapDeviceError(string deviceOutput, string? executedCode = null);

    /// <summary>
    /// Enriches an existing Belay exception with additional context.
    /// </summary>
    /// <typeparam name="T">The exception type.</typeparam>
    /// <param name="exception">The exception to enrich.</param>
    /// <param name="context">The context to add.</param>
    /// <returns>The enriched exception.</returns>
    T EnrichException<T>(T exception, Dictionary<string, object> context)
        where T : BelayException;
}

/// <summary>
/// Interface for enriching exceptions with additional context.
/// </summary>
public interface IExceptionEnricher {
    /// <summary>
    /// Enriches an exception with component and context information.
    /// </summary>
    /// <typeparam name="T">The exception type.</typeparam>
    /// <param name="exception">The exception to enrich.</param>
    /// <param name="component">The component name.</param>
    /// <param name="additionalContext">Additional context information.</param>
    /// <returns>The enriched exception.</returns>
    T Enrich<T>(T exception, string? component = null, Dictionary<string, object>? additionalContext = null)
        where T : Exception;

    /// <summary>
    /// Enriches an exception with device context information.
    /// </summary>
    /// <typeparam name="T">The exception type.</typeparam>
    /// <param name="exception">The exception to enrich.</param>
    /// <param name="deviceType">The device type.</param>
    /// <param name="firmwareVersion">The firmware version.</param>
    /// <param name="sessionId">The session identifier, if known.</param>
    /// <returns>The enriched exception.</returns>
    T EnrichWithDeviceContext<T>(T exception, string? deviceType = null, string? firmwareVersion = null, string? sessionId = null)
        where T : Exception;
}
