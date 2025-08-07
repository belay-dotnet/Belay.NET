// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Exceptions;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Global exception handler for consistent error handling throughout the application.
/// </summary>
internal class GlobalExceptionHandler : IGlobalExceptionHandler {
    private readonly IErrorMapper errorMapper;
    private readonly IExceptionEnricher enricher;
    private readonly ILogger<GlobalExceptionHandler> logger;
    private readonly ExceptionHandlingConfiguration configuration = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalExceptionHandler"/> class.
    /// </summary>
    /// <param name="errorMapper">The error mapper.</param>
    /// <param name="enricher">The exception enricher.</param>
    /// <param name="logger">The logger.</param>
    public GlobalExceptionHandler(
        IErrorMapper errorMapper,
        IExceptionEnricher enricher,
        ILogger<GlobalExceptionHandler> logger) {
        this.errorMapper = errorMapper;
        this.enricher = enricher;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TResult> ExecuteWithErrorHandlingAsync<TResult>(Func<Task<TResult>> operation, string? context = null) {
        try {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception ex) {
            var mappedException = this.errorMapper.MapException(ex, context);
            var enrichedException = this.enricher.Enrich(mappedException, context);

            if (this.configuration.LogExceptions) {
                this.LogException(enrichedException, context);
            }

            if (this.configuration.RethrowExceptions) {
                throw enrichedException;
            }

            return default!;
        }
    }

    /// <inheritdoc/>
    public async Task ExecuteWithErrorHandlingAsync(Func<Task> operation, string? context = null) {
        try {
            await operation().ConfigureAwait(false);
        }
        catch (Exception ex) {
            var mappedException = this.errorMapper.MapException(ex, context);
            var enrichedException = this.enricher.Enrich(mappedException, context);

            if (this.configuration.LogExceptions) {
                this.LogException(enrichedException, context);
            }

            if (this.configuration.RethrowExceptions) {
                throw enrichedException;
            }
        }
    }

    /// <inheritdoc/>
    public void ConfigureExceptionHandling(Action<ExceptionHandlingConfiguration> configure) {
        configure(this.configuration);
    }

    private void LogException(Exception exception, string? context) {
        if (exception is BelayException belayEx) {
            // Log with structured data from Belay exception
            using var scope = this.logger.BeginScope(belayEx.Context);

            this.logger.Log(
                this.configuration.ExceptionLogLevel,
                exception,
                "Belay exception occurred in context {Context}. ErrorCode: {ErrorCode}, Component: {Component}",
                context ?? "Unknown",
                belayEx.ErrorCode,
                belayEx.ComponentName);
        }
        else {
            this.logger.Log(
                this.configuration.ExceptionLogLevel,
                exception,
                "Unhandled exception occurred in context {Context}",
                context ?? "Unknown");
        }
    }
}
