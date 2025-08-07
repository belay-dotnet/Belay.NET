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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Belay.Attributes;
using Belay.Core;
using Belay.Core.Execution;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Belay.Tests.Unit.Execution {
    /// <summary>
    /// Tests for the ThreadExecutor class.
    /// </summary>
    public class ThreadExecutorTests {
        private readonly Device _mockDevice;
        private readonly ILogger<ThreadExecutor> _mockLogger;
        private readonly ThreadExecutor _executor;

        public ThreadExecutorTests() {
            _mockDevice = Substitute.For<Device>();
            _mockLogger = Substitute.For<ILogger<ThreadExecutor>>();
            _executor = new ThreadExecutor(_mockDevice, _mockLogger);
        }

        [Fact]
        public void Constructor_WithNullDevice_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new ThreadExecutor(null!, _mockLogger));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new ThreadExecutor(_mockDevice, null!));
        }

        [Fact]
        public async Task StartThreadAsync_WithThreadAttribute_StartsSuccessfully() {
            // Arrange
            var method = GetMethodWithThreadAttribute(nameof(TestThreadMethod));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Act
            var runningThread = await _executor.StartThreadAsync(method);

            // Assert
            Assert.NotNull(runningThread);
            Assert.Equal(method, runningThread.Method);
            Assert.NotNull(runningThread.ThreadId);
            Assert.NotNull(runningThread.DeviceMethodName);
            Assert.True(runningThread.StartedAt <= DateTime.UtcNow);
            await _mockDevice.Received(2).ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task StartThreadAsync_WithoutThreadAttribute_ThrowsInvalidOperationException() {
            // Arrange
            var method = GetMethodWithoutAttribute(nameof(TestMethodWithoutAttribute));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _executor.StartThreadAsync(method));

            Assert.Contains("not decorated with [Thread] attribute", exception.Message);
        }

        [Fact]
        public async Task StartThreadAsync_WithCustomName_UsesCustomName() {
            // Arrange
            var method = GetMethodWithThreadAttribute(nameof(TestThreadMethodWithCustomName));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Act
            var runningThread = await _executor.StartThreadAsync(method);

            // Assert
            Assert.NotNull(runningThread);
            Assert.Contains("CustomThread", runningThread.DeviceMethodName);
        }

        [Fact]
        public async Task StartThreadAsync_WithDaemon_SetsDaemonProperty() {
            // Arrange
            var method = GetMethodWithThreadAttribute(nameof(TestThreadMethodDaemon));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Act
            var runningThread = await _executor.StartThreadAsync(method);

            // Assert
            Assert.NotNull(runningThread);
            Assert.True(runningThread.IsDaemon);
        }

        [Fact]
        public async Task DeployAsync_WithThreadMethod_CachesDeployedMethod() {
            // Arrange
            var method = GetMethodWithThreadAttribute(nameof(TestThreadMethod));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Act
            var deployed1 = await _executor.DeployAsync(method);
            var deployed2 = await _executor.DeployAsync(method);

            // Assert
            Assert.Same(deployed1, deployed2); // Should return same cached instance
            Assert.Equal(method.GetDeviceMethodName(), deployed1.DeviceMethodName);
            Assert.Equal(method.GetSignatureHash(), deployed1.SignatureHash);
        }

        [Fact]
        public async Task IsDeployedAsync_WithDeployedMethod_ReturnsTrue() {
            // Arrange
            var method = GetMethodWithThreadAttribute(nameof(TestThreadMethod));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            await _executor.DeployAsync(method);

            // Act
            var isDeployed = await _executor.IsDeployedAsync(method);

            // Assert
            Assert.True(isDeployed);
        }

        [Fact]
        public async Task IsDeployedAsync_WithNotDeployedMethod_ReturnsFalse() {
            // Arrange
            var method = GetMethodWithThreadAttribute(nameof(TestThreadMethod));

            // Act
            var isDeployed = await _executor.IsDeployedAsync(method);

            // Assert
            Assert.False(isDeployed);
        }

        [Fact]
        public void GetRunningThreads_WithNoThreads_ReturnsEmpty() {
            // Act
            var runningThreads = _executor.GetRunningThreads();

            // Assert
            Assert.Empty(runningThreads);
        }

        [Fact]
        public async Task GetRunningThreads_WithRunningThread_ReturnsThread() {
            // Arrange
            var method = GetMethodWithThreadAttribute(nameof(TestThreadMethod));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            await _executor.StartThreadAsync(method);

            // Act
            var runningThreads = _executor.GetRunningThreads();

            // Assert
            Assert.Single(runningThreads);
            var thread = runningThreads.First();
            Assert.Equal(method, thread.Method);
        }

        [Fact]
        public async Task StopThreadAsync_WithRunningThread_StopsThread() {
            // Arrange
            var method = GetMethodWithThreadAttribute(nameof(TestThreadMethod));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            var runningThread = await _executor.StartThreadAsync(method);

            // Act
            var result = await _executor.StopThreadAsync(runningThread.ThreadId);

            // Assert
            Assert.True(result);
            await _mockDevice.Received().ExecuteAsync(Arg.Is<string>(code => code.Contains("_thread.exit")),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task StopThreadAsync_WithNonExistentThread_ReturnsFalse() {
            // Act
            var result = await _executor.StopThreadAsync("non-existent-thread-id");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task StopAllThreadsAsync_WithMultipleThreads_StopsAll() {
            // Arrange
            var method1 = GetMethodWithThreadAttribute(nameof(TestThreadMethod));
            var method2 = GetMethodWithThreadAttribute(nameof(TestThreadMethodDaemon));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await _executor.StartThreadAsync(method1);
            await _executor.StartThreadAsync(method2);

            // Act
            var stoppedCount = await _executor.StopAllThreadsAsync();

            // Assert
            Assert.Equal(2, stoppedCount);
            var runningThreads = _executor.GetRunningThreads();
            Assert.Empty(runningThreads);
        }

        [Fact]
        public async Task GetThreadStatusAsync_WithRunningThread_ReturnsStatus() {
            // Arrange
            var method = GetMethodWithThreadAttribute(nameof(TestThreadMethod));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            _mockDevice.ExecuteAsync<bool>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(true);
            var runningThread = await _executor.StartThreadAsync(method);

            // Act
            var isRunning = await _executor.GetThreadStatusAsync(runningThread.ThreadId);

            // Assert
            Assert.True(isRunning);
        }

        [Fact]
        public async Task ClearCacheAsync_RemovesAllDeployedMethods() {
            // Arrange
            var method1 = GetMethodWithThreadAttribute(nameof(TestThreadMethod));
            var method2 = GetMethodWithThreadAttribute(nameof(TestThreadMethodDaemon));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await _executor.DeployAsync(method1);
            await _executor.DeployAsync(method2);

            // Act
            await _executor.ClearCacheAsync();

            // Assert
            var deployedMethods = await _executor.GetDeployedMethodsAsync();
            Assert.Empty(deployedMethods);
        }

        // Test methods with various attributes for testing
        [Thread]
        [Fact]
        public void TestThreadMethod() { }

        [Thread(Name = "CustomThread")]
        [Fact]
        public void TestThreadMethodWithCustomName() { }

        [Thread(Daemon = true)]
        [Fact]
        public void TestThreadMethodDaemon() { }

        [Thread(MaxInstances = 5)]
        [Fact]
        public void TestThreadMethodWithMaxInstances() { }

        [Fact]
        public void TestMethodWithoutAttribute() { }

        private MethodInfo GetMethodWithThreadAttribute(string methodName) {
            var method = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method);
            Assert.True(method.HasAttribute<ThreadAttribute>());
            return method;
        }

        private MethodInfo GetMethodWithoutAttribute(string methodName) {
            var method = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method);
            Assert.False(method.HasAttribute<ThreadAttribute>());
            return method;
        }
    }
}
