// Copyright (c) 2024 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for more information.
using System;
using Belay.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Extension methods for registering exception handling services.
/// </summary>
public static class ExceptionHandlingServiceExtensions {
    /// <summary>
    /// Adds Belay exception handling services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBelayExceptionHandling(
        this IServiceCollection services,
        Action<ExceptionHandlingConfiguration>? configure = null) {
        // Register core exception handling services
        services.AddSingleton<IErrorMapper, ErrorMapper>();
        services.AddSingleton<IExceptionEnricher, ExceptionEnricher>();
        services.AddSingleton<IGlobalExceptionHandler, GlobalExceptionHandler>();

        // Configure global exception handling if configuration is provided
        if (configure != null) {
            services.AddSingleton<IExceptionHandlingConfigurator>(provider => {
                var handler = provider.GetRequiredService<IGlobalExceptionHandler>();
                return new ExceptionHandlingConfigurator(handler, configure);
            });
        }

        return services;
    }

    /// <summary>
    /// Adds Belay exception handling services with specific configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The exception handling configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBelayExceptionHandling(
        this IServiceCollection services,
        ExceptionHandlingConfiguration configuration) {
        return services.AddBelayExceptionHandling(config => {
            config.RethrowExceptions = configuration.RethrowExceptions;
            config.LogExceptions = configuration.LogExceptions;
            config.IncludeStackTraces = configuration.IncludeStackTraces;
            config.ExceptionLogLevel = configuration.ExceptionLogLevel;
            config.PreserveContext = configuration.PreserveContext;
            config.MaxContextEntries = configuration.MaxContextEntries;
        });
    }

    /// <summary>
    /// Gets the global exception handler from the service provider.
    /// </summary>
    /// <param name="provider">The service provider.</param>
    /// <returns>The global exception handler.</returns>
    public static IGlobalExceptionHandler GetBelayExceptionHandler(this IServiceProvider provider) {
        return provider.GetRequiredService<IGlobalExceptionHandler>();
    }

    /// <summary>
    /// Gets the error mapper from the service provider.
    /// </summary>
    /// <param name="provider">The service provider.</param>
    /// <returns>The error mapper.</returns>
    public static IErrorMapper GetBelayErrorMapper(this IServiceProvider provider) {
        return provider.GetRequiredService<IErrorMapper>();
    }
}

/// <summary>
/// Interface for exception handling configurator.
/// </summary>
internal interface IExceptionHandlingConfigurator {
    /// <summary>
    /// Configures the exception handling.
    /// </summary>
    void Configure();
}

/// <summary>
/// Exception handling configurator implementation.
/// </summary>
internal class ExceptionHandlingConfigurator : IExceptionHandlingConfigurator {
    private readonly IGlobalExceptionHandler handler;
    private readonly Action<ExceptionHandlingConfiguration> configure;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionHandlingConfigurator"/> class.
    /// </summary>
    /// <param name="handler">The global exception handler.</param>
    /// <param name="configure">The configuration action.</param>
    public ExceptionHandlingConfigurator(IGlobalExceptionHandler handler, Action<ExceptionHandlingConfiguration> configure) {
        this.handler = handler;
        this.configure = configure;
    }

    /// <inheritdoc/>
    public void Configure() {
        this.handler.ConfigureExceptionHandling(this.configure);
    }
}
