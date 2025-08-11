// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Extensions;

using Belay.Core.Exceptions;
using Belay.Core.Extensions;
using Belay.Core.Sessions;
using Belay.Extensions.Configuration;
using Belay.Extensions.Factories;
using Belay.Extensions.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Extension methods for registering Belay.NET services with dependency injection.
/// </summary>
/// <remarks>
/// <para>
/// These extensions provide multiple registration patterns for Belay.NET services,
/// supporting both simple registration and comprehensive enterprise configurations
/// with health checks, custom factories, and configuration binding.
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Basic Registration</strong></para>
/// <code>
/// // Program.cs or Startup.cs
/// public void ConfigureServices(IServiceCollection services)
/// {
///     // Simple registration with defaults
///     services.AddBelay();
///
///     // With custom configuration
///     services.AddBelay(options => {
///         options.DefaultTimeoutMs = 10000;
///         options.EnableCaching = true;
///         options.MaxConcurrentOperations = 4;
///     });
/// }
/// </code>
/// <para><strong>Configuration-based Registration</strong></para>
/// <code>
/// // appsettings.json
/// {
///   "Belay": {
///     "DefaultTimeoutMs": 15000,
///     "EnableCaching": true,
///     "DeviceDefaults": {
///       "SerialBaudRate": 115200,
///       "ConnectionRetries": 3
///     }
///   }
/// }
///
/// // Program.cs
/// public void ConfigureServices(IServiceCollection services)
/// {
///     services.AddBelay(Configuration, "Belay");
/// }
/// </code>
/// <para><strong>Full Enterprise Setup with Health Checks</strong></para>
/// <code>
/// public void ConfigureServices(IServiceCollection services)
/// {
///     // Register all Belay services
///     services.AddBelay(options => {
///         options.DefaultTimeoutMs = 30000;
///         options.EnableCaching = true;
///     });
///
///     // Add health checks for device monitoring
///     services.AddBelayHealthChecks(healthOptions => {
///         healthOptions.SystemHealthCheckTimeoutSeconds = 10;
///         healthOptions.AddDeviceCheck("primary-sensor", "COM3", timeoutSeconds: 5);
///         healthOptions.AddDeviceCheck("backup-sensor", "COM4", timeoutSeconds: 5);
///         healthOptions.AddDeviceCheck("test-subprocess", "micropython", timeoutSeconds: 15);
///     });
///
///     // Add custom executors
///     services.AddBelayExecutors();
/// }
///
/// // Usage in controllers/services
/// public class SensorController : ControllerBase
/// {
///     private readonly IDeviceFactory deviceFactory;
///
///     public SensorController(IDeviceFactory deviceFactory)
///     {
///         this.deviceFactory = deviceFactory;
///     }
///
///     [HttpGet("temperature")]
///     public async Task&lt;ActionResult&lt;float&gt;&gt; GetTemperature()
///     {
///         using var device = this.deviceFactory.CreateSerialDevice("COM3");
///         await device.ConnectAsync();
///
///         float temp = await device.ExecuteAsync&lt;float&gt;(@"
///             import machine
///             sensor = machine.ADC(machine.Pin(26))
///             reading = sensor.read_u16()
///             (reading * 3.3 / 65535) * 100
///         ");
///
///         return Ok(temp);
///     }
/// }
/// </code>
/// <para><strong>Background Service Integration</strong></para>
/// <code>
/// public class DeviceMonitoringService : BackgroundService
/// {
///     private readonly IServiceScopeFactory scopeFactory;
///     private readonly ILogger&lt;DeviceMonitoringService&gt; logger;
///
///     public DeviceMonitoringService(IServiceScopeFactory scopeFactory,
///         ILogger&lt;DeviceMonitoringService&gt; logger)
///     {
///         this.scopeFactory = scopeFactory;
///         this.logger = logger;
///     }
///
///     protected override async Task ExecuteAsync(CancellationToken stoppingToken)
///     {
///         while (!stoppingToken.IsCancellationRequested)
///         {
///             using var scope = this.scopeFactory.CreateScope();
///             var deviceFactory = scope.ServiceProvider.GetBelayDeviceFactory();
///
///             try
///             {
///                 using var device = deviceFactory.CreateSerialDevice("COM3");
///                 await device.ConnectAsync(stoppingToken);
///
///                 var reading = await device.ExecuteAsync&lt;float&gt;("read_sensors()", stoppingToken);
///                 this.logger.LogInformation("Sensor reading: {Reading}", reading);
///             }
///             catch (Exception ex)
///             {
///                 this.logger.LogError(ex, "Failed to read sensors");
///             }
///
///             await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
///         }
///     }
/// }
///
/// // Register background service
/// services.AddHostedService&lt;DeviceMonitoringService&gt;();
/// </code>
/// </example>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds all Belay.NET services to the service collection with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBelay(this IServiceCollection services) {
        return services.AddBelay(_ => { });
    }

    /// <summary>
    /// Adds all Belay.NET services to the service collection with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure Belay options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBelay(this IServiceCollection services, Action<BelayConfiguration> configure) {
        // Add configuration
        services.Configure(configure);

        // Add core services
        services.AddBelayCore();
        services.AddBelayFactories();
        services.AddBelayExceptionHandling();

        return services;
    }

    /// <summary>
    /// Adds all Belay.NET services to the service collection with IConfiguration binding.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="sectionName">The configuration section name (defaults to "Belay").</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBelay(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Belay") {
        // Bind configuration
        services.Configure<BelayConfiguration>(configuration.GetSection(sectionName));

        // Add core services
        services.AddBelayCore();
        services.AddBelayFactories();
        services.AddBelayExceptionHandling();

        return services;
    }

    /// <summary>
    /// Adds Belay.NET core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBelayCore(this IServiceCollection services) {
        // Session management - Scoped for proper isolation between requests
        services.AddScoped<IDeviceSessionManager, DeviceSessionManager>();
        services.AddSingleton<IResourceTracker, ResourceTracker>();

        // Device context and session state are scoped to match session lifecycle
        services.AddScoped<IDeviceContext, DeviceContext>();
        services.AddScoped<ISessionState, SessionState>();

        return services;
    }

    /// <summary>
    /// Adds Belay.NET factory services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBelayFactories(this IServiceCollection services) {
        services.AddSingleton<ICommunicatorFactory, CommunicatorFactory>();
        services.AddSingleton<IDeviceFactory, DeviceFactory>();
        services.AddSingleton<IExecutorFactory, ExecutorFactory>();

        return services;
    }

    /// <summary>
    /// Adds Belay.NET health checks to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureHealthChecks">Optional action to configure health check options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBelayHealthChecks(
        this IServiceCollection services,
        Action<BelayHealthCheckOptions>? configureHealthChecks = null) {
        var options = new BelayHealthCheckOptions();
        configureHealthChecks?.Invoke(options);

        services.AddHealthChecks()
            .AddCheck<BelayHealthCheck>(
                "belay_system",
                HealthStatus.Degraded,
                new[] { "belay", "system" },
                TimeSpan.FromSeconds(options.SystemHealthCheckTimeoutSeconds));

        // Add device connectivity health checks if specified
        foreach (var deviceCheck in options.DeviceHealthChecks) {
            services.AddHealthChecks()
                .Add(new HealthCheckRegistration(
                    deviceCheck.Name,
                    sp => new DeviceConnectivityHealthCheck(
                        sp.GetRequiredService<IDeviceFactory>(),
                        sp.GetRequiredService<ILogger<DeviceConnectivityHealthCheck>>(),
                        deviceCheck.PortOrPath),
                    HealthStatus.Degraded,
                    new[] { "belay", "device", "connectivity" },
                    TimeSpan.FromSeconds(deviceCheck.TimeoutSeconds)));
        }

        return services;
    }

    /// <summary>
    /// Adds Belay.NET executor services as singletons.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBelayExecutors(this IServiceCollection services) {
        // Note: Executors are registered as transient since they are tied to specific devices
        // Users typically get executors through the ExecutorFactory
        services.AddTransient<Belay.Core.Execution.TaskExecutor>();
        services.AddTransient<Belay.Core.Execution.SetupExecutor>();
        services.AddTransient<Belay.Core.Execution.TeardownExecutor>();
        services.AddTransient<Belay.Core.Execution.ThreadExecutor>();

        return services;
    }

    /// <summary>
    /// Creates a service scope and resolves a device factory.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>A device factory instance.</returns>
    public static IDeviceFactory GetBelayDeviceFactory(this IServiceProvider serviceProvider) {
        return serviceProvider.GetRequiredService<IDeviceFactory>();
    }

    /// <summary>
    /// Creates a service scope and resolves an executor factory.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>An executor factory instance.</returns>
    public static IExecutorFactory GetBelayExecutorFactory(this IServiceProvider serviceProvider) {
        return serviceProvider.GetRequiredService<IExecutorFactory>();
    }
}

