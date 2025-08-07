// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    using Belay.Core.Communication;

    /// <summary>
    /// Provides device-specific context information within a session.
    /// </summary>
    public interface IDeviceContext {
        /// <summary>
        /// Gets the session identifier associated with this context.
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// Gets the device communication state at the time of session creation.
        /// </summary>
        DeviceConnectionState ConnectionState { get; }

        /// <summary>
        /// Gets device capabilities and information, if available.
        /// </summary>
        IDeviceInfo? DeviceInfo { get; }

        /// <summary>
        /// Gets the current device communication instance.
        /// </summary>
        IDeviceCommunication Communication { get; }

        /// <summary>
        /// Gets device-specific configuration for this session.
        /// </summary>
        /// <typeparam name="T">The type of configuration to retrieve.</typeparam>
        /// <param name="key">The configuration key.</param>
        /// <param name="defaultValue">The default value to return if not found.</param>
        /// <returns>The configuration value, or the default value if not found.</returns>
        T GetConfiguration<T>(string key, T defaultValue = default!);

        /// <summary>
        /// Sets device-specific configuration for this session.
        /// </summary>
        /// <typeparam name="T">The type of configuration to store.</typeparam>
        /// <param name="key">The configuration key.</param>
        /// <param name="value">The configuration value to store.</param>
        void SetConfiguration<T>(string key, T value);

        /// <summary>
        /// Gets performance metrics for device operations in this session.
        /// </summary>
        /// <param name="metricName">The name of the metric to retrieve.</param>
        /// <returns>The metric value, or null if the metric is not available.</returns>
        double? GetMetric(string metricName);

        /// <summary>
        /// Records a performance metric for device operations in this session.
        /// </summary>
        /// <param name="metricName">The name of the metric to record.</param>
        /// <param name="value">The metric value to record.</param>
        void RecordMetric(string metricName, double value);

        /// <summary>
        /// Gets all available performance metric names for this session.
        /// </summary>
        /// <returns>A collection of available performance metric names.</returns>
        IReadOnlyCollection<string> AvailableMetrics { get; }
    }

    /// <summary>
    /// Provides basic device information and capabilities.
    /// </summary>
    public interface IDeviceInfo {
        /// <summary>
        /// Gets the device platform (e.g., "micropython", "circuitpython").
        /// </summary>
        string Platform { get; }

        /// <summary>
        /// Gets the device platform version.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets the device hardware identifier.
        /// </summary>
        string? Hardware { get; }

        /// <summary>
        /// Gets the device's unique identifier, if available.
        /// </summary>
        string? UniqueId { get; }

        /// <summary>
        /// Gets a value indicating whether the device supports threading.
        /// </summary>
        bool SupportsThreading { get; }

        /// <summary>
        /// Gets a value indicating whether the device supports file operations.
        /// </summary>
        bool SupportsFileSystem { get; }

        /// <summary>
        /// Gets the available memory on the device, if known.
        /// </summary>
        long? AvailableMemory { get; }
    }
}
