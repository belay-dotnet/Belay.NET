// Copyright 2025 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Belay.Core;
using Belay.Core.Communication;
using Belay.Core.Execution;
using Belay.Core.Sessions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace Belay.Tests.Unit.Execution {
    /// <summary>
    /// Tests for the ThreadExecutor class.
    /// </summary>
    public class ThreadExecutorTests {
        private readonly IDeviceCommunication _mockCommunication;
        private readonly ILogger<Device> _mockDeviceLogger;
        private readonly ILogger<ThreadExecutor> _mockLogger;
        private readonly Device _device;
        private readonly ThreadExecutor _executor;

        public ThreadExecutorTests() {
            _mockCommunication = Substitute.For<IDeviceCommunication>();
            _mockDeviceLogger = Substitute.For<ILogger<Device>>();
            _mockLogger = Substitute.For<ILogger<ThreadExecutor>>();

            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger<ThreadExecutor>().Returns(_mockLogger);

            _device = new Device(_mockCommunication, _mockDeviceLogger, loggerFactory);
            _executor = _device.Thread;
        }

        [Test]
        public void Constructor_WithNullDevice_ThrowsArgumentNullException() {
            var mockSessionManager = Substitute.For<IDeviceSessionManager>();
            Assert.Throws<ArgumentNullException>(() => new ThreadExecutor(null!, mockSessionManager, _mockLogger));
        }

        [Test]
        public void Constructor_WithNullSessionManager_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new ThreadExecutor(_device, null!, _mockLogger));
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException() {
            var mockSessionManager = Substitute.For<IDeviceSessionManager>();
            Assert.Throws<ArgumentNullException>(() => new ThreadExecutor(_device, mockSessionManager, null!));
        }

        [Test]
        public async Task ApplyPoliciesAndExecuteAsync_WithBasicCode_ExecutesSuccessfully() {
            // Arrange
            const string pythonCode = "print('Hello from thread')";
            const string expectedResult = "Thread started";

            _mockCommunication.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(expectedResult);

            // Act
            var result = await _executor.ApplyPoliciesAndExecuteAsync<string>(pythonCode);

            // Assert
            Assert.AreEqual(expectedResult, result);
            await _mockCommunication.Received(1).ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void ApplyPoliciesAndExecuteAsync_WithNullCode_ThrowsArgumentException() {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => {
                _executor.ApplyPoliciesAndExecuteAsync<string>(null!).GetAwaiter().GetResult();
            });

#pragma warning disable CS8602 // Dereference of a possibly null reference
            StringAssert.Contains("Python code cannot be null or empty", ex.Message);
#pragma warning restore CS8602
        }

        [Test]
        public void ApplyPoliciesAndExecuteAsync_WithEmptyCode_ThrowsArgumentException() {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => {
                _executor.ApplyPoliciesAndExecuteAsync<string>("").GetAwaiter().GetResult();
            });

#pragma warning disable CS8602 // Dereference of a possibly null reference
            StringAssert.Contains("Python code cannot be null or empty", ex.Message);
#pragma warning restore CS8602
        }

        [Test]
        public async Task StopThreadAsync_WithValidThreadName_StopsSuccessfully() {
            // Arrange
            const string threadName = "test_thread";
            _mockCommunication.ExecuteAsync<bool>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(true);

            // Act
            var result = await _executor.StopThreadAsync(threadName);

            // Assert
            Assert.IsTrue(result);
            await _mockCommunication.Received(1).ExecuteAsync<bool>(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task StopThreadAsync_WithInvalidThreadName_ReturnsFalse() {
            // Arrange
            const string threadName = "non_existent_thread";
            _mockCommunication.ExecuteAsync<bool>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(false);

            // Act
            var result = await _executor.StopThreadAsync(threadName);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public async Task CheckThreadHealthAsync_ExecutesSuccessfully() {
            // Arrange
            _mockCommunication.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Act & Assert - Should not throw
            await _executor.CheckThreadHealthAsync();

            await _mockCommunication.Received(1).ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task StopAllThreadsAsync_ReturnsStoppedCount() {
            // Arrange
            const string threadName1 = "thread1";
            const string threadName2 = "thread2";
            const string threadName3 = "thread3";

            // Simulate existing threads
            var threads = new[]
            {
                new RunningThread { 
                    ThreadId = "id1", 
                    MethodName = threadName1, 
                    StartedAt = DateTime.UtcNow, 
                    AutoRestart = false, 
                    Priority = Belay.Attributes.ThreadPriority.Normal,
                    MaxRuntimeMs = null
                },
                new RunningThread { 
                    ThreadId = "id2", 
                    MethodName = threadName2, 
                    StartedAt = DateTime.UtcNow,
                    AutoRestart = false, 
                    Priority = Belay.Attributes.ThreadPriority.Normal,
                    MaxRuntimeMs = null
                },
                new RunningThread { 
                    ThreadId = "id3", 
                    MethodName = threadName3, 
                    StartedAt = DateTime.UtcNow,
                    AutoRestart = false, 
                    Priority = Belay.Attributes.ThreadPriority.Normal,
                    MaxRuntimeMs = null
                }
            };

            var privateField = typeof(ThreadExecutor).GetField("runningThreads", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            privateField?.SetValue(_executor, new System.Collections.Concurrent.ConcurrentDictionary<string, RunningThread>(
                threads.ToDictionary(t => t.MethodName, t => t)));

            // All stop operations are successful
            _mockCommunication.ExecuteAsync<bool>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(true);

            // Act
            var result = await _executor.StopAllThreadsAsync();

            // Assert
            Assert.AreEqual(3, result);
            await _mockCommunication.Received(3).ExecuteAsync<bool>(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ClearCacheAsync_ExecutesSuccessfully() {
            // Act & Assert - Should not throw
            await _executor.ClearCacheAsync();

            await _mockCommunication.Received(1).ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void ApplyPoliciesAndExecuteAsync_WithTimeout_AppliesCancellation() {
            // Arrange
            const string pythonCode = "import time; time.sleep(2)";

            _mockCommunication.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => Task.Delay(TimeSpan.FromSeconds(10), callInfo.Arg<CancellationToken>()).ContinueWith(_ => "result"));

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Act & Assert - Should timeout quickly due to cancellation token
            Assert.Throws<OperationCanceledException>(() => {
                _executor.ApplyPoliciesAndExecuteAsync<string>(pythonCode, cts.Token).GetAwaiter().GetResult();
            });
        }
    }
}
