// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Extensions.HealthChecks;

using Belay.Extensions.Factories;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Health check for Belay.NET system components.
/// </summary>
public class BelayHealthCheck : IHealthCheck {
    private readonly IDeviceFactory _deviceFactory;
    private readonly ILogger<BelayHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BelayHealthCheck"/> class.
    /// </summary>
    /// <param name="deviceFactory">The device factory.</param>
    /// <param name="logger">The logger.</param>
    public BelayHealthCheck(
        IDeviceFactory deviceFactory,
        ILogger<BelayHealthCheck> logger) {
        this._deviceFactory = deviceFactory;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        try {
            var data = new Dictionary<string, object>();
            var warnings = new List<string>();

            // Check device factory
            if (this.CheckDeviceFactory(data, warnings)) {
                data["device_factory"] = "healthy";
            }
            else {
                warnings.Add("Device factory check failed");
                data["device_factory"] = "degraded";
            }

            data["check_timestamp"] = DateTimeOffset.UtcNow;
            data["warnings"] = warnings.ToArray();
            data["architecture"] = "simplified"; // Indicate we're using simplified architecture

            if (warnings.Count == 0) {
                this._logger.LogDebug("Belay health check passed");
                return Task.FromResult(HealthCheckResult.Healthy("Belay.NET components are healthy", data));
            }

            this._logger.LogWarning("Belay health check passed with warnings: {Warnings}", string.Join(", ", warnings));
            return Task.FromResult(HealthCheckResult.Degraded("Belay.NET components are degraded", null, data));
        }
        catch (Exception ex) {
            this._logger.LogError(ex, "Belay health check threw an exception");
            return Task.FromResult(HealthCheckResult.Unhealthy("Belay.NET health check failed with exception", ex));
        }
    }

    private bool CheckDeviceFactory(Dictionary<string, object> data, List<string> warnings) {
        _ = warnings; // Parameter reserved for future use
        try {
            // Basic factory availability check
            data["device_factory_available"] = this._deviceFactory != null;
            return this._deviceFactory != null;
        }
        catch (Exception ex) {
            this._logger.LogWarning(ex, "Device factory health check failed");
            data["device_factory_error"] = ex.Message;
            return false;
        }
    }
}

/// <summary>
/// Health check for device connectivity.
/// </summary>
public class DeviceConnectivityHealthCheck : IHealthCheck {
    private readonly IDeviceFactory _deviceFactory;
    private readonly ILogger<DeviceConnectivityHealthCheck> _logger;
    private readonly string _testPortOrPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceConnectivityHealthCheck"/> class.
    /// </summary>
    /// <param name="deviceFactory">The device factory.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="testPortOrPath">The test port name or executable path for connectivity test.</param>
    public DeviceConnectivityHealthCheck(
        IDeviceFactory deviceFactory,
        ILogger<DeviceConnectivityHealthCheck> logger,
        string testPortOrPath) {
        this._deviceFactory = deviceFactory;
        this._logger = logger;
        this._testPortOrPath = testPortOrPath;
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        try {
            var data = new Dictionary<string, object> {
                ["test_target"] = this._testPortOrPath,
                ["check_timestamp"] = DateTimeOffset.UtcNow,
            };

            // Determine if this is a serial port or executable path
            var isSerialPort = this._testPortOrPath.StartsWith("COM") || this._testPortOrPath.StartsWith("/dev/");

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var device = isSerialPort
                ? this._deviceFactory.CreateSerialDevice(this._testPortOrPath)
                : this._deviceFactory.CreateSubprocessDevice(this._testPortOrPath);

            try {
                // Attempt a simple connectivity test
                await device.ExecuteAsync("print('health_check')", combinedCts.Token).ConfigureAwait(false);

                data["connectivity"] = "healthy";
                data["connection_type"] = isSerialPort ? "serial" : "subprocess";

                this._logger.LogDebug("Device connectivity check passed for {Target}", this._testPortOrPath);
                return HealthCheckResult.Healthy($"Device {this._testPortOrPath} is accessible", data);
            }
            catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested) {
                data["connectivity"] = "timeout";
                this._logger.LogWarning("Device connectivity check timed out for {Target}", this._testPortOrPath);
                return HealthCheckResult.Degraded($"Device {this._testPortOrPath} connection timed out", null, data);
            }
            catch (Exception ex) {
                data["connectivity"] = "failed";
                data["error"] = ex.Message;
                this._logger.LogWarning(ex, "Device connectivity check failed for {Target}", this._testPortOrPath);
                return HealthCheckResult.Degraded($"Device {this._testPortOrPath} is not accessible", ex, data);
            }
        }
        catch (Exception ex) {
            this._logger.LogError(ex, "Device connectivity health check threw an exception");
            return HealthCheckResult.Unhealthy("Device connectivity health check failed with exception", ex);
        }
    }
}
