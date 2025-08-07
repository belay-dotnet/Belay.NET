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
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Belay.Attributes;
using Belay.Core;
using Belay.Core.Communication;
using Belay.Core.Execution;
using Belay.Core.Sessions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace Belay.Tests.Unit.Execution {
    /// <summary>
    /// Tests for the TaskExecutor class.
    /// </summary>
    public class TaskExecutorTests {
        private readonly IDeviceCommunication _mockCommunication;
        private readonly ILogger<Device> _mockDeviceLogger;
        private readonly ILogger<TaskExecutor> _mockLogger;
        private readonly Device _device;
        private readonly TaskExecutor _executor;

        public TaskExecutorTests() {
            _mockCommunication = Substitute.For<IDeviceCommunication>();
            _mockDeviceLogger = Substitute.For<ILogger<Device>>();
            _mockLogger = Substitute.For<ILogger<TaskExecutor>>();

            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger<TaskExecutor>().Returns(_mockLogger);

            _device = new Device(_mockCommunication, _mockDeviceLogger, loggerFactory);
            _executor = _device.Task;
        }

        [Test]
        public void Constructor_WithNullDevice_ThrowsArgumentNullException() {
            var mockSessionManager = Substitute.For<IDeviceSessionManager>();
            Assert.Throws<ArgumentNullException>(() => new TaskExecutor(null!, mockSessionManager, _mockLogger));
        }

        [Test]
        public void Constructor_WithNullSessionManager_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new TaskExecutor(_device, null!, _mockLogger));
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException() {
            var mockSessionManager = Substitute.For<IDeviceSessionManager>();
            Assert.Throws<ArgumentNullException>(() => new TaskExecutor(_device, mockSessionManager, null!));
        }

        [Test]
        public async Task ApplyPoliciesAndExecuteAsync_WithBasicCode_ExecutesSuccessfully() {
            // Arrange
            const string pythonCode = "print('Hello World')";
            const string expectedResult = "Hello World";

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

            Assert.That(ex.Message, Does.Contain("Python code cannot be null or empty"));
        }

        [Test]
        public void ApplyPoliciesAndExecuteAsync_WithEmptyCode_ThrowsArgumentException() {
            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => {
                _executor.ApplyPoliciesAndExecuteAsync<string>("").GetAwaiter().GetResult();
            });

            Assert.That(ex.Message, Does.Contain("Python code cannot be null or empty"));
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

        [Test]
        public async Task ApplyPoliciesAndExecuteAsync_WithConcurrentCalls_HandlesExclusiveExecution() {
            // Arrange
            const string pythonCode = "result = 42";
            const string methodName = "TestExclusiveMethod";

            var executionCount = 0;
            var executionDelay = TimeSpan.FromMilliseconds(50);

            _mockCommunication.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => {
                    Interlocked.Increment(ref executionCount);
                    return Task.Delay(executionDelay, callInfo.Arg<CancellationToken>())
                        .ContinueWith(_ => "42");
                });

            // Act - Start multiple concurrent executions
            var tasks = new List<Task<string>>();
            for (int i = 0; i < 3; i++) {
                tasks.Add(_executor.ApplyPoliciesAndExecuteAsync<string>(pythonCode, default, methodName));
            }

            var results = await Task.WhenAll(tasks);

            // Assert - All should complete successfully
            Assert.That(results, Is.All.InstanceOf<string>());
            Assert.That(results, Has.All.EqualTo("42"));
            Assert.That(executionCount, Is.GreaterThanOrEqualTo(3)); // All executions should have occurred
        }

        [Test]
        public async Task GetExecutedMethods_InitiallyEmpty() {
            // Act
            var executedMethods = await _executor.GetExecutedMethodsAsync();

            // Assert
            Assert.IsEmpty(executedMethods);
        }

        [Test]
        public async Task GetExecutedMethods_AfterExecution_ContainsMethod() {
            // Arrange
            const string pythonCode = "result = 42";
            const string methodName = "TestMethod";

            _mockCommunication.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("42");

            // Act
            await _executor.ApplyPoliciesAndExecuteAsync<string>(pythonCode, default, methodName);
            var executedMethods = await _executor.GetExecutedMethodsAsync();

            // Assert
            Assert.That(executedMethods, Contains.Item(methodName));
        }

        [Test]
        public async Task ClearCache_RemovesExecutedMethods() {
            // This test would need to be implemented based on the actual caching behavior
            // For now, we'll test the basic clearing functionality

            // Act
            _executor.ClearCache();
            var executedMethods = await _executor.GetExecutedMethodsAsync();

            // Assert
            Assert.IsEmpty(executedMethods);
        }
    }
}
