using Belay.Core;
using Belay.Core.Communication;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Belay.Tests.Unit;

[TestFixture]
public class DeviceTests {
    [Test]
    public void Device_Constructor_ShouldInitializeCorrectly() {
        // Arrange
        var mockCommunication = new Mock<IDeviceCommunication>();
        mockCommunication.SetupGet(x => x.State).Returns(DeviceConnectionState.Disconnected);

        // Act
        using var device = new Device(mockCommunication.Object, logger: null);

        // Assert
        device.State.Should().Be(DeviceConnectionState.Disconnected);
    }

    [Test]
    public void Device_Constructor_WithNullCommunication_ShouldThrowArgumentNullException() {
        // Act & Assert
        Action action = () => new Device(null!, logger: null);
        action.Should().Throw<ArgumentNullException>().WithParameterName("communication");
    }

    [Test]
    [TestCase("serial:COM3")]
    [TestCase("serial:/dev/ttyUSB0")]
    [TestCase("subprocess:micropython")]
    public void FromConnectionString_ValidConnectionStrings_ShouldCreateDevice(string connectionString) {
        // Act
        using var device = Device.FromConnectionString(connectionString);

        // Assert
        device.Should().NotBeNull();
        device.State.Should().Be(DeviceConnectionState.Disconnected);
    }

    [Test]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void FromConnectionString_InvalidConnectionString_ShouldThrowArgumentException(string? connectionString) {
        // Act & Assert
        Action action = () => Device.FromConnectionString(connectionString!);
        action.Should().Throw<ArgumentException>();
    }

    [Test]
    [TestCase("invalidformat")]
    [TestCase("serial")]
    [TestCase("serial:")]
    [TestCase(":COM3")]
    public void FromConnectionString_MalformedConnectionString_ShouldThrowArgumentException(string connectionString) {
        // Act & Assert
        Action action = () => Device.FromConnectionString(connectionString);
        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void FromConnectionString_UnsupportedConnectionType_ShouldThrowArgumentException() {
        // Act & Assert
        Action action = () => Device.FromConnectionString("unsupported:parameter");
        action.Should().Throw<ArgumentException>().WithMessage("*Unsupported connection type: unsupported*");
    }

    [Test]
    public async Task ExecuteAsync_DisposedDevice_ShouldThrowObjectDisposedException() {
        // Arrange
        var mockCommunication = new Mock<IDeviceCommunication>();
        var device = new Device(mockCommunication.Object, logger: null);
        device.Dispose();

        // Act & Assert
        await device.Invoking(d => d.ExecuteAsync("print('test')"))
            .Should().ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public async Task ExecuteAsync_InvalidCode_ShouldThrowArgumentException(string? code) {
        // Arrange
        var mockCommunication = new Mock<IDeviceCommunication>();
        using var device = new Device(mockCommunication.Object, logger: null);

        // Act & Assert
        await device.Invoking(d => d.ExecuteAsync(code!))
            .Should().ThrowAsync<ArgumentException>().WithParameterName("code");
    }

    [Test]
    public async Task ExecuteAsync_ValidCode_ShouldCallCommunicationLayer() {
        // Arrange
        var mockCommunication = new Mock<IDeviceCommunication>();
        mockCommunication.Setup(x => x.ExecuteAsync("print('test')", It.IsAny<CancellationToken>()))
            .ReturnsAsync("test");

        using var device = new Device(mockCommunication.Object, logger: null);

        // Act
        string result = await device.ExecuteAsync("print('test')");

        // Assert
        result.Should().Be("test");
        mockCommunication.Verify(x => x.ExecuteAsync("print('test')", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ConnectAsync_SerialCommunication_ShouldCallConnectAsync() {
        // Arrange
        var mockSerial = new Mock<SerialDeviceCommunication>("COM3", 115200, 30000);
        mockSerial.Setup(x => x.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        using var device = new Device(mockSerial.Object, logger: null);

        // Act
        await device.ConnectAsync();

        // Assert
        mockSerial.Verify(x => x.ConnectAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DisconnectAsync_SerialCommunication_ShouldCallDisconnectAsync() {
        // Arrange
        var mockSerial = new Mock<SerialDeviceCommunication>("COM3", 115200, 30000);
        mockSerial.Setup(x => x.DisconnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        using var device = new Device(mockSerial.Object, logger: null);

        // Act
        await device.DisconnectAsync();

        // Assert
        mockSerial.Verify(x => x.DisconnectAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Dispose_MultipleCallsToDispose_ShouldNotThrow() {
        // Arrange
        var mockCommunication = new Mock<IDeviceCommunication>();
        var device = new Device(mockCommunication.Object, logger: null);

        // Act & Assert
        device.Dispose();
        Action secondDispose = () => device.Dispose();
        secondDispose.Should().NotThrow();
    }
}
