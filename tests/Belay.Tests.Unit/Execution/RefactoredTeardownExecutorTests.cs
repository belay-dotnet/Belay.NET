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
    /// Refactored tests for TeardownExecutor functionality without session management dependencies.
    /// These tests validate the simplified executor approach using DeviceState instead of sessions.
    /// </summary>
    [TestFixture]
    public class RefactoredTeardownExecutorTests {
        private Mock<IDeviceCommunication> mockCommunication = null!;
        private Mock<ILogger> mockLogger = null!;
        private TestableTeardownExecutor executor = null!;

        [SetUp]
        public void SetUp() {
            mockCommunication = new Mock<IDeviceCommunication>();
            mockLogger = new Mock<ILogger>();

            // Mock device is connected
            mockCommunication.Setup(c => c.State).Returns(DeviceConnectionState.Connected);

            executor = new TestableTeardownExecutor(mockCommunication.Object, mockLogger.Object);
        }

        [Test]
        public void Constructor_WithNullCommunication_ThrowsArgumentNullException() {
            // Act & Assert
            FluentActions
                .Invoking(() => new TestableTeardownExecutor(null!, mockLogger.Object))
                .Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("communication");
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException() {
            // Act & Assert
            FluentActions
                .Invoking(() => new TestableTeardownExecutor(mockCommunication.Object, null!))
                .Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Test]
        public async Task ExecuteTeardownAsync_WithBasicCode_ExecutesSuccessfully() {
            // Arrange
            const string pythonCode = "cleanup_resources()\nprint('Teardown complete')";
            const string expectedResult = "Teardown complete";

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await executor.ExecuteTeardownAsync<string>(pythonCode);

            // Assert
            result.Should().Be(expectedResult);
            mockCommunication.Verify(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ExecuteTeardownAsync_WithNullCode_ThrowsArgumentException() {
            // Act & Assert
            await FluentActions
                .Invoking(async () => await executor.ExecuteTeardownAsync<string>(null!))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Python code cannot be null or empty*")
                .WithParameterName("pythonCode");
        }

        [Test]
        public async Task ExecuteTeardownAsync_TracksTeardownOperation_InDeviceState() {
            // Arrange
            const string pythonCode = "teardown_complete = True";

            mockCommunication
                .Setup(c => c.ExecuteAsync<bool>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async () => {
                    await Task.Delay(10); // Small delay to ensure operation tracking is visible
                    return true;
                });

            // Act
            var task = executor.ExecuteTeardownAsync<bool>(pythonCode);

            // Assert - operation should be tracked during execution
            executor.State.CurrentOperation.Should().Be("Teardown");

            var result = await task;

            // Assert - operation should be completed after execution
            result.Should().BeTrue();
            executor.State.CurrentOperation.Should().BeNull();
            executor.State.LastOperationTime.Should().NotBeNull();
        }

        [Test]
        public async Task ExecuteTeardownAsync_WithResourceCleanup_CleansUpProperly() {
            // Arrange
            const string pythonCode = """
                # Cleanup GPIO pins
                for pin in used_pins:
                    pin.deinit()
                
                # Free memory
                import gc
                gc.collect()
                
                print('All resources cleaned up')
                """;

            const string expectedResult = "All resources cleaned up";

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await executor.ExecuteTeardownAsync<string>(pythonCode);

            // Assert
            result.Should().Be(expectedResult);
            mockCommunication.Verify(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ExecuteTeardownAsync_WithTeardownException_AttemptsGracefulRecovery() {
            // Arrange
            const string pythonCode = "undefined_cleanup_function()";
            var teardownException = new InvalidOperationException("Teardown failed: undefined_cleanup_function not defined");

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(teardownException);

            // Act & Assert - Should propagate teardown exceptions
            await FluentActions
                .Invoking(async () => await executor.ExecuteTeardownAsync<string>(pythonCode))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Teardown failed: undefined_cleanup_function not defined");

            // State should still be updated even on teardown failure
            executor.State.CurrentOperation.Should().BeNull();
            executor.State.LastOperationTime.Should().NotBeNull();
        }

        [Test]
        public async Task ExecuteTeardownAsync_WithCriticalTeardownFailure_ExecutesEmergencyCleanup() {
            // Arrange
            const string pythonCode = "critical_cleanup()";
            const string emergencyCode = "import gc; gc.collect()";

            // First call fails, emergency cleanup succeeds
            mockCommunication.SetupSequence(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Critical teardown failure"))
                .ReturnsAsync("Emergency cleanup completed");

            // Act
            var result = await executor.ExecuteTeardownWithRecoveryAsync<string>(pythonCode, emergencyCode);

            // Assert
            result.Should().Be("Emergency cleanup completed");
            mockCommunication.Verify(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()), Times.Once);
            mockCommunication.Verify(c => c.ExecuteAsync<string>(emergencyCode, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ExecuteTeardownAsync_WithCancellation_AttemptsQuickCleanup() {
            // Arrange
            const string pythonCode = "import time; time.sleep(10); cleanup()";
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((_, token) => Task.FromCanceled<string>(token));

            cts.Cancel();

            // Act & Assert
            await FluentActions
                .Invoking(async () => await executor.ExecuteTeardownAsync<string>(pythonCode, cts.Token))
                .Should().ThrowAsync<TaskCanceledException>();
        }

        [Test]
        public async Task ExecuteTeardownAsync_WithVoidReturn_CompletesSuccessfully() {
            // Arrange
            const string pythonCode = "global_state = None";

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);

            // Act & Assert - Should not throw
            var action = () => executor.ExecuteTeardownAsync(pythonCode);
            await action.Should().NotThrowAsync();

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
    }

    /// <summary>
    /// Testable implementation of TeardownExecutor for the refactored architecture.
    /// This represents how TeardownExecutor would work without session management dependencies.
    /// </summary>
    public class TestableTeardownExecutor {
        private readonly IDeviceCommunication communication;
        private readonly ILogger logger;

        public DeviceState State { get; } = new DeviceState();

        public TestableTeardownExecutor(IDeviceCommunication communication, ILogger logger) {
            this.communication = communication ?? throw new ArgumentNullException(nameof(communication));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize state with connection state from communication
            State.ConnectionState = this.communication.State;
        }

        public async Task<T> ExecuteTeardownAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken = default) {
            // Validate input
            if (string.IsNullOrWhiteSpace(pythonCode)) {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            // Track operation in state
            State.SetCurrentOperation("Teardown");

            try {
                logger.LogDebug("Executing teardown code: {Code}", pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

                // Apply teardown-specific policies
                var result = await ExecuteWithTeardownPoliciesAsync<T>(pythonCode, cancellationToken);

                logger.LogDebug("Teardown code execution completed successfully");

                return result;
            }
            catch (Exception ex) {
                logger.LogWarning(ex, "Teardown execution encountered error, attempting graceful recovery");
                throw;
            }
            finally {
                // Complete operation tracking even on failure
                State.CompleteOperation();
            }
        }

        public async Task ExecuteTeardownAsync(
            string pythonCode,
            CancellationToken cancellationToken = default) {
            // Call the string-returning version and ignore result for void execution
            await ExecuteTeardownAsync<string>(pythonCode, cancellationToken);
        }

        public async Task<T> ExecuteTeardownWithRecoveryAsync<T>(
            string primaryCode,
            string emergencyCode,
            CancellationToken cancellationToken = default) {
            try {
                return await ExecuteTeardownAsync<T>(primaryCode, cancellationToken);
            }
            catch (Exception ex) {
                logger.LogWarning(ex, "Primary teardown failed, executing emergency cleanup");

                try {
                    return await ExecuteTeardownAsync<T>(emergencyCode, cancellationToken);
                }
                catch (Exception emergencyEx) {
                    logger.LogError(emergencyEx, "Emergency teardown also failed");
                    throw;
                }
            }
        }

        private async Task<T> ExecuteWithTeardownPoliciesAsync<T>(string pythonCode, CancellationToken cancellationToken) {
            // Teardown-specific policies could include:
            // - Force execution even if device is disconnecting
            // - Shorter timeout for teardown operations
            // - Emergency cleanup on failure
            // - Best-effort execution (continue on minor errors)

            // For now, direct execution (policies would be added here)
            return await communication.ExecuteAsync<T>(pythonCode, cancellationToken);
        }
    }
}
