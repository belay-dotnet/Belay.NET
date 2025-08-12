// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit.Execution {
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Belay.Core;
    using Belay.Core.Communication;
    using FluentAssertions;
    using Microsoft.Extensions.Logging;
    using Moq;
    using NUnit.Framework;

    /// <summary>
    /// Refactored tests for SetupExecutor functionality without session management dependencies.
    /// These tests validate the simplified executor approach using DeviceState instead of sessions.
    /// </summary>
    [TestFixture]
    public class RefactoredSetupExecutorTests {
        private Mock<IDeviceCommunication> mockCommunication = null!;
        private Mock<ILogger> mockLogger = null!;
        private TestableSetupExecutor executor = null!;

        [SetUp]
        public void SetUp() {
            mockCommunication = new Mock<IDeviceCommunication>();
            mockLogger = new Mock<ILogger>();

            // Mock device is connected
            mockCommunication.Setup(c => c.State).Returns(DeviceConnectionState.Connected);

            executor = new TestableSetupExecutor(mockCommunication.Object, mockLogger.Object);
        }

        [Test]
        public void Constructor_WithNullCommunication_ThrowsArgumentNullException() {
            // Act & Assert
            FluentActions
                .Invoking(() => new TestableSetupExecutor(null!, mockLogger.Object))
                .Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("communication");
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException() {
            // Act & Assert
            FluentActions
                .Invoking(() => new TestableSetupExecutor(mockCommunication.Object, null!))
                .Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Test]
        public async Task ExecuteSetupAsync_WithBasicCode_ExecutesSuccessfully() {
            // Arrange
            const string pythonCode = "import machine\nprint('Setup complete')";
            const string expectedResult = "Setup complete";

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await executor.ExecuteSetupAsync<string>(pythonCode);

            // Assert
            result.Should().Be(expectedResult);
            mockCommunication.Verify(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ExecuteSetupAsync_WithNullCode_ThrowsArgumentException() {
            // Act & Assert
            await FluentActions
                .Invoking(async () => await executor.ExecuteSetupAsync<string>(null!))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Python code cannot be null or empty*")
                .WithParameterName("pythonCode");
        }

        [Test]
        public async Task ExecuteSetupAsync_TracksSetupOperation_InDeviceState() {
            // Arrange
            const string pythonCode = "setup_complete = True";

            mockCommunication
                .Setup(c => c.ExecuteAsync<bool>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async () => {
                    await Task.Delay(10); // Small delay to ensure operation tracking is visible
                    return true;
                });

            // Act
            var task = executor.ExecuteSetupAsync<bool>(pythonCode);

            // Assert - operation should be tracked during execution
            executor.State.CurrentOperation.Should().Be("Setup");

            var result = await task;

            // Assert - operation should be completed after execution
            result.Should().BeTrue();
            executor.State.CurrentOperation.Should().BeNull();
            executor.State.LastOperationTime.Should().NotBeNull();
        }

        [Test]
        public async Task ExecuteSetupAsync_WithDeviceCapabilitiesAvailable_UsesCapabilities() {
            // Arrange
            const string pythonCode = "configure_hardware()";
            var capabilities = new SimpleDeviceCapabilities {
                Platform = "rp2",
                SupportedFeatures = SimpleDeviceFeatureSet.GPIO | SimpleDeviceFeatureSet.I2C,
                AvailableMemory = 32768,
                DetectionComplete = true
            };

            executor.State.Capabilities = capabilities;

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Hardware configured for RP2");

            // Act
            var result = await executor.ExecuteSetupAsync<string>(pythonCode);

            // Assert
            result.Should().Be("Hardware configured for RP2");
            // Verify capabilities were available during setup
            executor.State.Capabilities.Should().NotBeNull();
            executor.State.Capabilities!.Platform.Should().Be("rp2");
        }

        [Test]
        public async Task ExecuteSetupAsync_WithCancellation_PropagatesCancellation() {
            // Arrange
            const string pythonCode = "import time; time.sleep(5)";
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((_, token) => Task.FromCanceled<string>(token));

            cts.Cancel();

            // Act & Assert
            await FluentActions
                .Invoking(async () => await executor.ExecuteSetupAsync<string>(pythonCode, cts.Token))
                .Should().ThrowAsync<TaskCanceledException>();
        }

        [Test]
        public async Task ExecuteSetupAsync_WithSetupException_PropagatesException() {
            // Arrange
            const string pythonCode = "invalid_setup_function()";
            var setupException = new InvalidOperationException("Setup failed: invalid_setup_function not defined");

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(setupException);

            // Act & Assert
            await FluentActions
                .Invoking(async () => await executor.ExecuteSetupAsync<string>(pythonCode))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Setup failed: invalid_setup_function not defined");
        }

        [Test]
        public async Task ExecuteSetupAsync_WithComplexSetupSequence_ExecutesInOrder() {
            // Arrange
            var setupSequence = new[]
            {
                "# Initialize hardware",
                "import machine",
                "pin = machine.Pin(2, machine.Pin.OUT)",
                "pin.value(1)",
                "print('Setup completed')"
            };

            var pythonCode = string.Join("\n", setupSequence);
            const string expectedResult = "Setup completed";

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await executor.ExecuteSetupAsync<string>(pythonCode);

            // Assert
            result.Should().Be(expectedResult);
            mockCommunication.Verify(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public void State_InitialState_IsCorrect() {
            // Assert
            executor.State.Should().NotBeNull();
            executor.State.Capabilities.Should().BeNull();
            executor.State.CurrentOperation.Should().BeNull();
            executor.State.LastOperationTime.Should().BeNull();
            executor.State.ConnectionState.Should().Be(DeviceConnectionState.Connected);
        }

        [Test]
        public async Task ExecuteSetupAsync_WithVoidReturn_CompletesSuccessfully() {
            // Arrange
            const string pythonCode = "global_variable = 'initialized'";

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);

            // Act & Assert - Should not throw
            var action = () => executor.ExecuteSetupAsync(pythonCode);
            await action.Should().NotThrowAsync();

            mockCommunication.Verify(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    /// <summary>
    /// Testable implementation of SetupExecutor for the refactored architecture.
    /// This represents how SetupExecutor would work without session management dependencies.
    /// </summary>
    public class TestableSetupExecutor {
        private readonly IDeviceCommunication communication;
        private readonly ILogger logger;

        public DeviceState State { get; } = new DeviceState();

        public TestableSetupExecutor(IDeviceCommunication communication, ILogger logger) {
            this.communication = communication ?? throw new ArgumentNullException(nameof(communication));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize state with connection state from communication
            State.ConnectionState = this.communication.State;
        }

        public async Task<T> ExecuteSetupAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken = default) {
            // Validate input
            if (string.IsNullOrWhiteSpace(pythonCode)) {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            // Check device connection
            if (communication.State != DeviceConnectionState.Connected) {
                throw new InvalidOperationException("Device must be connected before executing setup code");
            }

            // Track operation in state
            State.SetCurrentOperation("Setup");

            try {
                logger.LogDebug("Executing setup code: {Code}", pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

                // Apply setup-specific policies
                var result = await ExecuteWithSetupPoliciesAsync<T>(pythonCode, cancellationToken);

                logger.LogDebug("Setup code execution completed successfully");

                return result;
            }
            finally {
                // Complete operation tracking
                State.CompleteOperation();
            }
        }

        public async Task ExecuteSetupAsync(
            string pythonCode,
            CancellationToken cancellationToken = default) {
            // Call the string-returning version and ignore result for void execution
            await ExecuteSetupAsync<string>(pythonCode, cancellationToken);
        }

        private async Task<T> ExecuteWithSetupPoliciesAsync<T>(string pythonCode, CancellationToken cancellationToken) {
            // Setup-specific policies could include:
            // - Longer timeout for setup operations
            // - Capability-aware initialization
            // - Resource pre-allocation

            // For now, direct execution (policies would be added here)
            return await communication.ExecuteAsync<T>(pythonCode, cancellationToken);
        }
    }
}
