// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core {
    using System;
    using Belay.Core.Communication;

    /// <summary>
    /// Simple state tracking for MicroPython device operations.
    /// Replaces complex session management with lightweight state tracking
    /// aligned with single-threaded MicroPython device constraints.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides minimal state tracking for MicroPython devices without
    /// the complexity of session management. It tracks basic device capabilities,
    /// current operations for debugging, and timing information for monitoring.
    /// </para>
    /// <para>
    /// Unlike the previous session management system, this approach:
    /// <list type="bullet">
    /// <item><description>Eliminates race conditions from concurrent session creation</description></item>
    /// <item><description>Reduces memory overhead from session tracking</description></item>
    /// <item><description>Provides direct device operations without session indirection</description></item>
    /// <item><description>Aligns with single-threaded MicroPython device reality</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <para><strong>Basic Usage</strong></para>
    /// <code>
    /// using var device = Device.FromConnectionString("subprocess:micropython");
    /// await device.ConnectAsync();
    ///
    /// // Check device state
    /// Console.WriteLine($"Platform: {device.State.Capabilities?.Platform}");
    /// Console.WriteLine($"Features: {device.State.Capabilities?.SupportedFeatures}");
    /// Console.WriteLine($"Connection: {device.State.ConnectionState}");
    /// </code>
    /// </example>
    public sealed class DeviceState {
        /// <summary>
        /// Gets or sets the detected device capabilities.
        /// </summary>
        /// <value>
        /// The device capabilities including platform information and supported features.
        /// Null until first capability detection is performed during connection.
        /// </value>
        public SimpleDeviceCapabilities? Capabilities { get; set; }

        /// <summary>
        /// Gets the name of the current operation being executed.
        /// </summary>
        /// <value>
        /// The name of the operation currently being executed on the device.
        /// Used for debugging and error context. Null when the device is idle.
        /// </value>
        /// <remarks>
        /// This property is primarily used for diagnostic purposes and error reporting.
        /// It allows developers to understand what operation was in progress when
        /// an error occurred or when examining device state.
        /// </remarks>
        public string? CurrentOperation { get; private set; }

        /// <summary>
        /// Gets the timestamp of the last completed operation.
        /// </summary>
        /// <value>
        /// The UTC timestamp when the last operation completed successfully.
        /// Used for monitoring and diagnostics. Null if no operations have completed.
        /// </value>
        public DateTime? LastOperationTime { get; private set; }

        /// <summary>
        /// Gets the current connection state of the device.
        /// </summary>
        /// <value>The current connection state from the communication layer.</value>
        public DeviceConnectionState ConnectionState { get; internal set; } = DeviceConnectionState.Disconnected;

        /// <summary>
        /// Sets the current operation and updates internal tracking.
        /// </summary>
        /// <param name="operationName">The name of the operation being started.</param>
        /// <remarks>
        /// This method is called internally by the Device class to track the current
        /// operation for debugging and error reporting purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// device.State.SetCurrentOperation("ExecutePythonCode");
        /// // ... perform operation ...
        /// device.State.CompleteOperation();
        /// </code>
        /// </example>
        public void SetCurrentOperation(string? operationName) {
            this.CurrentOperation = operationName;
        }

        /// <summary>
        /// Marks an operation as completed and updates timing information.
        /// </summary>
        /// <remarks>
        /// This method clears the current operation and records the completion time
        /// for monitoring and diagnostic purposes.
        /// </remarks>
        public void CompleteOperation() {
            this.CurrentOperation = null;
            this.LastOperationTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Clears all execution history and operation tracking.
        /// </summary>
        /// <remarks>
        /// This method resets both current operation and last operation time,
        /// typically used when clearing cache or resetting device state.
        /// </remarks>
        public void ClearExecutionHistory() {
            this.CurrentOperation = null;
            this.LastOperationTime = null;
        }

        /// <summary>
        /// Gets a summary of the current device state.
        /// </summary>
        /// <returns>A formatted string containing key state information.</returns>
        public override string ToString() {
            var operation = this.CurrentOperation != null ? $"Operation: {this.CurrentOperation}" : "Idle";
            var platform = this.Capabilities?.Platform ?? "Unknown";
            return $"DeviceState [{this.ConnectionState}] Platform: {platform}, {operation}";
        }
    }

    /// <summary>
    /// Simplified device capabilities with efficient batched detection.
    /// Replaces sequential capability detection with single optimized call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class provides device capability information detected through a single
    /// batched script execution, eliminating the performance issues of the previous
    /// sequential detection approach (14+ separate import attempts).
    /// </para>
    /// <para>
    /// Performance improvements:
    /// <list type="bullet">
    /// <item><description>Single batched detection call instead of 14+ sequential calls</description></item>
    /// <item><description>Detection completes in &lt;100ms instead of ~2000ms</description></item>
    /// <item><description>Reduced device communication overhead</description></item>
    /// <item><description>More reliable detection with better error handling</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public sealed class SimpleDeviceCapabilities {
        /// <summary>
        /// Gets or sets the MicroPython platform identifier.
        /// </summary>
        /// <value>
        /// The platform string from sys.platform (e.g., "esp32", "rp2", "linux").
        /// Null if platform detection failed.
        /// </value>
        public string? Platform { get; set; }

        /// <summary>
        /// Gets or sets the MicroPython version information.
        /// </summary>
        /// <value>
        /// The version string from sys.version. Null if version detection failed.
        /// </value>
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the supported hardware features as a flag enumeration.
        /// </summary>
        /// <value>
        /// A combination of SimpleDeviceFeatureSet flags indicating which hardware
        /// features are available on the device. Defaults to None.
        /// </value>
        public SimpleDeviceFeatureSet SupportedFeatures { get; set; } = SimpleDeviceFeatureSet.None;

        /// <summary>
        /// Gets or sets the available memory in bytes.
        /// </summary>
        /// <value>
        /// The amount of free memory reported by gc.mem_free(), or 0 if unavailable.
        /// </value>
        public int AvailableMemory { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether capability detection has completed.
        /// </summary>
        /// <value>
        /// True if capability detection has been attempted (successfully or not);
        /// otherwise, false.
        /// </value>
        public bool DetectionComplete { get; set; }

        /// <summary>
        /// Gets a value indicating whether the device supports a specific feature.
        /// </summary>
        /// <param name="feature">The feature to check for support.</param>
        /// <returns>True if the feature is supported; otherwise, false.</returns>
        /// <example>
        /// <code>
        /// if (capabilities.SupportsFeature(SimpleDeviceFeatureSet.GPIO))
        /// {
        ///     // Use GPIO functionality
        /// }
        /// </code>
        /// </example>
        public bool SupportsFeature(SimpleDeviceFeatureSet feature) {
            return this.SupportedFeatures.HasFlag(feature);
        }

        /// <summary>
        /// Returns a string representation of the device capabilities.
        /// </summary>
        /// <returns>A formatted string showing platform, version, and feature count.</returns>
        public override string ToString() {
            var featureCount = CountFlags(this.SupportedFeatures);
            var status = this.DetectionComplete ? "Complete" : "Pending";
            return $"DeviceCapabilities [{status}] Platform: {this.Platform ?? "Unknown"}, " +
                   $"Version: {this.Version ?? "Unknown"}, Features: {featureCount}, " +
                   $"Memory: {this.AvailableMemory} bytes";
        }

        private static int CountFlags(SimpleDeviceFeatureSet flags) {
            var count = 0;
            var value = (int)flags;
            while (value > 0) {
                count += value & 1;
                value >>= 1;
            }

            return count;
        }
    }

    /// <summary>
    /// Hardware features that can be detected on MicroPython devices.
    /// </summary>
    /// <remarks>
    /// This enumeration represents the hardware capabilities that can be detected
    /// through import testing on MicroPython devices. The detection is performed
    /// in a single batched operation for optimal performance.
    /// </remarks>
    [Flags]
    public enum SimpleDeviceFeatureSet {
        /// <summary>No features detected or supported.</summary>
        None = 0,

        /// <summary>General Purpose I/O pins (machine.Pin).</summary>
        GPIO = 1 << 0,

        /// <summary>Analog to Digital Converter (machine.ADC).</summary>
        ADC = 1 << 1,

        /// <summary>Pulse Width Modulation (machine.PWM).</summary>
        PWM = 1 << 2,

        /// <summary>I2C communication interface (machine.I2C).</summary>
        I2C = 1 << 3,

        /// <summary>SPI communication interface (machine.SPI).</summary>
        SPI = 1 << 4,

        /// <summary>Hardware timers (machine.Timer).</summary>
        Timer = 1 << 5,

        /// <summary>Real-time clock (machine.RTC).</summary>
        RTC = 1 << 6,

        /// <summary>Multi-threading support (_thread module).</summary>
        Threading = 1 << 7,

        /// <summary>File system operations (os module).</summary>
        FileSystem = 1 << 8,

        /// <summary>WiFi networking capabilities (network module).</summary>
        WiFi = 1 << 9,

        /// <summary>Bluetooth communication (bluetooth module).</summary>
        Bluetooth = 1 << 10,

        /// <summary>Cryptographic acceleration (hashlib, etc.).</summary>
        CryptoAccel = 1 << 11,

        /// <summary>Touch sensor capabilities.</summary>
        TouchSensor = 1 << 12,

        /// <summary>Display capabilities (framebuf, etc.).</summary>
        Display = 1 << 13,

        /// <summary>Audio capabilities (machine.DAC, etc.).</summary>
        Audio = 1 << 14,
    }
}
