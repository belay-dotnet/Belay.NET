// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using Belay.Core.Communication;
using Belay.Core.Sessions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Belay.Tests.Unit.Sessions
{
    /// <summary>
    /// Tests for the DeviceCapabilities class.
    /// </summary>
    public class DeviceCapabilitiesTests
    {
        private readonly Mock<IDeviceCommunication> mockCommunication;
        private readonly Mock<ILogger<DeviceCapabilities>> mockLogger;
        private readonly DeviceCapabilities deviceCapabilities;

        public DeviceCapabilitiesTests()
        {
            this.mockCommunication = new Mock<IDeviceCommunication>();
            this.mockLogger = new Mock<ILogger<DeviceCapabilities>>();
            this.deviceCapabilities = new DeviceCapabilities(this.mockCommunication.Object, this.mockLogger.Object);

            this.mockCommunication.Setup(c => c.State)
                .Returns(DeviceConnectionState.Connected);
        }

        [Test]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            // Act & Assert
            this.deviceCapabilities.FirmwareVersion.Should().BeNull();
            this.deviceCapabilities.DeviceType.Should().BeNull();
            this.deviceCapabilities.SupportedFeatures.Should().Be(DeviceFeatureSet.None);
            this.deviceCapabilities.IsDetectionComplete.Should().BeFalse();
        }

        [Test]
        public void Constructor_WithNullCommunication_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new DeviceCapabilities(null!, this.mockLogger.Object);
            act.Should().Throw<ArgumentNullException>()
                .Which.ParamName.Should().Be("communication");
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new DeviceCapabilities(this.mockCommunication.Object, null!);
            act.Should().Throw<ArgumentNullException>()
                .Which.ParamName.Should().Be("logger");
        }

        [Test]
        public async Task RefreshCapabilitiesAsync_WhenDisconnected_ThrowsDeviceConnectionException()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.State)
                .Returns(DeviceConnectionState.Disconnected);

            // Act & Assert
            var act = async () => await this.deviceCapabilities.RefreshCapabilitiesAsync();
            await act.Should().ThrowAsync<Belay.Core.Exceptions.DeviceConnectionException>();
        }

        [Test]
        public async Task RefreshCapabilitiesAsync_WithValidDevice_DetectsBasicInfo()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>("import sys; sys.version", It.IsAny<CancellationToken>()))
                .ReturnsAsync("MicroPython v1.19.1 on 2023-05-18; ESP32 module with ESP32");
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>("sys.platform", It.IsAny<CancellationToken>()))
                .ReturnsAsync("esp32");
            this.mockCommunication.Setup(c => c.ExecuteAsync<object[]>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new object[] { 100000, 50000, 150000 }); // free, alloc, total memory

            // Act
            await this.deviceCapabilities.RefreshCapabilitiesAsync();

            // Assert
            this.deviceCapabilities.FirmwareVersion.Should().NotBeNull();
            this.deviceCapabilities.DeviceType.Should().Be("esp32");
            this.deviceCapabilities.IsDetectionComplete.Should().BeTrue();
        }

        [Test]
        public void SupportsFeature_WithSupportedFeature_ReturnsTrue()
        {
            // Arrange - use reflection to set supported features for testing
            var featuresField = typeof(DeviceCapabilities).GetField("supportedFeatures", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            featuresField?.SetValue(this.deviceCapabilities, DeviceFeatureSet.GPIO | DeviceFeatureSet.I2C);

            // Act & Assert
            this.deviceCapabilities.SupportsFeature(DeviceFeature.GPIO).Should().BeTrue();
            this.deviceCapabilities.SupportsFeature(DeviceFeature.I2C).Should().BeTrue();
            this.deviceCapabilities.SupportsFeature(DeviceFeature.SPI).Should().BeFalse();
        }

        [Test]
        public async Task GetMemoryInfoAsync_WithValidResponse_ReturnsMemoryInfo()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.ExecuteAsync<object[]>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new object[] { 100000, 50000, 150000 });

            // Act
            var memoryInfo = await this.deviceCapabilities.GetMemoryInfoAsync();

            // Assert
            memoryInfo.FreeBytes.Should().Be(100000);
            memoryInfo.AllocatedBytes.Should().Be(50000);
            memoryInfo.TotalBytes.Should().Be(150000);
        }

        [Test]
        public async Task GetMemoryInfoAsync_WithInvalidResponse_ReturnsDefaultMemoryInfo()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.ExecuteAsync<object[]>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new object[] { 100 }); // Invalid response format

            // Act
            var memoryInfo = await this.deviceCapabilities.GetMemoryInfoAsync();

            // Assert
            memoryInfo.FreeBytes.Should().Be(0);
            memoryInfo.AllocatedBytes.Should().Be(0);
            memoryInfo.TotalBytes.Should().Be(0);
        }

        [Test]
        public async Task RefreshCapabilitiesAsync_DetectsPerformanceProfile()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>("import sys; sys.version", It.IsAny<CancellationToken>()))
                .ReturnsAsync("MicroPython v1.19.1");
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>("sys.platform", It.IsAny<CancellationToken>()))
                .ReturnsAsync("esp32");
            this.mockCommunication.Setup(c => c.ExecuteAsync<object[]>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new object[] { 100000, 50000, 150000 });
            this.mockCommunication.Setup(c => c.ExecuteAsync<int>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(5000); // benchmark time in microseconds

            // Act
            await this.deviceCapabilities.RefreshCapabilitiesAsync();

            // Assert
            this.deviceCapabilities.PerformanceProfile.Should().NotBeNull();
            this.deviceCapabilities.PerformanceProfile.PerformanceTier.Should().NotBe(DevicePerformanceTier.Unknown);
            this.deviceCapabilities.PerformanceProfile.AvailableRamBytes.Should().BeGreaterThan(0);
        }

        [TestCase(DeviceFeature.GPIO, DeviceFeatureSet.GPIO)]
        [TestCase(DeviceFeature.I2C, DeviceFeatureSet.I2C)]
        [TestCase(DeviceFeature.WiFi, DeviceFeatureSet.WiFi)]
        public void SupportsFeature_MapsCorrectly(DeviceFeature feature, DeviceFeatureSet expectedFlag)
        {
            // Arrange - use reflection to set specific feature flag
            var featuresField = typeof(DeviceCapabilities).GetField("supportedFeatures", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            featuresField?.SetValue(this.deviceCapabilities, expectedFlag);

            // Act & Assert
            this.deviceCapabilities.SupportsFeature(feature).Should().BeTrue();
        }
    }
}