/// <summary>
/// Options for configuring Belay.NET health checks.
/// </summary>
public class BelayHealthCheckOptions {
    /// <summary>
    /// Gets or sets the system health check timeout in seconds.
    /// </summary>
    public int SystemHealthCheckTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Gets the list of device health checks to register.
    /// </summary>
    public List<DeviceHealthCheckConfiguration> DeviceHealthChecks { get; } = new();

    /// <summary>
    /// Adds a device connectivity health check.
    /// </summary>
    /// <param name="name">The name of the health check.</param>
    /// <param name="portOrPath">The port name or executable path to test.</param>
    /// <param name="timeoutSeconds">The timeout in seconds (default 10).</param>
    /// <returns>The options instance for chaining.</returns>
    public BelayHealthCheckOptions AddDeviceCheck(string name, string portOrPath, int timeoutSeconds = 10) {
        this.DeviceHealthChecks.Add(new DeviceHealthCheckConfiguration {
            Name = name,
            PortOrPath = portOrPath,
            TimeoutSeconds = timeoutSeconds,
        });
        return this;
    }
}

/// <summary>
/// Configuration for a device health check.
/// </summary>
public class DeviceHealthCheckConfiguration {
    /// <summary>
    /// Gets or sets the name of the health check.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the port name or executable path to test.
    /// </summary>
    public string PortOrPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}
