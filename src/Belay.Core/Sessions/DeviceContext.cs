// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    using System.Collections.Concurrent;
    using Belay.Core.Communication;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Basic device information implementation.
    /// </summary>
    public sealed class DeviceInfo : IDeviceInfo {
        /// <inheritdoc />
        public required string Platform { get; init; }

        /// <inheritdoc />
        public required string Version { get; init; }

        /// <inheritdoc />
        public string? Hardware { get; init; }

        /// <inheritdoc />
        public string? UniqueId { get; init; }

        /// <inheritdoc />
        public bool SupportsThreading { get; init; }

        /// <inheritdoc />
        public bool SupportsFileSystem { get; init; }

        /// <inheritdoc />
        public long? AvailableMemory { get; init; }
    }

    /// <summary>
    /// Provides device-specific context information within a session.
    /// </summary>
    public sealed class DeviceContext : IDeviceContext {
        private readonly ILogger<DeviceContext> logger;
        private readonly ConcurrentDictionary<string, object?> configuration = new();
        private readonly ConcurrentDictionary<string, double> metrics = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceContext"/> class.
        /// </summary>
        /// <param name="sessionId">The identifier of the session this context belongs to.</param>
        /// <param name="communication">The device communication instance.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        /// <param name="deviceInfo">Optional device information.</param>
        public DeviceContext(
            string sessionId,
            IDeviceCommunication communication,
            ILogger<DeviceContext> logger,
            IDeviceInfo? deviceInfo = null) {
            this.SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            this.Communication = communication ?? throw new ArgumentNullException(nameof(communication));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.DeviceInfo = deviceInfo;
            this.ConnectionState = communication.State;

            // Initialize basic metrics
            this.metrics.TryAdd("session_created_at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        /// <inheritdoc />
        public string SessionId { get; }

        /// <inheritdoc />
        public DeviceConnectionState ConnectionState { get; }

        /// <inheritdoc />
        public IDeviceInfo? DeviceInfo { get; }

        /// <inheritdoc />
        public IDeviceCommunication Communication { get; }

        /// <inheritdoc />
        public T GetConfiguration<T>(string key, T defaultValue = default!) {
            if (string.IsNullOrWhiteSpace(key)) {
                return defaultValue;
            }

            return this.configuration.TryGetValue(key, out var value) && value is T typedValue
                ? typedValue
                : defaultValue;
        }

        /// <inheritdoc />
        public void SetConfiguration<T>(string key, T value) {
            if (string.IsNullOrWhiteSpace(key)) {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            this.configuration.AddOrUpdate(key, value, (k, v) => value);

            this.logger.LogDebug(
                "Set configuration {Key} in session {SessionId}",
                key,
                this.SessionId);
        }

        /// <inheritdoc />
        public double? GetMetric(string metricName) {
            if (string.IsNullOrWhiteSpace(metricName)) {
                return null;
            }

            return this.metrics.TryGetValue(metricName, out var value) ? value : null;
        }

        /// <inheritdoc />
        public void RecordMetric(string metricName, double value) {
            if (string.IsNullOrWhiteSpace(metricName)) {
                throw new ArgumentException("Metric name cannot be null or whitespace", nameof(metricName));
            }

            this.metrics.AddOrUpdate(metricName, value, (k, v) => value);

            this.logger.LogDebug(
                "Recorded metric {MetricName} = {Value} in session {SessionId}",
                metricName,
                value,
                this.SessionId);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<string> AvailableMetrics => this.metrics.Keys.ToArray();
    }
}
