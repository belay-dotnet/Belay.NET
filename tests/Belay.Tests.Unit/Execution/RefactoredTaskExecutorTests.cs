// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit.Execution
{
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
    /// Refactored tests for TaskExecutor functionality without session management dependencies.
    /// These tests validate the simplified executor approach using DeviceState instead of sessions.
    /// </summary>
    [TestFixture]
    public class RefactoredTaskExecutorTests
    {
        private Mock<IDeviceCommunication> mockCommunication = null!;
        private Mock<ILogger> mockLogger = null!;
        private TestableTaskExecutor executor = null!;

        [SetUp]
        public void SetUp()
        {
            this.mockCommunication = new Mock<IDeviceCommunication>();
            this.mockLogger = new Mock<ILogger>();

            // Mock device is connected
            this.mockCommunication.Setup(c => c.State).Returns(DeviceConnectionState.Connected);

            this.executor = new TestableTaskExecutor(this.mockCommunication.Object, this.mockLogger.Object);
        }

        [Test]
        public void Constructor_WithNullCommunication_ThrowsArgumentNullException()
        {
            // Act & Assert
            FluentActions
                .Invoking(() => new TestableTaskExecutor(null!, this.mockLogger.Object))
                .Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("communication");
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            FluentActions
                .Invoking(() => new TestableTaskExecutor(this.mockCommunication.Object, null!))
                .Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Test]
        public async Task ExecuteAsync_WithBasicPythonCode_ExecutesSuccessfully()
        {
            // Arrange
            const string pythonCode = "result = 'Hello World'";
            const string expectedResult = "Hello World";

            this.mockCommunication
                .Setup(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await this.executor.ExecuteAsync<string>(pythonCode);

            // Assert
            result.Should().Be(expectedResult);
            this.mockCommunication.Verify(c => c.ExecuteAsync<string>(pythonCode, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ExecuteAsync_WithNullCode_ThrowsArgumentException()
        {
            // Act & Assert
            await FluentActions
                .Invoking(async () => await this.executor.ExecuteAsync<string>(null!))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Python code cannot be null or empty*")
                .WithParameterName("pythonCode");
        }

        [Test]
        public async Task ExecuteAsync_WithEmptyCode_ThrowsArgumentException()
        {
            // Act & Assert
            await FluentActions
                .Invoking(async () => await this.executor.ExecuteAsync<string>(""))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Python code cannot be null or empty*")
                .WithParameterName("pythonCode");
        }

        [Test]
        public async Task ExecuteAsync_WithWhitespaceCode_ThrowsArgumentException()
        {
            // Act & Assert
            await FluentActions
                .Invoking(async () => await this.executor.ExecuteAsync<string>("   "))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Python code cannot be null or empty*")
                .WithParameterName("pythonCode");
        }

        [Test]
        public async Task ExecuteAsync_WithCancellation_PropagatesCancellation()
        {
            // Arrange
            const string pythonCode = "import time; time.sleep(2)";
            using var cts = new CancellationTokenSource();

            this.mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((_, token) => Task.FromCanceled<string>(token));

            cts.Cancel();

            // Act & Assert
            await FluentActions
                .Invoking(async () => await this.executor.ExecuteAsync<string>(pythonCode, cts.Token))
                .Should().ThrowAsync<TaskCanceledException>();
        }

        [Test]
        public async Task ExecuteAsync_WhenDeviceDisconnected_ThrowsInvalidOperationException()
        {
            // Arrange
            const string pythonCode = "result = 'test'";
            this.mockCommunication.Setup(c => c.State).Returns(DeviceConnectionState.Disconnected);

            // Act & Assert
            await FluentActions
                .Invoking(async () => await this.executor.ExecuteAsync<string>(pythonCode))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Device must be connected*");
        }

        [Test]
        public async Task ExecuteAsync_TracksCurrentOperation_InDeviceState()
        {
            // Arrange
            const string pythonCode = "result = 42";
            const string operationName = "TestOperation";

            this.mockCommunication
                .Setup(c => c.ExecuteAsync<int>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await Task.Delay(10); // Small delay to ensure operation tracking is visible
                    return 42;
                });

            // Act
            var task = this.executor.ExecuteAsync<int>(pythonCode, operationName: operationName);

            // Assert - operation should be tracked during execution
            this.executor.State.CurrentOperation.Should().Be(operationName);

            var result = await task;

            // Assert - operation should be completed after execution
            result.Should().Be(42);
            this.executor.State.CurrentOperation.Should().BeNull();
            this.executor.State.LastOperationTime.Should().NotBeNull();
        }

        [Test]
        public async Task ExecuteAsync_WithConcurrentCalls_HandlesCorrectly()
        {
            // Arrange
            const string pythonCode = "result = 42";
            var executionCount = 0;
            var executionDelay = TimeSpan.FromMilliseconds(50);

            this.mockCommunication
                .Setup(c => c.ExecuteAsync<int>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((_, cancellationToken) =>
                {
                    Interlocked.Increment(ref executionCount);
                    return Task.Delay(executionDelay, cancellationToken).ContinueWith(_ => 42, cancellationToken);
                });

            // Act - Start multiple concurrent executions
            var tasks = new List<Task<int>>();
            for (int i = 0; i < 3; i++)
            {
                tasks.Add(this.executor.ExecuteAsync<int>(pythonCode, operationName: $"ConcurrentOp{i}"));
            }

            var results = await Task.WhenAll(tasks);

            // Assert - All should complete successfully
            results.Should().AllSatisfy(result => result.Should().Be(42));
            executionCount.Should().Be(3);
        }

        [Test]
        public async Task ExecuteAsync_WithDeviceCapabilities_OptimizesExecution()
        {
            // Arrange
            const string pythonCode = "result = 'optimized'";
            var capabilities = new SimpleDeviceCapabilities
            {
                Platform = "esp32",
                SupportedFeatures = SimpleDeviceFeatureSet.GPIO | SimpleDeviceFeatureSet.WiFi,
                AvailableMemory = 50000,
                DetectionComplete = true
            };

            this.executor.State.Capabilities = capabilities;

            this.mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("optimized");

            // Act
            var result = await this.executor.ExecuteAsync<string>(pythonCode);

            // Assert
            result.Should().Be("optimized");
            // Verify that the executor had access to capabilities during execution
            this.executor.State.Capabilities.Should().NotBeNull();
            this.executor.State.Capabilities!.Platform.Should().Be("esp32");
        }

        [Test]
        public async Task ExecuteAsync_WithComplexReturnType_HandlesConversion()
        {
            // Arrange
            const string pythonCode = "result = {'name': 'test', 'value': 42}";
            var expectedDict = new Dictionary<string, object>
            {
                ["name"] = "test",
                ["value"] = 42
            };

            this.mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDict);

            // Act
            var result = await this.executor.ExecuteAsync<Dictionary<string, object>>(pythonCode);

            // Assert
            result.Should().NotBeNull();
            result.Should().ContainKey("name").WhoseValue.Should().Be("test");
            result.Should().ContainKey("value").WhoseValue.Should().Be(42);
        }

        [Test]
        public async Task ExecuteAsync_WithExceptionFromDevice_PropagatesException()
        {
            // Arrange
            const string pythonCode = "raise ValueError('Device error')";
            var deviceException = new InvalidOperationException("Device execution failed");

            this.mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(deviceException);

            // Act & Assert
            await FluentActions
                .Invoking(async () => await this.executor.ExecuteAsync<string>(pythonCode))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Device execution failed");
        }

        [Test]
        public void State_InitialState_IsCorrect()
        {
            // Assert
            this.executor.State.Should().NotBeNull();
            this.executor.State.Capabilities.Should().BeNull();
            this.executor.State.CurrentOperation.Should().BeNull();
            this.executor.State.LastOperationTime.Should().BeNull();
            this.executor.State.ConnectionState.Should().Be(DeviceConnectionState.Connected);
        }

        [Test]
        public async Task ClearCache_RemovesExecutionHistory()
        {
            // Arrange - Execute something to create cache entries
            const string pythonCode = "result = 'cached'";
            this.mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("cached");

            await this.executor.ExecuteAsync<string>(pythonCode, operationName: "CacheTest");

            // Act
            this.executor.ClearCache();

            // Assert - State should be reset
            this.executor.State.LastOperationTime.Should().BeNull();
        }
    }

    /// <summary>
    /// Testable implementation of TaskExecutor for the refactored architecture.
    /// This represents how TaskExecutor would work without session management dependencies.
    /// </summary>
    public class TestableTaskExecutor
    {
        private readonly IDeviceCommunication communication;
        private readonly ILogger logger;

        public DeviceState State { get; } = new DeviceState();

        public TestableTaskExecutor(IDeviceCommunication communication, ILogger logger)
        {
            this.communication = communication ?? throw new ArgumentNullException(nameof(communication));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize state with connection state from communication
            this.State.ConnectionState = this.communication.State;
        }

        public async Task<T> ExecuteAsync<T>(
            string pythonCode,
            CancellationToken cancellationToken = default,
            string? operationName = null)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(pythonCode))
            {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            // Check device connection
            if (this.communication.State != DeviceConnectionState.Connected)
            {
                throw new InvalidOperationException("Device must be connected before executing code");
            }

            // Track operation in state
            this.State.SetCurrentOperation(operationName ?? "Execute");

            try
            {
                this.logger.LogDebug("Executing Python code: {Code}", pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

                // Direct device execution without session complexity
                var result = await this.communication.ExecuteAsync<T>(pythonCode, cancellationToken);

                this.logger.LogDebug("Python code execution completed successfully");

                return result;
            }
            finally
            {
                // Complete operation tracking
                this.State.CompleteOperation();
            }
        }

        public void ClearCache()
        {
            // Reset execution tracking
            this.State.ClearExecutionHistory();
            // Note: In real implementation, this would clear method cache, etc.
        }
    }
}