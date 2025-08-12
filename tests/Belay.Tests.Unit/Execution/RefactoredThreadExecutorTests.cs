// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit.Execution {
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
    /// Refactored tests for ThreadExecutor functionality without session management dependencies.
    /// These tests validate the simplified executor approach using DeviceState instead of sessions.
    /// </summary>
    [TestFixture]
    public class RefactoredThreadExecutorTests {
        private Mock<IDeviceCommunication> mockCommunication = null!;
        private Mock<ILogger> mockLogger = null!;
        private TestableThreadExecutor executor = null!;

        [SetUp]
        public void SetUp() {
            mockCommunication = new Mock<IDeviceCommunication>();
            mockLogger = new Mock<ILogger>();

            // Mock device is connected and supports threading
            mockCommunication.Setup(c => c.State).Returns(DeviceConnectionState.Connected);

            executor = new TestableThreadExecutor(mockCommunication.Object, mockLogger.Object);
        }

        [Test]
        public void Constructor_WithNullCommunication_ThrowsArgumentNullException() {
            // Act & Assert
            FluentActions
                .Invoking(() => new TestableThreadExecutor(null!, mockLogger.Object))
                .Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("communication");
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException() {
            // Act & Assert
            FluentActions
                .Invoking(() => new TestableThreadExecutor(mockCommunication.Object, null!))
                .Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Test]
        public async Task StartThreadAsync_WithBasicCode_StartsSuccessfully() {
            // Arrange
            const string pythonCode = """
                import _thread
                import time

                def worker():
                    while True:
                        print('Thread working')
                        time.sleep(1)

                _thread.start_new_thread(worker, ())
                """;
            const string expectedThreadId = "thread_001";

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedThreadId);

            // Act
            var result = await executor.StartThreadAsync<string>(pythonCode, "WorkerThread");

            // Assert
            result.Should().Be(expectedThreadId);
            executor.ActiveThreadCount.Should().Be(1);
            mockCommunication.Verify(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task StartThreadAsync_WithNullCode_ThrowsArgumentException() {
            // Act & Assert
            await FluentActions
                .Invoking(async () => await executor.StartThreadAsync<string>(null!, "test"))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Python code cannot be null or empty*")
                .WithParameterName("pythonCode");
        }

        [Test]
        public async Task StartThreadAsync_TracksThreadOperation_InDeviceState() {
            // Arrange
            const string pythonCode = "import _thread; _thread.start_new_thread(lambda: None, ())";
            const string threadName = "TestThread";

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async () => {
                    await Task.Delay(10); // Small delay to ensure operation tracking is visible
                    return "thread_id";
                });

            // Act
            var task = executor.StartThreadAsync<string>(pythonCode, threadName);

            // Assert - operation should be tracked during execution
            executor.State.CurrentOperation.Should().Be($"StartThread:{threadName}");

            var result = await task;

            // Assert - operation should be completed after execution
            result.Should().Be("thread_id");
            executor.State.CurrentOperation.Should().BeNull();
            executor.State.LastOperationTime.Should().NotBeNull();
        }

        [Test]
        public async Task StopThreadAsync_WithValidThreadName_StopsSuccessfully() {
            // Arrange
            const string threadName = "WorkerThread";
            const string threadId = "thread_001";

            // Start a thread first
            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(threadId);

            await executor.StartThreadAsync<string>("test_code", threadName);

            // Mock stop operation
            mockCommunication
                .Setup(c => c.ExecuteAsync<bool>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await executor.StopThreadAsync(threadName);

            // Assert
            result.Should().BeTrue();
            executor.ActiveThreadCount.Should().Be(0);
            mockCommunication.Verify(c => c.ExecuteAsync<bool>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task StopThreadAsync_WithInvalidThreadName_ReturnsFalse() {
            // Arrange
            const string threadName = "NonExistentThread";

            mockCommunication
                .Setup(c => c.ExecuteAsync<bool>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await executor.StopThreadAsync(threadName);

            // Assert
            result.Should().BeFalse();
            executor.ActiveThreadCount.Should().Be(0);
        }

        [Test]
        public async Task StopAllThreadsAsync_WithMultipleThreads_StopsAllThreads() {
            // Arrange - Start multiple threads
            var threadNames = new[] { "Thread1", "Thread2", "Thread3" };

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("thread_id");

            foreach (var threadName in threadNames) {
                await executor.StartThreadAsync<string>("test_code", threadName);
            }

            // Mock stop operations
            mockCommunication
                .Setup(c => c.ExecuteAsync<bool>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var stoppedCount = await executor.StopAllThreadsAsync();

            // Assert
            stoppedCount.Should().Be(3);
            executor.ActiveThreadCount.Should().Be(0);
        }

        [Test]
        public async Task CheckThreadHealthAsync_WithActiveThreads_ReturnsHealthStatus() {
            // Arrange
            const string threadName = "HealthCheckThread";
            var healthStatus = new Dictionary<string, object> {
                ["active_threads"] = 1,
                ["memory_usage"] = 12345,
                ["cpu_usage"] = 15.5
            };

            // Start a thread
            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("thread_id");

            await executor.StartThreadAsync<string>("test_code", threadName);

            // Mock health check
            mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(healthStatus);

            // Act
            var result = await executor.CheckThreadHealthAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().ContainKey("active_threads").WhoseValue.Should().Be(1);
            result.Should().ContainKey("memory_usage").WhoseValue.Should().Be(12345);
        }

        [Test]
        public async Task StartThreadAsync_WithThreadingNotSupported_ThrowsNotSupportedException() {
            // Arrange
            var capabilities = new SimpleDeviceCapabilities {
                Platform = "basic_device",
                SupportedFeatures = SimpleDeviceFeatureSet.GPIO, // No threading support
                DetectionComplete = true
            };

            executor.State.Capabilities = capabilities;

            // Act & Assert
            await FluentActions
                .Invoking(async () => await executor.StartThreadAsync<string>("test_code", "TestThread"))
                .Should().ThrowAsync<NotSupportedException>()
                .WithMessage("*Threading is not supported on this device*");
        }

        [Test]
        public async Task StartThreadAsync_WithThreadingSupported_ExecutesSuccessfully() {
            // Arrange
            var capabilities = new SimpleDeviceCapabilities {
                Platform = "esp32",
                SupportedFeatures = SimpleDeviceFeatureSet.GPIO | SimpleDeviceFeatureSet.Threading,
                DetectionComplete = true
            };

            executor.State.Capabilities = capabilities;

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("thread_id");

            // Act
            var result = await executor.StartThreadAsync<string>("test_code", "TestThread");

            // Assert
            result.Should().Be("thread_id");
        }

        [Test]
        public async Task StartThreadAsync_WithCancellation_PropagatesCancellation() {
            // Arrange
            const string pythonCode = "import time; time.sleep(10); start_thread()";
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((_, token) => Task.FromCanceled<string>(token));

            cts.Cancel();

            // Act & Assert
            await FluentActions
                .Invoking(async () => await executor.StartThreadAsync<string>(pythonCode, "TestThread", cts.Token))
                .Should().ThrowAsync<TaskCanceledException>();
        }

        [Test]
        public void State_InitialState_IsCorrect() {
            // Assert
            executor.State.Should().NotBeNull();
            executor.State.Capabilities.Should().BeNull();
            executor.State.CurrentOperation.Should().BeNull();
            executor.State.LastOperationTime.Should().BeNull();
            executor.State.ConnectionState.Should().Be(DeviceConnectionState.Connected);
            executor.ActiveThreadCount.Should().Be(0);
        }

        [Test]
        public async Task ClearThreadCacheAsync_RemovesThreadHistory() {
            // Arrange - Start a thread to create cache entries
            mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("thread_id");

            await executor.StartThreadAsync<string>("test_code", "CacheThread");

            // Act
            await executor.ClearThreadCacheAsync();

            // Assert - Thread history should be reset
            executor.ActiveThreadCount.Should().Be(0);
        }
    }

    /// <summary>
    /// Testable implementation of ThreadExecutor for the refactored architecture.
    /// This represents how ThreadExecutor would work without session management dependencies.
    /// </summary>
    public class TestableThreadExecutor {
        private readonly IDeviceCommunication communication;
        private readonly ILogger logger;
        private readonly Dictionary<string, string> activeThreads = new();

        public DeviceState State { get; } = new DeviceState();
        public int ActiveThreadCount => activeThreads.Count;

        public TestableThreadExecutor(IDeviceCommunication communication, ILogger logger) {
            this.communication = communication ?? throw new ArgumentNullException(nameof(communication));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize state with connection state from communication
            State.ConnectionState = this.communication.State;
        }

        public async Task<T> StartThreadAsync<T>(
            string pythonCode,
            string threadName,
            CancellationToken cancellationToken = default) {
            // Validate input
            if (string.IsNullOrWhiteSpace(pythonCode)) {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            if (string.IsNullOrWhiteSpace(threadName)) {
                throw new ArgumentException("Thread name cannot be null or empty", nameof(threadName));
            }

            // Check threading capability
            if (State.Capabilities?.DetectionComplete == true &&
                !State.Capabilities.SupportsFeature(SimpleDeviceFeatureSet.Threading)) {
                throw new NotSupportedException("Threading is not supported on this device platform");
            }

            // Track operation in state
            State.SetCurrentOperation($"StartThread:{threadName}");

            try {
                logger.LogDebug("Starting thread '{ThreadName}' with code: {Code}",
                    threadName, pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

                // Execute thread start code
                var result = await ExecuteWithThreadPoliciesAsync<T>(pythonCode, cancellationToken);

                // Track active thread
                activeThreads[threadName] = result?.ToString() ?? "unknown_id";

                logger.LogDebug("Thread '{ThreadName}' started successfully", threadName);

                return result;
            }
            finally {
                // Complete operation tracking
                State.CompleteOperation();
            }
        }

        public async Task<bool> StopThreadAsync(
            string threadName,
            CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(threadName)) {
                throw new ArgumentException("Thread name cannot be null or empty", nameof(threadName));
            }

            // Track operation in state
            State.SetCurrentOperation($"StopThread:{threadName}");

            try {
                logger.LogDebug("Stopping thread '{ThreadName}'", threadName);

                // Generate stop thread code
                var stopCode = $"stop_thread('{threadName}')";
                var result = await communication.ExecuteAsync<bool>(stopCode, cancellationToken);

                if (result) {
                    activeThreads.Remove(threadName);
                    logger.LogDebug("Thread '{ThreadName}' stopped successfully", threadName);
                }

                return result;
            }
            finally {
                State.CompleteOperation();
            }
        }

        public async Task<int> StopAllThreadsAsync(CancellationToken cancellationToken = default) {
            State.SetCurrentOperation("StopAllThreads");

            try {
                var stoppedCount = 0;
                var threadsToStop = activeThreads.Keys.ToList();

                foreach (var threadName in threadsToStop) {
                    var stopped = await StopThreadAsync(threadName, cancellationToken);
                    if (stopped) {
                        stoppedCount++;
                    }
                }

                return stoppedCount;
            }
            finally {
                State.CompleteOperation();
            }
        }

        public async Task<Dictionary<string, object>> CheckThreadHealthAsync(CancellationToken cancellationToken = default) {
            State.SetCurrentOperation("CheckThreadHealth");

            try {
                const string healthCheckCode = """
                    import gc
                    result = {
                        'active_threads': len(active_threads),
                        'memory_usage': gc.mem_alloc(),
                        'free_memory': gc.mem_free()
                    }
                    """;

                return await communication.ExecuteAsync<Dictionary<string, object>>(healthCheckCode, cancellationToken);
            }
            finally {
                State.CompleteOperation();
            }
        }

        public async Task ClearThreadCacheAsync(CancellationToken cancellationToken = default) {
            State.SetCurrentOperation("ClearThreadCache");

            try {
                // Clear local thread tracking
                activeThreads.Clear();

                // Execute device-side thread cache clearing
                const string clearCode = "clear_thread_cache()";
                await communication.ExecuteAsync(clearCode, cancellationToken);
            }
            finally {
                State.CompleteOperation();
            }
        }

        private async Task<T> ExecuteWithThreadPoliciesAsync<T>(string pythonCode, CancellationToken cancellationToken) {
            // Thread-specific policies could include:
            // - Thread safety checks
            // - Resource allocation verification
            // - Thread limit enforcement
            // - Priority-based execution

            // For now, direct execution (policies would be added here)
            return await communication.ExecuteAsync<T>(pythonCode, cancellationToken);
        }
    }
}
