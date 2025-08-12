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
    /// Refactored tests for ThreadExecutor functionality without session management dependencies.
    /// These tests validate the simplified executor approach using DeviceState instead of sessions.
    /// </summary>
    [TestFixture]
    public class RefactoredThreadExecutorTests
    {
        private Mock<IDeviceCommunication> mockCommunication = null!;
        private Mock<ILogger> mockLogger = null!;
        private TestableThreadExecutor executor = null!;

        [SetUp]
        public void SetUp()
        {
            this.mockCommunication = new Mock<IDeviceCommunication>();
            this.mockLogger = new Mock<ILogger>();

            // Mock device is connected and supports threading
            this.mockCommunication.Setup(c => c.State).Returns(DeviceConnectionState.Connected);

            this.executor = new TestableThreadExecutor(this.mockCommunication.Object, this.mockLogger.Object);
        }

        [Test]
        public void Constructor_WithNullCommunication_ThrowsArgumentNullException()
        {
            // Act & Assert
            FluentActions
                .Invoking(() => new TestableThreadExecutor(null!, this.mockLogger.Object))
                .Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("communication");
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            FluentActions
                .Invoking(() => new TestableThreadExecutor(this.mockCommunication.Object, null!))
                .Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [Test]
        public async Task StartThreadAsync_WithBasicCode_StartsSuccessfully()
        {
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

            this.mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedThreadId);

            // Act
            var result = await this.executor.StartThreadAsync<string>(pythonCode, "WorkerThread");

            // Assert
            result.Should().Be(expectedThreadId);
            this.executor.ActiveThreadCount.Should().Be(1);
            this.mockCommunication.Verify(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task StartThreadAsync_WithNullCode_ThrowsArgumentException()
        {
            // Act & Assert
            await FluentActions
                .Invoking(async () => await this.executor.StartThreadAsync<string>(null!, "test"))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Python code cannot be null or empty*")
                .WithParameterName("pythonCode");
        }

        [Test]
        public async Task StartThreadAsync_TracksThreadOperation_InDeviceState()
        {
            // Arrange
            const string pythonCode = "import _thread; _thread.start_new_thread(lambda: None, ())";
            const string threadName = "TestThread";

            this.mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await Task.Delay(10); // Small delay to ensure operation tracking is visible
                    return "thread_id";
                });

            // Act
            var task = this.executor.StartThreadAsync<string>(pythonCode, threadName);

            // Assert - operation should be tracked during execution
            this.executor.State.CurrentOperation.Should().Be($"StartThread:{threadName}");

            var result = await task;

            // Assert - operation should be completed after execution
            result.Should().Be("thread_id");
            this.executor.State.CurrentOperation.Should().BeNull();
            this.executor.State.LastOperationTime.Should().NotBeNull();
        }

        [Test]
        public async Task StopThreadAsync_WithValidThreadName_StopsSuccessfully()
        {
            // Arrange
            const string threadName = "WorkerThread";
            const string threadId = "thread_001";

            // Start a thread first
            this.mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(threadId);

            await this.executor.StartThreadAsync<string>("test_code", threadName);

            // Mock stop operation
            this.mockCommunication
                .Setup(c => c.ExecuteAsync<bool>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await this.executor.StopThreadAsync(threadName);

            // Assert
            result.Should().BeTrue();
            this.executor.ActiveThreadCount.Should().Be(0);
            this.mockCommunication.Verify(c => c.ExecuteAsync<bool>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task StopThreadAsync_WithInvalidThreadName_ReturnsFalse()
        {
            // Arrange
            const string threadName = "NonExistentThread";

            this.mockCommunication
                .Setup(c => c.ExecuteAsync<bool>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await this.executor.StopThreadAsync(threadName);

            // Assert
            result.Should().BeFalse();
            this.executor.ActiveThreadCount.Should().Be(0);
        }

        [Test]
        public async Task StopAllThreadsAsync_WithMultipleThreads_StopsAllThreads()
        {
            // Arrange - Start multiple threads
            var threadNames = new[] { "Thread1", "Thread2", "Thread3" };
            
            this.mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("thread_id");

            foreach (var threadName in threadNames)
            {
                await this.executor.StartThreadAsync<string>("test_code", threadName);
            }

            // Mock stop operations
            this.mockCommunication
                .Setup(c => c.ExecuteAsync<bool>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var stoppedCount = await this.executor.StopAllThreadsAsync();

            // Assert
            stoppedCount.Should().Be(3);
            this.executor.ActiveThreadCount.Should().Be(0);
        }

        [Test]
        public async Task CheckThreadHealthAsync_WithActiveThreads_ReturnsHealthStatus()
        {
            // Arrange
            const string threadName = "HealthCheckThread";
            var healthStatus = new Dictionary<string, object>
            {
                ["active_threads"] = 1,
                ["memory_usage"] = 12345,
                ["cpu_usage"] = 15.5
            };

            // Start a thread
            this.mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("thread_id");

            await this.executor.StartThreadAsync<string>("test_code", threadName);

            // Mock health check
            this.mockCommunication
                .Setup(c => c.ExecuteAsync<Dictionary<string, object>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(healthStatus);

            // Act
            var result = await this.executor.CheckThreadHealthAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().ContainKey("active_threads").WhoseValue.Should().Be(1);
            result.Should().ContainKey("memory_usage").WhoseValue.Should().Be(12345);
        }

        [Test]
        public async Task StartThreadAsync_WithThreadingNotSupported_ThrowsNotSupportedException()
        {
            // Arrange
            var capabilities = new SimpleDeviceCapabilities
            {
                Platform = "basic_device",
                SupportedFeatures = SimpleDeviceFeatureSet.GPIO, // No threading support
                DetectionComplete = true
            };

            this.executor.State.Capabilities = capabilities;

            // Act & Assert
            await FluentActions
                .Invoking(async () => await this.executor.StartThreadAsync<string>("test_code", "TestThread"))
                .Should().ThrowAsync<NotSupportedException>()
                .WithMessage("*Threading is not supported on this device*");
        }

        [Test]
        public async Task StartThreadAsync_WithThreadingSupported_ExecutesSuccessfully()
        {
            // Arrange
            var capabilities = new SimpleDeviceCapabilities
            {
                Platform = "esp32",
                SupportedFeatures = SimpleDeviceFeatureSet.GPIO | SimpleDeviceFeatureSet.Threading,
                DetectionComplete = true
            };

            this.executor.State.Capabilities = capabilities;

            this.mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("thread_id");

            // Act
            var result = await this.executor.StartThreadAsync<string>("test_code", "TestThread");

            // Assert
            result.Should().Be("thread_id");
        }

        [Test]
        public async Task StartThreadAsync_WithCancellation_PropagatesCancellation()
        {
            // Arrange
            const string pythonCode = "import time; time.sleep(10); start_thread()";
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            this.mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((_, token) => Task.FromCanceled<string>(token));

            cts.Cancel();

            // Act & Assert
            await FluentActions
                .Invoking(async () => await this.executor.StartThreadAsync<string>(pythonCode, "TestThread", cts.Token))
                .Should().ThrowAsync<TaskCanceledException>();
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
            this.executor.ActiveThreadCount.Should().Be(0);
        }

        [Test]
        public async Task ClearThreadCacheAsync_RemovesThreadHistory()
        {
            // Arrange - Start a thread to create cache entries
            this.mockCommunication
                .Setup(c => c.ExecuteAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("thread_id");

            await this.executor.StartThreadAsync<string>("test_code", "CacheThread");

            // Act
            await this.executor.ClearThreadCacheAsync();

            // Assert - Thread history should be reset
            this.executor.ActiveThreadCount.Should().Be(0);
        }
    }

    /// <summary>
    /// Testable implementation of ThreadExecutor for the refactored architecture.
    /// This represents how ThreadExecutor would work without session management dependencies.
    /// </summary>
    public class TestableThreadExecutor
    {
        private readonly IDeviceCommunication communication;
        private readonly ILogger logger;
        private readonly Dictionary<string, string> activeThreads = new();

        public DeviceState State { get; } = new DeviceState();
        public int ActiveThreadCount => this.activeThreads.Count;

        public TestableThreadExecutor(IDeviceCommunication communication, ILogger logger)
        {
            this.communication = communication ?? throw new ArgumentNullException(nameof(communication));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize state with connection state from communication
            this.State.ConnectionState = this.communication.State;
        }

        public async Task<T> StartThreadAsync<T>(
            string pythonCode,
            string threadName,
            CancellationToken cancellationToken = default)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(pythonCode))
            {
                throw new ArgumentException("Python code cannot be null or empty", nameof(pythonCode));
            }

            if (string.IsNullOrWhiteSpace(threadName))
            {
                throw new ArgumentException("Thread name cannot be null or empty", nameof(threadName));
            }

            // Check threading capability
            if (this.State.Capabilities?.DetectionComplete == true &&
                !this.State.Capabilities.SupportsFeature(SimpleDeviceFeatureSet.Threading))
            {
                throw new NotSupportedException("Threading is not supported on this device platform");
            }

            // Track operation in state
            this.State.SetCurrentOperation($"StartThread:{threadName}");

            try
            {
                this.logger.LogDebug("Starting thread '{ThreadName}' with code: {Code}", 
                    threadName, pythonCode.Length > 100 ? $"{pythonCode[..100]}..." : pythonCode);

                // Execute thread start code
                var result = await this.ExecuteWithThreadPoliciesAsync<T>(pythonCode, cancellationToken);

                // Track active thread
                this.activeThreads[threadName] = result?.ToString() ?? "unknown_id";

                this.logger.LogDebug("Thread '{ThreadName}' started successfully", threadName);

                return result;
            }
            finally
            {
                // Complete operation tracking
                this.State.CompleteOperation();
            }
        }

        public async Task<bool> StopThreadAsync(
            string threadName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(threadName))
            {
                throw new ArgumentException("Thread name cannot be null or empty", nameof(threadName));
            }

            // Track operation in state
            this.State.SetCurrentOperation($"StopThread:{threadName}");

            try
            {
                this.logger.LogDebug("Stopping thread '{ThreadName}'", threadName);

                // Generate stop thread code
                var stopCode = $"stop_thread('{threadName}')";
                var result = await this.communication.ExecuteAsync<bool>(stopCode, cancellationToken);

                if (result)
                {
                    this.activeThreads.Remove(threadName);
                    this.logger.LogDebug("Thread '{ThreadName}' stopped successfully", threadName);
                }

                return result;
            }
            finally
            {
                this.State.CompleteOperation();
            }
        }

        public async Task<int> StopAllThreadsAsync(CancellationToken cancellationToken = default)
        {
            this.State.SetCurrentOperation("StopAllThreads");

            try
            {
                var stoppedCount = 0;
                var threadsToStop = this.activeThreads.Keys.ToList();

                foreach (var threadName in threadsToStop)
                {
                    var stopped = await this.StopThreadAsync(threadName, cancellationToken);
                    if (stopped)
                    {
                        stoppedCount++;
                    }
                }

                return stoppedCount;
            }
            finally
            {
                this.State.CompleteOperation();
            }
        }

        public async Task<Dictionary<string, object>> CheckThreadHealthAsync(CancellationToken cancellationToken = default)
        {
            this.State.SetCurrentOperation("CheckThreadHealth");

            try
            {
                const string healthCheckCode = """
                    import gc
                    result = {
                        'active_threads': len(active_threads),
                        'memory_usage': gc.mem_alloc(),
                        'free_memory': gc.mem_free()
                    }
                    """;

                return await this.communication.ExecuteAsync<Dictionary<string, object>>(healthCheckCode, cancellationToken);
            }
            finally
            {
                this.State.CompleteOperation();
            }
        }

        public async Task ClearThreadCacheAsync(CancellationToken cancellationToken = default)
        {
            this.State.SetCurrentOperation("ClearThreadCache");

            try
            {
                // Clear local thread tracking
                this.activeThreads.Clear();

                // Execute device-side thread cache clearing
                const string clearCode = "clear_thread_cache()";
                await this.communication.ExecuteAsync(clearCode, cancellationToken);
            }
            finally
            {
                this.State.CompleteOperation();
            }
        }

        private async Task<T> ExecuteWithThreadPoliciesAsync<T>(string pythonCode, CancellationToken cancellationToken)
        {
            // Thread-specific policies could include:
            // - Thread safety checks
            // - Resource allocation verification
            // - Thread limit enforcement
            // - Priority-based execution

            // For now, direct execution (policies would be added here)
            return await this.communication.ExecuteAsync<T>(pythonCode, cancellationToken);
        }
    }
}