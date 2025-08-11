// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    using System.Text.RegularExpressions;
    using Belay.Core.Communication;
    using Belay.Core.Exceptions;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Detects and tracks device capabilities and features.
    /// </summary>
    public sealed class DeviceCapabilities : IDeviceCapabilities {
        private readonly IDeviceCommunication communication;
        private readonly ILogger<DeviceCapabilities> logger;
        private readonly SemaphoreSlim detectionLock = new(1, 1);

        private bool detectionComplete = false;
        private string? firmwareVersion;
        private string? deviceType;
        private string? uniqueDeviceId;
        private DeviceFeatureSet supportedFeatures = DeviceFeatureSet.None;
        private DevicePerformanceProfile performanceProfile = new() {
            PerformanceTier = DevicePerformanceTier.Unknown,
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceCapabilities"/> class.
        /// </summary>
        /// <param name="communication">The device communication instance.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        public DeviceCapabilities(IDeviceCommunication communication, ILogger<DeviceCapabilities> logger) {
            this.communication = communication ?? throw new ArgumentNullException(nameof(communication));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public string? FirmwareVersion {
            get {
                lock (this.detectionLock) {
                    return this.firmwareVersion;
                }
            }
        }

        /// <inheritdoc />
        public string? DeviceType {
            get {
                lock (this.detectionLock) {
                    return this.deviceType;
                }
            }
        }

        /// <inheritdoc />
        public DeviceFeatureSet SupportedFeatures {
            get {
                lock (this.detectionLock) {
                    return this.supportedFeatures;
                }
            }
        }

        /// <inheritdoc />
        public DevicePerformanceProfile PerformanceProfile {
            get {
                lock (this.detectionLock) {
                    return this.performanceProfile;
                }
            }
        }

        /// <inheritdoc />
        public bool IsDetectionComplete {
            get {
                lock (this.detectionLock) {
                    return this.detectionComplete;
                }
            }
        }

        /// <inheritdoc />
        public string? UniqueDeviceId {
            get {
                lock (this.detectionLock) {
                    return this.uniqueDeviceId;
                }
            }
        }

        /// <inheritdoc />
        public bool SupportsFeature(DeviceFeature feature) {
            var featureFlag = feature switch {
                DeviceFeature.GPIO => DeviceFeatureSet.GPIO,
                DeviceFeature.ADC => DeviceFeatureSet.ADC,
                DeviceFeature.PWM => DeviceFeatureSet.PWM,
                DeviceFeature.I2C => DeviceFeatureSet.I2C,
                DeviceFeature.SPI => DeviceFeatureSet.SPI,
                DeviceFeature.Timer => DeviceFeatureSet.Timer,
                DeviceFeature.RTC => DeviceFeatureSet.RTC,
                DeviceFeature.Threading => DeviceFeatureSet.Threading,
                DeviceFeature.FileSystem => DeviceFeatureSet.FileSystem,
                DeviceFeature.WiFi => DeviceFeatureSet.WiFi,
                DeviceFeature.Bluetooth => DeviceFeatureSet.Bluetooth,
                DeviceFeature.CryptoAccel => DeviceFeatureSet.CryptoAccel,
                DeviceFeature.TouchSensor => DeviceFeatureSet.TouchSensor,
                DeviceFeature.Display => DeviceFeatureSet.Display,
                DeviceFeature.Audio => DeviceFeatureSet.Audio,
                _ => DeviceFeatureSet.None,
            };

            return this.supportedFeatures.HasFlag(featureFlag);
        }

        /// <inheritdoc />
        public async Task RefreshCapabilitiesAsync(CancellationToken cancellationToken = default) {
            if (this.communication.State != DeviceConnectionState.Connected) {
                throw new Belay.Core.Exceptions.DeviceConnectionException("Cannot refresh capabilities while device is disconnected");
            }

            await this.detectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                this.logger.LogDebug("Starting device capability detection");

                // Reset detection state
                this.detectionComplete = false;
                this.supportedFeatures = DeviceFeatureSet.None;

                // Detect basic system information
                await this.DetectSystemInfoAsync(cancellationToken).ConfigureAwait(false);

                // Detect hardware features
                await this.DetectHardwareFeaturesAsync(cancellationToken).ConfigureAwait(false);

                // Measure performance characteristics
                await this.DetectPerformanceProfileAsync(cancellationToken).ConfigureAwait(false);

                this.detectionComplete = true;
                this.logger.LogInformation(
                    "Device capability detection complete. Device: {DeviceType}, Features: {Features}",
                    this.deviceType,
                    this.supportedFeatures);
            }
            catch (Exception ex) {
                this.logger.LogError(ex, "Error during capability detection");
                throw;
            }
            finally {
                this.detectionLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task<DeviceMemoryInfo> GetMemoryInfoAsync(CancellationToken cancellationToken = default) {
            if (this.communication.State != DeviceConnectionState.Connected) {
                throw new Belay.Core.Exceptions.DeviceConnectionException("Cannot get memory info while device is disconnected");
            }

            try {
                // Force garbage collection and get memory information
                var memoryScript = """
                    import gc
                    gc.collect()
                    free = gc.mem_free()
                    alloc = gc.mem_alloc()
                    (free, alloc, free + alloc)
                    """;

                var result = await this.communication.ExecuteAsync<object[]>(memoryScript, cancellationToken).ConfigureAwait(false);

                if (result?.Length == 3) {
                    var free = Convert.ToInt32(result[0]);
                    var allocated = Convert.ToInt32(result[1]);
                    var total = Convert.ToInt32(result[2]);

                    return new DeviceMemoryInfo {
                        FreeBytes = free,
                        AllocatedBytes = allocated,
                        TotalBytes = total,
                    };
                }

                this.logger.LogWarning("Unexpected memory info response format");
                return new DeviceMemoryInfo();
            }
            catch (Exception ex) {
                this.logger.LogWarning(ex, "Failed to get device memory information");
                return new DeviceMemoryInfo();
            }
        }

        private async Task DetectSystemInfoAsync(CancellationToken cancellationToken) {
            try {
                // Get system version information
                var versionResult = await this.communication.ExecuteAsync<string>(
                    "import sys; sys.version", cancellationToken).ConfigureAwait(false);

                this.firmwareVersion = this.ExtractFirmwareVersion(versionResult);

                // Get platform information
                var platformResult = await this.communication.ExecuteAsync<string>(
                    "sys.platform", cancellationToken).ConfigureAwait(false);

                this.deviceType = platformResult?.Trim();

                // Try to get unique device ID
                try {
                    var uniqueIdResult = await this.communication.ExecuteAsync<string>(
                        "import machine; machine.unique_id().hex() if hasattr(machine, 'unique_id') else None",
                        cancellationToken).ConfigureAwait(false);

                    this.uniqueDeviceId = uniqueIdResult;
                }
                catch (Exception ex) {
                    // unique_id not available on all platforms or communication error
                    this.logger.LogDebug(ex, "Unable to get unique device ID - may not be supported on this platform");
                    this.uniqueDeviceId = null;
                }

                this.logger.LogDebug(
                    "Detected system info - Platform: {Platform}, Version: {Version}, ID: {UniqueId}",
                    this.deviceType, this.firmwareVersion, this.uniqueDeviceId);
            }
            catch (Exception ex) {
                this.logger.LogWarning(ex, "Failed to detect system information");
            }
        }

        private async Task DetectHardwareFeaturesAsync(CancellationToken cancellationToken) {
            var features = DeviceFeatureSet.None;

            // Test for machine module and common hardware features
            var featureTests = new Dictionary<DeviceFeatureSet, string> {
                { DeviceFeatureSet.GPIO, "from machine import Pin" },
                { DeviceFeatureSet.ADC, "from machine import ADC" },
                { DeviceFeatureSet.PWM, "from machine import PWM" },
                { DeviceFeatureSet.I2C, "from machine import I2C" },
                { DeviceFeatureSet.SPI, "from machine import SPI" },
                { DeviceFeatureSet.Timer, "from machine import Timer" },
                { DeviceFeatureSet.RTC, "from machine import RTC" },
                { DeviceFeatureSet.TouchSensor, "from machine import TouchPad" },
            };

            // Test basic software features
            var softwareTests = new Dictionary<DeviceFeatureSet, string> {
                { DeviceFeatureSet.Threading, "import _thread" },
                { DeviceFeatureSet.FileSystem, "import os; os.listdir('/')" },
            };

            // Test hardware features
            foreach (var test in featureTests) {
                if (await this.TestFeatureAsync(test.Value, cancellationToken).ConfigureAwait(false)) {
                    features |= test.Key;
                }
            }

            // Test software features
            foreach (var test in softwareTests) {
                if (await this.TestFeatureAsync(test.Value, cancellationToken).ConfigureAwait(false)) {
                    features |= test.Key;
                }
            }

            // Platform-specific feature detection
            if (this.deviceType?.Contains("esp32", StringComparison.OrdinalIgnoreCase) == true) {
                if (await this.TestFeatureAsync("import network", cancellationToken).ConfigureAwait(false)) {
                    features |= DeviceFeatureSet.WiFi;
                }

                if (await this.TestFeatureAsync("import bluetooth", cancellationToken).ConfigureAwait(false)) {
                    features |= DeviceFeatureSet.Bluetooth;
                }
            }

            this.supportedFeatures = features;
            this.logger.LogDebug("Detected hardware features: {Features}", features);
        }

        private async Task DetectPerformanceProfileAsync(CancellationToken cancellationToken) {
            try {
                // Get memory information for performance estimation
                var memoryInfo = await this.GetMemoryInfoAsync(cancellationToken).ConfigureAwait(false);

                // Run simple performance benchmark
                var benchmarkScript = """
                    import time
                    start = time.ticks_us()
                    for i in range(10000):
                        x = i * 2.5
                    time.ticks_diff(time.ticks_us(), start)
                    """;

                var benchmarkTime = await this.communication.ExecuteAsync<int>(
                    benchmarkScript, cancellationToken).ConfigureAwait(false);

                // Estimate performance tier based on memory and benchmark
                var tier = EstimatePerformanceTier(memoryInfo.TotalBytes, benchmarkTime);

                var profile = new DevicePerformanceProfile {
                    AvailableRamBytes = memoryInfo.TotalBytes,
                    PerformanceTier = tier,
                    BenchmarkResults = new Dictionary<string, double> {
                        { "arithmetic_10k_us", benchmarkTime },
                    },
                };

                // Calculate relative performance score based on benchmark
                // This is not an actual CPU speed, but a relative performance indicator
                if (benchmarkTime > 0) {
                    // Normalize benchmark time to a relative score (higher = better performance)
                    // Based on typical MicroPython performance ranges observed in testing
                    var relativeScore = Math.Max(1, Math.Min(1000, 1_000_000 / benchmarkTime));
                    profile = profile with { EstimatedCpuSpeedMhz = relativeScore };
                }

                this.performanceProfile = profile;
                this.logger.LogDebug(
                    "Performance profile - Tier: {Tier}, RAM: {Ram}B, Benchmark: {Benchmark}us",
                    tier, memoryInfo.TotalBytes, benchmarkTime);
            }
            catch (Exception ex) {
                this.logger.LogWarning(ex, "Failed to detect performance profile");
                this.performanceProfile = new DevicePerformanceProfile {
                    PerformanceTier = DevicePerformanceTier.Unknown,
                };
            }
        }

        private async Task<bool> TestFeatureAsync(string testCode, CancellationToken cancellationToken) {
            try {
                await this.communication.ExecuteAsync(testCode, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) {
                this.logger.LogDebug(ex, "Feature test failed for code: {TestCode}",
                    testCode.Length > 50 ? $"{testCode[..50]}..." : testCode);
                return false;
            }
        }

        private string? ExtractFirmwareVersion(string? versionString) {
            if (string.IsNullOrWhiteSpace(versionString)) {
                return null;
            }

            // Extract MicroPython version using regex
            var match = Regex.Match(versionString, @"MicroPython v(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (match.Success) {
                return match.Groups[1].Value;
            }

            // Fallback to extracting any version-like pattern
            match = Regex.Match(versionString, @"v?(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (match.Success) {
                return match.Groups[1].Value;
            }

            return versionString.Length > 50 ? versionString[..50] + "..." : versionString;
        }

        private static DevicePerformanceTier EstimatePerformanceTier(int totalMemory, int benchmarkTime) {
            // Performance tier estimation based on memory and execution time
            // These thresholds are based on common MicroPython device categories:
            // - High: ESP32-S3, Raspberry Pi Pico W with lots of RAM
            // - Medium: Standard ESP32, ESP8266 with decent memory
            // - Low: Constrained devices, older microcontrollers

            // If we can't get valid measurements, return Unknown
            if (totalMemory <= 0 || benchmarkTime <= 0) {
                return DevicePerformanceTier.Unknown;
            }

            // High-end devices: >512KB RAM and fast execution (<100ms for 10k iterations)
            if (totalMemory > 512_000 && benchmarkTime < 100_000) {
                return DevicePerformanceTier.High;
            }

            // Medium devices: >128KB RAM and reasonable performance (<500ms for 10k iterations)
            if (totalMemory > 128_000 && benchmarkTime < 500_000) {
                return DevicePerformanceTier.Medium;
            }

            // Everything else with valid measurements is considered low-end
            return DevicePerformanceTier.Low;
        }
    }
}
