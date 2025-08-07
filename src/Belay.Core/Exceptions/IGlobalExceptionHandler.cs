// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Exceptions;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Interface for global exception handling across the application.
/// </summary>
public interface IGlobalExceptionHandler {
    /// <summary>
    /// Executes an operation with error handling and returns a result.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="context">The context information.</param>
    /// <returns>The result of the operation.</returns>
    Task<TResult> ExecuteWithErrorHandlingAsync<TResult>(Func<Task<TResult>> operation, string? context = null);

    /// <summary>
    /// Executes an operation with error handling.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="context">The context information.</param>
    /// <returns>A task representing the operation.</returns>
    Task ExecuteWithErrorHandlingAsync(Func<Task> operation, string? context = null);

    /// <summary>
    /// Configures the exception handling behavior.
    /// </summary>
    /// <param name="configure">The configuration action.</param>
    void ConfigureExceptionHandling(Action<ExceptionHandlingConfiguration> configure);
}

/// <summary>
/// Configuration for exception handling behavior.
/// </summary>
public class ExceptionHandlingConfiguration {
    /// <summary>
    /// Gets or sets a value indicating whether exceptions should be re-thrown.
    /// Default is true.
    /// </summary>
    public bool RethrowExceptions { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether exceptions should be logged.
    /// Default is true.
    /// </summary>
    public bool LogExceptions { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether stack traces should be included in logs.
    /// Default is true.
    /// </summary>
    public bool IncludeStackTraces { get; set; } = true;

    /// <summary>
    /// Gets or sets the log level for exceptions.
    /// Default is Error.
    /// </summary>
    public LogLevel ExceptionLogLevel { get; set; } = LogLevel.Error;

    /// <summary>
    /// Gets or sets a value indicating whether context should be preserved in exceptions.
    /// Default is true.
    /// </summary>
    public bool PreserveContext { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of context entries to preserve.
    /// Default is 50.
    /// </summary>
    public int MaxContextEntries { get; set; } = 50;
}
