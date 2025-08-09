// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    /// <summary>
    /// Represents device capabilities and features that can be detected and tracked.
    /// </summary>
    public interface IDeviceCapabilities {
        /// <summary>
        /// Gets the MicroPython firmware version.
        /// </summary>
        string? FirmwareVersion { get; }

        /// <summary>
        /// Gets the device type (platform name).
        /// </summary>
        string? DeviceType { get; }

        /// <summary>
        /// Gets the set of features supported by the device.
        /// </summary>
        DeviceFeatureSet SupportedFeatures { get; }

        /// <summary>
        /// Gets the performance profile of the device.
        /// </summary>
        DevicePerformanceProfile PerformanceProfile { get; }

        /// <summary>
        /// Gets a value indicating whether capability detection has completed.
        /// </summary>
        bool IsDetectionComplete { get; }

        /// <summary>
        /// Gets the unique device identifier, if available.
        /// </summary>
        string? UniqueDeviceId { get; }

        /// <summary>
        /// Determines if the device supports a specific feature.
        /// </summary>
        /// <param name="feature">The feature to check.</param>
        /// <returns>True if the feature is supported, false otherwise.</returns>
        bool SupportsFeature(DeviceFeature feature);

        /// <summary>
        /// Refreshes device capabilities by querying the device.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RefreshCapabilitiesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets memory information from the device.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>Device memory information.</returns>
        Task<DeviceMemoryInfo> GetMemoryInfoAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents a set of device features.
    /// </summary>
    [Flags]
    public enum DeviceFeatureSet {
        /// <summary>
        /// No features detected.
        /// </summary>
        None = 0,

        /// <summary>
        /// Basic GPIO pin control.
        /// </summary>
        GPIO = 1 << 0,

        /// <summary>
        /// Analog-to-Digital Converter.
        /// </summary>
        ADC = 1 << 1,

        /// <summary>
        /// Pulse Width Modulation.
        /// </summary>
        PWM = 1 << 2,

        /// <summary>
        /// I2C communication protocol.
        /// </summary>
        I2C = 1 << 3,

        /// <summary>
        /// SPI communication protocol.
        /// </summary>
        SPI = 1 << 4,

        /// <summary>
        /// Hardware timers.
        /// </summary>
        Timer = 1 << 5,

        /// <summary>
        /// Real-time clock.
        /// </summary>
        RTC = 1 << 6,

        /// <summary>
        /// Threading support.
        /// </summary>
        Threading = 1 << 7,

        /// <summary>
        /// File system access.
        /// </summary>
        FileSystem = 1 << 8,

        /// <summary>
        /// WiFi connectivity.
        /// </summary>
        WiFi = 1 << 9,

        /// <summary>
        /// Bluetooth connectivity.
        /// </summary>
        Bluetooth = 1 << 10,

        /// <summary>
        /// Hardware cryptographic acceleration.
        /// </summary>
        CryptoAccel = 1 << 11,

        /// <summary>
        /// Touch sensor support.
        /// </summary>
        TouchSensor = 1 << 12,

        /// <summary>
        /// Display controller.
        /// </summary>
        Display = 1 << 13,

        /// <summary>
        /// Audio processing capabilities.
        /// </summary>
        Audio = 1 << 14,
    }

    /// <summary>
    /// Individual device features that can be tested.
    /// </summary>
    public enum DeviceFeature {
        /// <summary>
        /// GPIO pin control.
        /// </summary>
        GPIO,

        /// <summary>
        /// Analog-to-Digital Converter.
        /// </summary>
        ADC,

        /// <summary>
        /// Pulse Width Modulation.
        /// </summary>
        PWM,

        /// <summary>
        /// I2C communication.
        /// </summary>
        I2C,

        /// <summary>
        /// SPI communication.
        /// </summary>
        SPI,

        /// <summary>
        /// Hardware timers.
        /// </summary>
        Timer,

        /// <summary>
        /// Real-time clock.
        /// </summary>
        RTC,

        /// <summary>
        /// Threading support.
        /// </summary>
        Threading,

        /// <summary>
        /// File system access.
        /// </summary>
        FileSystem,

        /// <summary>
        /// WiFi connectivity.
        /// </summary>
        WiFi,

        /// <summary>
        /// Bluetooth connectivity.
        /// </summary>
        Bluetooth,

        /// <summary>
        /// Hardware crypto acceleration.
        /// </summary>
        CryptoAccel,

        /// <summary>
        /// Touch sensor support.
        /// </summary>
        TouchSensor,

        /// <summary>
        /// Display controller.
        /// </summary>
        Display,

        /// <summary>
        /// Audio processing.
        /// </summary>
        Audio,
    }

    /// <summary>
    /// Performance characteristics of a device.
    /// </summary>
    public record DevicePerformanceProfile {
        /// <summary>
        /// Gets the estimated CPU speed in MHz.
        /// </summary>
        public int EstimatedCpuSpeedMhz { get; init; }

        /// <summary>
        /// Gets the available RAM in bytes.
        /// </summary>
        public int AvailableRamBytes { get; init; }

        /// <summary>
        /// Gets the flash storage size in bytes.
        /// </summary>
        public int FlashStorageBytes { get; init; }

        /// <summary>
        /// Gets the estimated performance tier.
        /// </summary>
        public DevicePerformanceTier PerformanceTier { get; init; }

        /// <summary>
        /// Gets a value indicating whether the device has floating-point unit.
        /// </summary>
        public bool HasFloatingPointUnit { get; init; }

        /// <summary>
        /// Gets benchmark results if available.
        /// </summary>
        public Dictionary<string, double> BenchmarkResults { get; init; } = new();
    }

    /// <summary>
    /// Device performance tiers for optimization decisions.
    /// </summary>
    public enum DevicePerformanceTier {
        /// <summary>
        /// Low-end device with limited resources.
        /// </summary>
        Low,

        /// <summary>
        /// Mid-range device with moderate resources.
        /// </summary>
        Medium,

        /// <summary>
        /// High-end device with ample resources.
        /// </summary>
        High,

        /// <summary>
        /// Unknown performance characteristics.
        /// </summary>
        Unknown,
    }

    /// <summary>
    /// Device memory information.
    /// </summary>
    public record DeviceMemoryInfo {
        /// <summary>
        /// Gets the total memory available.
        /// </summary>
        public int TotalBytes { get; init; }

        /// <summary>
        /// Gets the currently free memory.
        /// </summary>
        public int FreeBytes { get; init; }

        /// <summary>
        /// Gets the allocated memory.
        /// </summary>
        public int AllocatedBytes { get; init; }

        /// <summary>
        /// Gets the memory utilization percentage.
        /// </summary>
        public double UtilizationPercent => this.TotalBytes > 0 ? (double)this.AllocatedBytes / this.TotalBytes * 100.0 : 0.0;

        /// <summary>
        /// Gets the timestamp when this information was collected.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
