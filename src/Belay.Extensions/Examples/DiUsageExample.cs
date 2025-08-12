// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Extensions.Examples;

using Belay.Extensions.Configuration;
using Belay.Extensions.Factories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Example showing how to use Belay.NET with dependency injection.
/// </summary>
public static class DiUsageExample {
    /// <summary>
    /// Example of setting up Belay.NET with dependency injection in a console application.
    /// Note: In a real application you would add logging and use a proper hosting framework.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunConsoleExample() {
        var services = new ServiceCollection();

        // Add Belay.NET services with programmatic configuration
        services.AddBelay(config => {
            config.Device.DefaultConnectionTimeoutMs = 5000;
            config.Communication.Serial.DefaultBaudRate = 115200;
        });

        // Add health checks
        services.AddBelayHealthChecks(options => {
            options.AddDeviceCheck("test_device", "subprocess:micropython");
        });

        // Add your application services
        services.AddScoped<IMyDeviceService, MyDeviceService>();

        // Build service provider
        var serviceProvider = services.BuildServiceProvider();

        // Use the services
        var deviceService = serviceProvider.GetRequiredService<IMyDeviceService>();
        await deviceService.RunDeviceOperationsAsync();
    }

    /// <summary>
    /// Example of setting up Belay.NET with ASP.NET Core.
    /// Note: Requires Microsoft.Extensions.Hosting and Microsoft.AspNetCore.App packages.
    /// </summary>
    /// <param name="services">The service collection from your ASP.NET Core application.</param>
    /// <param name="configuration">The configuration from your ASP.NET Core application.</param>
    public static void ConfigureWebApplication(IServiceCollection services, IConfiguration configuration) {
        // Add Belay.NET services
        services.AddBelay(config => {
            config.Device.DefaultConnectionTimeoutMs = 10000;
            config.Communication.Serial.DefaultBaudRate = 115200;
        });

        // Add health checks with device connectivity tests
        services.AddBelayHealthChecks(options => {
            options.AddDeviceCheck("primary_device", "serial:COM3");
            options.AddDeviceCheck("test_device", "subprocess:micropython");
        });
    }

    /// <summary>
    /// Example service that uses Belay.NET factories through dependency injection.
    /// </summary>
    public interface IMyDeviceService {
        /// <summary>
        /// Run some operations on a device.
        /// </summary>
        /// <returns>A task representing the operation.</returns>
        Task RunDeviceOperationsAsync();
    }

    /// <summary>
    /// Implementation of device service using DI.
    /// </summary>
    public class MyDeviceService : IMyDeviceService {
        private readonly IDeviceFactory _deviceFactory;
        private readonly IExecutorFactory _executorFactory;
        private readonly ILogger<MyDeviceService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MyDeviceService"/> class.
        /// </summary>
        /// <param name="deviceFactory">Factory for creating devices.</param>
        /// <param name="executorFactory">Factory for creating executors.</param>
        /// <param name="logger">Logger instance.</param>
        public MyDeviceService(
            IDeviceFactory deviceFactory,
            IExecutorFactory executorFactory,
            ILogger<MyDeviceService> logger) {
            this._deviceFactory = deviceFactory;
            this._executorFactory = executorFactory;
            this._logger = logger;
        }

        /// <inheritdoc/>
        public async Task RunDeviceOperationsAsync() {
            this._logger.LogInformation("Starting device operations");

            // Create a device using the factory
            using var device = this._deviceFactory.CreateSubprocessDevice("micropython");
            try {
                await device.ConnectAsync();

                // Execute some Python code
                var result = await device.ExecuteAsync<int>("2 + 3");
                this._logger.LogInformation("Calculation result: {Result}", result);

                // Get executors using the factory (though in the simplified architecture,
                // you can also directly use device.Task, device.Setup, etc.)
                var taskExecutor = this._executorFactory.GetTaskExecutor(device);
                this._logger.LogDebug("Task executor type: {ExecutorType}", taskExecutor.GetType().Name);

                // Note: In the simplified architecture, you typically use Device methods directly:
                var complexResult = await device.ExecuteAsync<string>("import sys; sys.version");
                this._logger.LogInformation("Python version: {Version}", complexResult);
            }
            catch (Exception ex) {
                this._logger.LogError(ex, "Error during device operations");
            }
            finally {
                await device.DisconnectAsync();
            }
        }
    }
}

/// <summary>
/// Example configuration for appsettings.json.
/// </summary>
public static class ConfigurationExample {
    /// <summary>
    /// Example JSON configuration that can be used in appsettings.json.
    /// </summary>
    public const string ExampleAppSettings = """
    {
      "Belay": {
        "Device": {
          "DefaultConnectionTimeoutMs": 5000,
          "DefaultCommandTimeoutMs": 30000,
          "Discovery": {
            "EnableAutoDiscovery": true,
            "DiscoveryTimeoutMs": 10000,
            "SerialPortPatterns": [ "COM*", "/dev/ttyUSB*", "/dev/ttyACM*" ]
          },
          "Retry": {
            "MaxRetries": 3,
            "InitialRetryDelayMs": 1000,
            "BackoffMultiplier": 2.0,
            "MaxRetryDelayMs": 30000
          }
        },
        "Communication": {
          "Serial": {
            "DefaultBaudRate": 115200,
            "ReadTimeoutMs": 1000,
            "WriteTimeoutMs": 1000
          },
          "RawRepl": {
            "InitializationTimeoutMs": 2000,
            "WindowSize": 256,
            "MaxRetries": 3
          }
        },
        "Executor": {
          "DefaultTaskTimeoutMs": 30000,
          "MaxCacheSize": 1000,
          "EnableCachingByDefault": false,
          "CacheExpirationMs": 600000
        },
        "ExceptionHandling": {
          "RethrowExceptions": true,
          "LogExceptions": true,
          "IncludeStackTraces": true,
          "ExceptionLogLevel": "Error",
          "PreserveContext": true,
          "MaxContextEntries": 50
        }
      }
    }
    """;
}
