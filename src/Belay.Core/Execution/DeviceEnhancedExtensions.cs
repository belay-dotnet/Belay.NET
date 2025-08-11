// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System;
    using System.Collections.Concurrent;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Belay.Core.Communication;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Extensions to the Device class for enhanced executor support.
    /// </summary>
    public static class DeviceEnhancedExtensions {
        private static readonly ConditionalWeakTable<Device, IEnhancedExecutor> EnhancedExecutorCache = new();
        private static readonly ConditionalWeakTable<Device, ConcurrentDictionary<string, object>> ProxyCache = new();

        /// <summary>
        /// Gets or creates an enhanced executor for this device.
        /// The enhanced executor provides advanced method interception and pipeline processing.
        /// </summary>
        /// <param name="device">The device to get the enhanced executor for.</param>
        /// <param name="logger">Optional logger for the enhanced executor.</param>
        /// <returns>An enhanced executor instance for this device.</returns>
        public static IEnhancedExecutor GetEnhancedExecutor(this Device device, ILogger? logger = null) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            return EnhancedExecutorCache.GetValue(device, d => {
                var enhancedLogger = (logger as ILogger<EnhancedExecutor>) ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EnhancedExecutor>.Instance;
                var executor = new EnhancedExecutor(
                    d,
                    d.Sessions,
                    enhancedLogger);

                // Log creation
                enhancedLogger.LogDebug("Created enhanced executor for device {DeviceType}", d.GetType().Name);

                return executor;
            });
        }

        /// <summary>
        /// Creates a device proxy that automatically routes method calls through the enhanced executor.
        /// This enables seamless attribute-based programming where C# methods are executed on MicroPython devices.
        /// </summary>
        /// <typeparam name="T">The interface or abstract class to proxy.</typeparam>
        /// <param name="device">The device to create the proxy for.</param>
        /// <param name="logger">Optional logger for proxy operations.</param>
        /// <returns>A proxy instance that routes method calls to the device.</returns>
        public static T CreateProxy<T>(this Device device, ILogger? logger = null)
            where T : class {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            var deviceProxyCache = ProxyCache.GetValue(device, _ => new ConcurrentDictionary<string, object>());
            var cacheKey = typeof(T).FullName ?? typeof(T).Name;
            return (T)deviceProxyCache.GetOrAdd(cacheKey, _ => DeviceProxyFactory.CreateProxy<T>(device, logger));
        }

        /// <summary>
        /// Creates a device proxy with a custom enhanced executor.
        /// </summary>
        /// <typeparam name="T">The interface or abstract class to proxy.</typeparam>
        /// <param name="device">The device to create the proxy for.</param>
        /// <param name="executor">The enhanced executor to use for method execution.</param>
        /// <param name="logger">Optional logger for proxy operations.</param>
        /// <returns>A proxy instance that routes method calls through the executor.</returns>
        public static T CreateProxyWithExecutor<T>(this Device device, IEnhancedExecutor executor, ILogger? logger = null)
            where T : class {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            if (executor == null) {
                throw new ArgumentNullException(nameof(executor));
            }

            return DeviceProxyFactory.CreateProxyWithExecutor<T>(executor, logger);
        }

        /// <summary>
        /// Checks if a type can be proxied for this device.
        /// </summary>
        /// <param name="device">The device to check proxy compatibility for.</param>
        /// <param name="type">The type to validate for proxying.</param>
        /// <returns>True if the type can be proxied, false otherwise.</returns>
        public static bool CanProxy(this Device device, Type type) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            return DeviceProxyFactory.CanProxy(type);
        }

        /// <summary>
        /// Clears all cached enhanced executors and proxies for performance or testing purposes.
        /// Note: With ConditionalWeakTable, this method has limited effect as entries are automatically
        /// cleaned up when devices are garbage collected.
        /// </summary>
        public static void ClearEnhancedExecutorCache() {
            // ConditionalWeakTable doesn't provide enumeration or clear methods for security reasons.
            // The cache will automatically clean up when devices are garbage collected.
            // For testing purposes, you may need to explicitly dispose devices and force GC.
        }

        /// <summary>
        /// Gets enhanced execution statistics for this device.
        /// </summary>
        /// <param name="device">The device to get statistics for.</param>
        /// <returns>Enhanced execution statistics, or null if no enhanced executor exists.</returns>
        public static EnhancedExecutionStatistics? GetEnhancedExecutionStatistics(this Device device) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            if (EnhancedExecutorCache.TryGetValue(device, out var executor)) {
                return executor.GetExecutionStatistics();
            }

            return null;
        }

        /// <summary>
        /// Clears the enhanced execution cache for this device.
        /// </summary>
        /// <param name="device">The device to clear the cache for.</param>
        public static void ClearEnhancedExecutionCache(this Device device) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            if (EnhancedExecutorCache.TryGetValue(device, out var executor)) {
                executor.ClearExecutionCache();
            }
        }
    }

    /// <summary>
    /// Enhanced device interface that provides access to advanced execution capabilities.
    /// Use this interface when you need the enhanced executor features directly.
    /// </summary>
    public interface IEnhancedDevice {
        /// <summary>
        /// Gets the enhanced executor for this device.
        /// </summary>
        IEnhancedExecutor EnhancedExecutor { get; }

        /// <summary>
        /// Creates a proxy for the specified interface type.
        /// </summary>
        /// <typeparam name="T">The interface type to proxy.</typeparam>
        /// <returns>A proxy instance that routes method calls to the device.</returns>
        T CreateProxy<T>()
            where T : class;

        /// <summary>
        /// Gets enhanced execution statistics.
        /// </summary>
        /// <returns>Enhanced execution statistics.</returns>
        EnhancedExecutionStatistics GetExecutionStatistics();
    }

    /// <summary>
    /// Enhanced device wrapper that provides access to advanced execution capabilities.
    /// This wrapper can be used when you need the enhanced features as first-class citizens.
    /// </summary>
    public class EnhancedDevice : IEnhancedDevice, IDisposable {
        private readonly Device device;
        private readonly IEnhancedExecutor enhancedExecutor;
        private readonly ILogger logger;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnhancedDevice"/> class.
        /// </summary>
        /// <param name="device">The underlying device to wrap.</param>
        /// <param name="logger">Optional logger for enhanced operations.</param>
        public EnhancedDevice(Device device, ILogger? logger = null) {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EnhancedDevice>.Instance;
            this.enhancedExecutor = device.GetEnhancedExecutor(this.logger);
        }

        /// <summary>
        /// Gets the underlying device.
        /// </summary>
        public Device UnderlyingDevice => this.device;

        /// <inheritdoc />
        public IEnhancedExecutor EnhancedExecutor => this.enhancedExecutor;

        /// <inheritdoc />
        public T CreateProxy<T>()
            where T : class {
            this.ThrowIfDisposed();
            return this.device.CreateProxy<T>(this.logger);
        }

        /// <inheritdoc />
        public EnhancedExecutionStatistics GetExecutionStatistics() {
            this.ThrowIfDisposed();
            return this.enhancedExecutor.GetExecutionStatistics();
        }

        /// <summary>
        /// Connects to the MicroPython device.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task ConnectAsync(CancellationToken cancellationToken = default) {
            this.ThrowIfDisposed();
            return this.device.ConnectAsync(cancellationToken);
        }

        /// <summary>
        /// Disconnects from the MicroPython device.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public Task DisconnectAsync(CancellationToken cancellationToken = default) {
            this.ThrowIfDisposed();
            return this.device.DisconnectAsync(cancellationToken);
        }

        /// <summary>
        /// Gets the current connection state of the device.
        /// </summary>
        public DeviceConnectionState State => this.device.State;

        /// <inheritdoc />
        public void Dispose() {
            if (!this.disposed) {
                this.enhancedExecutor.ClearExecutionCache();
                this.logger.LogDebug("Enhanced device wrapper disposed");
                this.disposed = true;
            }
        }

        private void ThrowIfDisposed() {
            if (this.disposed) {
                throw new ObjectDisposedException(nameof(EnhancedDevice));
            }
        }
    }
}
