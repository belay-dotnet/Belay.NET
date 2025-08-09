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
    /// Tests for the DeviceSessionManager class.
    /// </summary>
    public class DeviceSessionManagerTests
    {
        private Mock<ILoggerFactory> mockLoggerFactory = null!;
        private Mock<ILogger<DeviceSessionManager>> mockLogger = null!;
        private Mock<IDeviceCommunication> mockCommunication = null!;
        private DeviceSessionManager sessionManager = null!;

        [SetUp]
        public void SetUp()
        {
            this.mockLoggerFactory = new Mock<ILoggerFactory>();
            this.mockLogger = new Mock<ILogger<DeviceSessionManager>>();
            this.mockCommunication = new Mock<IDeviceCommunication>();

            this.mockLoggerFactory.Setup(f => f.CreateLogger(typeof(DeviceSessionManager).FullName!))
                .Returns(this.mockLogger.Object);
            this.mockLoggerFactory.Setup(f => f.CreateLogger(typeof(DeviceSession).FullName!))
                .Returns(new Mock<ILogger<DeviceSession>>().Object);
            this.mockLoggerFactory.Setup(f => f.CreateLogger(typeof(ResourceTracker).FullName!))
                .Returns(new Mock<ILogger<ResourceTracker>>().Object);
            this.mockLoggerFactory.Setup(f => f.CreateLogger(typeof(ExecutorContext).FullName!))
                .Returns(new Mock<ILogger<ExecutorContext>>().Object);
            this.mockLoggerFactory.Setup(f => f.CreateLogger(typeof(DeviceContext).FullName!))
                .Returns(new Mock<ILogger<DeviceContext>>().Object);
            this.mockLoggerFactory.Setup(f => f.CreateLogger(typeof(DeviceCapabilities).FullName!))
                .Returns(new Mock<ILogger<DeviceCapabilities>>().Object);

            this.mockCommunication.Setup(c => c.State)
                .Returns(DeviceConnectionState.Connected);

            this.sessionManager = new DeviceSessionManager(this.mockLoggerFactory.Object);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (this.sessionManager != null)
            {
                await this.sessionManager.DisposeAsync();
            }
        }

        [Test]
        public void Constructor_WithValidLoggerFactory_SetsStateToInactive()
        {
            // Act & Assert
            this.sessionManager.State.Should().Be(DeviceSessionState.Inactive);
            this.sessionManager.CurrentSessionId.Should().BeNull();
        }

        [Test]
        public void Constructor_WithNullLoggerFactory_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = () => new DeviceSessionManager(null!);
            act.Should().Throw<ArgumentNullException>()
                .Which.ParamName.Should().Be("loggerFactory");
        }

        [Test]
        public async Task CreateSessionAsync_WithValidCommunication_ReturnsSession()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("MicroPython v1.0");

            // Act
            var session = await this.sessionManager.CreateSessionAsync(this.mockCommunication.Object);

            // Assert
            session.Should().NotBeNull();
            session.SessionId.Should().NotBeNullOrWhiteSpace();
            session.IsActive.Should().BeTrue();
            this.sessionManager.CurrentSessionId.Should().Be(session.SessionId);
        }

        [Test]
        public async Task CreateSessionAsync_WithNullCommunication_ThrowsArgumentNullException()
        {
            // Act & Assert
            var act = async () => await this.sessionManager.CreateSessionAsync(null!);
            await act.Should().ThrowAsync<ArgumentNullException>()
                .Where(e => e.ParamName == "communication");
        }

        [Test]
        public async Task GetOrCreateSessionAsync_WhenNoCurrentSession_CreatesNewSession()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("MicroPython v1.0");

            // Act
            var session = await this.sessionManager.GetOrCreateSessionAsync(this.mockCommunication.Object);

            // Assert
            session.Should().NotBeNull();
            session.SessionId.Should().NotBeNullOrWhiteSpace();
            this.sessionManager.CurrentSessionId.Should().Be(session.SessionId);
        }

        [Test]
        public async Task GetOrCreateSessionAsync_WhenCurrentSessionExists_ReturnsExistingSession()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("MicroPython v1.0");

            var firstSession = await this.sessionManager.CreateSessionAsync(this.mockCommunication.Object);

            // Act
            var secondSession = await this.sessionManager.GetOrCreateSessionAsync(this.mockCommunication.Object);

            // Assert
            secondSession.Should().BeSameAs(firstSession);
            this.sessionManager.CurrentSessionId.Should().Be(firstSession.SessionId);
        }

        [Test]
        public async Task ExecuteInSessionAsync_WithFunction_ExecutesSuccessfully()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("MicroPython v1.0");

            var expectedResult = "test result";
            
            // Act
            var result = await this.sessionManager.ExecuteInSessionAsync(
                this.mockCommunication.Object,
                session =>
                {
                    session.Should().NotBeNull();
                    session.IsActive.Should().BeTrue();
                    return Task.FromResult(expectedResult);
                });

            // Assert
            result.Should().Be(expectedResult);
        }

        [Test]
        public async Task ExecuteInSessionAsync_WithAction_ExecutesSuccessfully()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("MicroPython v1.0");

            var executed = false;

            // Act
            await this.sessionManager.ExecuteInSessionAsync(
                this.mockCommunication.Object,
                session =>
                {
                    session.Should().NotBeNull();
                    session.IsActive.Should().BeTrue();
                    executed = true;
                    return Task.CompletedTask;
                });

            // Assert
            executed.Should().BeTrue();
        }

        [Test]
        public async Task EndSessionAsync_WithValidSessionId_EndsSession()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("MicroPython v1.0");

            var session = await this.sessionManager.CreateSessionAsync(this.mockCommunication.Object);
            var sessionId = session.SessionId;

            // Act
            await this.sessionManager.EndSessionAsync(sessionId);

            // Assert
            this.sessionManager.CurrentSessionId.Should().BeNull();
            session.IsActive.Should().BeFalse();
        }

        [Test]
        public async Task GetSessionStatsAsync_ReturnsCorrectStatistics()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("MicroPython v1.0");

            // Act
            var initialStats = await this.sessionManager.GetSessionStatsAsync();
            await this.sessionManager.CreateSessionAsync(this.mockCommunication.Object);
            var afterCreateStats = await this.sessionManager.GetSessionStatsAsync();

            // Assert
            initialStats.ActiveSessionCount.Should().Be(0);
            initialStats.TotalSessionCount.Should().Be(0);
            
            afterCreateStats.ActiveSessionCount.Should().Be(1);
            afterCreateStats.TotalSessionCount.Should().Be(1);
        }

        [Test]
        public async Task DisposeAsync_CleansUpSessionsAndResources()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("MicroPython v1.0");

            var session = await this.sessionManager.CreateSessionAsync(this.mockCommunication.Object);

            // Act
            await this.sessionManager.DisposeAsync();

            // Assert
            this.sessionManager.State.Should().Be(DeviceSessionState.Disposed);
            session.IsActive.Should().BeFalse();
        }

        [Test]
        public async Task DeviceCapabilities_WhenDetected_AreExposed()
        {
            // Arrange
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>("import sys; sys.version", It.IsAny<CancellationToken>()))
                .ReturnsAsync("MicroPython v1.19.1");
            this.mockCommunication.Setup(c => c.ExecuteAsync<string>("sys.platform", It.IsAny<CancellationToken>()))
                .ReturnsAsync("esp32");

            // Act
            await this.sessionManager.CreateSessionAsync(this.mockCommunication.Object);

            // Assert
            this.sessionManager.Capabilities.Should().NotBeNull();
            this.sessionManager.Capabilities!.FirmwareVersion.Should().NotBeNullOrEmpty();
            this.sessionManager.Capabilities.DeviceType.Should().NotBeNullOrEmpty();
        }
    }
}