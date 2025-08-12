// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit {
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Belay.Core;
    using Belay.Core.Communication;
    using FluentAssertions;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NUnit.Framework;

    /// <summary>
    /// Tests for the SimplifiedCapabilityDetection class.
    /// Validates the batched capability detection approach that replaces
    /// sequential detection for improved performance.
    /// </summary>
    [TestFixture]
    public class SimplifiedCapabilityDetectionTests {
        private Mock<IDeviceCommunication> mockCommunication = null!;
        private Mock<ILogger> mockLogger = null!;

        [SetUp]
        public void SetUp() {
            mockCommunication = new Mock<IDeviceCommunication>();
            mockLogger = new Mock<ILogger>();

            // Default setup - device is connected
            mockCommunication.Setup(c => c.State).Returns(DeviceConnectionState.Connected);
        }

        [Test]
        public async Task DetectAsync_WithNullCommunication_ThrowsArgumentNullException() {
            // Act & Assert
            await FluentActions
                .Invoking(() => SimplifiedCapabilityDetection.DetectAsync(null!))
                .Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("communication");
        }

        [Test]
        public async Task DetectAsync_WithDisconnectedDevice_ThrowsInvalidOperationException() {
            // Arrange
            mockCommunication.Setup(c => c.State).Returns(DeviceConnectionState.Disconnected);

            // Act & Assert
            await FluentActions
                .Invoking(() => SimplifiedCapabilityDetection.DetectAsync(mockCommunication.Object))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Device must be connected before capability detection");
        }

        [Test]
        public async Task DetectAsync_WithFullCapabilityResponse_ParsesCorrectly() {
            // Arrange
            var detectionResponse = new Dictionary<string, object> {
                ["platform"] = "esp32",
                ["version"] = "3.4.0",
                ["memory"] = 45000,
                ["features"] = new List<object> { "gpio", "wifi", "i2c", "spi", "bluetooth" }
            };

            mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(detectionResponse);

            // Act
            var result = await SimplifiedCapabilityDetection.DetectAsync(mockCommunication.Object, mockLogger.Object);

            // Assert
            result.Should().NotBeNull();
            result.Platform.Should().Be("esp32");
            result.Version.Should().Be("3.4.0");
            result.AvailableMemory.Should().Be(45000);
            result.DetectionComplete.Should().BeTrue();

            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.GPIO);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.WiFi);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.I2C);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.SPI);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.Bluetooth);
            result.SupportedFeatures.Should().NotHaveFlag(SimpleDeviceFeatureSet.ADC);
        }

        [Test]
        public async Task DetectAsync_WithMinimalResponse_ParsesCorrectly() {
            // Arrange
            var detectionResponse = new Dictionary<string, object> {
                ["platform"] = "unknown",
                ["version"] = "unknown",
                ["memory"] = 0,
                ["features"] = new List<object>()
            };

            mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(detectionResponse);

            // Act
            var result = await SimplifiedCapabilityDetection.DetectAsync(mockCommunication.Object);

            // Assert
            result.Should().NotBeNull();
            result.Platform.Should().Be("unknown");
            result.Version.Should().Be("unknown");
            result.AvailableMemory.Should().Be(0);
            result.SupportedFeatures.Should().Be(SimpleDeviceFeatureSet.None);
            result.DetectionComplete.Should().BeTrue();
        }

        [Test]
        public async Task DetectAsync_WithMissingFields_HandlesGracefully() {
            // Arrange - response with missing fields
            var detectionResponse = new Dictionary<string, object> {
                ["platform"] = "rp2"
                // Missing version, memory, and features
            };

            mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(detectionResponse);

            // Act
            var result = await SimplifiedCapabilityDetection.DetectAsync(mockCommunication.Object);

            // Assert
            result.Should().NotBeNull();
            result.Platform.Should().Be("rp2");
            result.Version.Should().BeNull();
            result.AvailableMemory.Should().Be(0);
            result.SupportedFeatures.Should().Be(SimpleDeviceFeatureSet.None);
            result.DetectionComplete.Should().BeTrue();
        }

        [Test]
        public async Task DetectAsync_WithLargeMemoryValue_HandlesCorrectly() {
            // Arrange - test with long memory value that needs conversion
            var detectionResponse = new Dictionary<string, object> {
                ["platform"] = "linux",
                ["memory"] = (long)2_147_483_647 + 1000, // Value larger than int.MaxValue
                ["features"] = new List<object> { "filesystem" }
            };

            mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(detectionResponse);

            // Act
            var result = await SimplifiedCapabilityDetection.DetectAsync(mockCommunication.Object);

            // Assert
            result.AvailableMemory.Should().Be(int.MaxValue); // Should be clamped to int.MaxValue
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.FileSystem);
        }

        [Test]
        public async Task DetectAsync_WithStringMemoryValue_ParsesCorrectly() {
            // Arrange - memory as string (could happen from Python)
            var detectionResponse = new Dictionary<string, object> {
                ["memory"] = "32768",
                ["features"] = new List<object>()
            };

            mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(detectionResponse);

            // Act
            var result = await SimplifiedCapabilityDetection.DetectAsync(mockCommunication.Object);

            // Assert
            result.AvailableMemory.Should().Be(32768);
        }

        [Test]
        public async Task DetectAsync_WithAllFeatures_MapsCorrectly() {
            // Arrange - test with all possible features
            var allFeatures = new List<object>
            {
                "gpio", "adc", "pwm", "i2c", "spi", "timer", "rtc",
                "threading", "filesystem", "wifi", "bluetooth", "crypto",
                "touch", "display", "audio"
            };

            var detectionResponse = new Dictionary<string, object> {
                ["features"] = allFeatures
            };

            mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(detectionResponse);

            // Act
            var result = await SimplifiedCapabilityDetection.DetectAsync(mockCommunication.Object);

            // Assert
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.GPIO);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.ADC);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.PWM);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.I2C);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.SPI);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.Timer);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.RTC);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.Threading);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.FileSystem);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.WiFi);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.Bluetooth);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.CryptoAccel);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.TouchSensor);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.Display);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.Audio);
        }

        [Test]
        public async Task DetectAsync_WithUnknownFeatures_IgnoresGracefully() {
            // Arrange - include unknown feature names
            var detectionResponse = new Dictionary<string, object> {
                ["features"] = new List<object> { "gpio", "unknown_feature", "wifi", "invalid", "i2c" }
            };

            mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(detectionResponse);

            // Act
            var result = await SimplifiedCapabilityDetection.DetectAsync(mockCommunication.Object);

            // Assert
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.GPIO);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.WiFi);
            result.SupportedFeatures.Should().HaveFlag(SimpleDeviceFeatureSet.I2C);
            // Unknown features should be ignored
            result.SupportedFeatures.Should().NotHaveFlag(SimpleDeviceFeatureSet.ADC);
        }

        [Test]
        public async Task DetectAsync_WhenExecutionFails_ReturnsMinimalCapabilities() {
            // Arrange
            mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Device communication failed"));

            // Act
            var result = await SimplifiedCapabilityDetection.DetectAsync(mockCommunication.Object, mockLogger.Object);

            // Assert
            result.Should().NotBeNull();
            result.Platform.Should().Be("unknown");
            result.Version.Should().Be("unknown");
            result.AvailableMemory.Should().Be(0);
            result.SupportedFeatures.Should().Be(SimpleDeviceFeatureSet.None);
            result.DetectionComplete.Should().BeTrue();
        }

        [Test]
        public async Task DetectAsync_WithCancellation_PropagatesCancellation() {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            // Act & Assert
            await FluentActions
                .Invoking(() => SimplifiedCapabilityDetection.DetectAsync(mockCommunication.Object, cancellationToken: cts.Token))
                .Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        public async Task DetectAsync_ExecutesCorrectScript_VerifyScriptContent() {
            // Arrange
            string? executedScript = null;
            mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((script, _) => executedScript = script)
                .ReturnsAsync(new Dictionary<string, object> { ["features"] = new List<object>() });

            // Act
            await SimplifiedCapabilityDetection.DetectAsync(mockCommunication.Object);

            // Assert
            executedScript.Should().NotBeNull();
            executedScript.Should().Contain("import sys");
            executedScript.Should().Contain("result =");
            executedScript.Should().Contain("from machine import Pin");
            executedScript.Should().Contain("import network");
            executedScript.Should().Contain("import gc");
            executedScript.Should().Contain("result['memory'] = gc.mem_free()");
        }

        [Test]
        public async Task DetectAsync_PerformanceTest_CompletesQuickly() {
            // Arrange
            var detectionResponse = new Dictionary<string, object> {
                ["platform"] = "esp32",
                ["features"] = new List<object> { "gpio", "wifi" }
            };

            mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(detectionResponse);

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await SimplifiedCapabilityDetection.DetectAsync(mockCommunication.Object);
            stopwatch.Stop();

            // Assert
            result.Should().NotBeNull();
            // The actual detection should be very fast (mocked communication)
            // This test validates that our parsing logic doesn't add significant overhead
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
        }
    }
}
