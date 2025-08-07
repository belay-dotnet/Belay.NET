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
    /// Tests for the MethodInterceptor class.
    /// </summary>
    public class MethodInterceptorTests {
        private readonly Device _mockDevice;
        private readonly TaskExecutor _mockTaskExecutor;
        private readonly SetupExecutor _mockSetupExecutor;
        private readonly ThreadExecutor _mockThreadExecutor;
        private readonly TeardownExecutor _mockTeardownExecutor;
        private readonly ILogger<MethodInterceptor> _mockLogger;
        private readonly MethodInterceptor _interceptor;

        public MethodInterceptorTests() {
            _mockDevice = Substitute.For<Device>();
            _mockTaskExecutor = Substitute.For<TaskExecutor>(_mockDevice, Substitute.For<ILogger<TaskExecutor>>());
            _mockSetupExecutor = Substitute.For<SetupExecutor>(_mockDevice, Substitute.For<ILogger<SetupExecutor>>());
            _mockThreadExecutor = Substitute.For<ThreadExecutor>(_mockDevice, Substitute.For<ILogger<ThreadExecutor>>());
            _mockTeardownExecutor = Substitute.For<TeardownExecutor>(_mockDevice, Substitute.For<ILogger<TeardownExecutor>>());
            _mockLogger = Substitute.For<ILogger<MethodInterceptor>>();

            _interceptor = new MethodInterceptor(
                _mockDevice,
                _mockTaskExecutor,
                _mockSetupExecutor,
                _mockThreadExecutor,
                _mockTeardownExecutor,
                _mockLogger);
        }

        [Fact]
        public void Constructor_WithNullDevice_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new MethodInterceptor(
                null!, _mockTaskExecutor, _mockSetupExecutor, _mockThreadExecutor, _mockTeardownExecutor, _mockLogger));
        }

        [Fact]
        public void Constructor_WithNullTaskExecutor_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new MethodInterceptor(
                _mockDevice, null!, _mockSetupExecutor, _mockThreadExecutor, _mockTeardownExecutor, _mockLogger));
        }

        [Fact]
        public async Task InterceptAsync_WithTaskAttribute_CallsTaskExecutor() {
            // Arrange
            var method = GetTaskMethod();
            var parameters = new object[] { 1, 2 };
            _mockTaskExecutor.ExecuteTaskAsync<int>(method, parameters, Arg.Any<CancellationToken>())
                .Returns(42);

            // Act
            var result = await _interceptor.InterceptAsync<int>(method, parameters);

            // Assert
            Assert.Equal(42, result);
            await _mockTaskExecutor.Received(1).ExecuteTaskAsync<int>(method, parameters, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task InterceptAsync_WithSetupAttribute_CallsSetupExecutor() {
            // Arrange
            var method = GetSetupMethod();
            var parameters = new object[0];
            _mockSetupExecutor.ExecuteSetupAsync<object>(method, parameters, Arg.Any<CancellationToken>())
                .Returns(new object());

            // Act
            await _interceptor.InterceptAsync(method, parameters);

            // Assert
            await _mockSetupExecutor.Received(1).ExecuteSetupAsync<object>(method, parameters, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task InterceptAsync_WithThreadAttribute_CallsThreadExecutor() {
            // Arrange
            var method = GetThreadMethod();
            var parameters = new object[0];
            var runningThread = new RunningThread {
                Method = method,
                ThreadId = "test-thread",
                DeviceMethodName = "test_method",
                StartedAt = DateTime.UtcNow
            };
            _mockThreadExecutor.StartThreadAsync(method, parameters, Arg.Any<CancellationToken>())
                .Returns(runningThread);

            // Act
            var result = await _interceptor.InterceptAsync<RunningThread>(method, parameters);

            // Assert
            Assert.Same(runningThread, result);
            await _mockThreadExecutor.Received(1).StartThreadAsync(method, parameters, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task InterceptAsync_WithTeardownAttribute_CallsTeardownExecutor() {
            // Arrange
            var method = GetTeardownMethod();
            var parameters = new object[0];
            _mockTeardownExecutor.ExecuteTeardownAsync<object>(method, parameters, Arg.Any<CancellationToken>())
                .Returns(new object());

            // Act
            await _interceptor.InterceptAsync(method, parameters);

            // Assert
            await _mockTeardownExecutor.Received(1).ExecuteTeardownAsync<object>(method, parameters, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task InterceptAsync_WithoutBelayAttribute_ThrowsNotSupportedException() {
            // Arrange
            var method = GetMethodWithoutAttribute();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NotSupportedException>(
                () => _interceptor.InterceptAsync<int>(method));

            Assert.Contains("does not have a recognized Belay attribute", exception.Message);
        }

        [Fact]
        public void PrepareTypeForInterception_WithBelayAttributes_RegistersMethods() {
            // Act
            var count = _interceptor.PrepareTypeForInterception(typeof(MethodInterceptorTests));

            // Assert
            Assert.Equal(4, count); // Should find 4 methods with Belay attributes

            var interceptedMethods = _interceptor.GetInterceptedMethods();
            Assert.Equal(4, interceptedMethods.Count);
        }

        [Fact]
        public void RegisterInterceptedMethod_WithMethod_AddsToCollection() {
            // Arrange
            var method = GetTaskMethod();

            // Act
            _interceptor.RegisterInterceptedMethod(method);

            // Assert
            Assert.True(_interceptor.IsMethodIntercepted(method));
            var interceptedMethods = _interceptor.GetInterceptedMethods();
            Assert.Single(interceptedMethods);
        }

        [Fact]
        public async Task ExecuteSetupMethodsAsync_CallsSetupExecutor() {
            // Arrange
            var type = typeof(MethodInterceptorTests);

            // Act
            await _interceptor.ExecuteSetupMethodsAsync(type);

            // Assert
            await _mockSetupExecutor.Received(1).ExecuteAllSetupMethodsAsync(type, null, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ExecuteTeardownMethodsAsync_CallsTeardownExecutor() {
            // Arrange
            var type = typeof(MethodInterceptorTests);

            // Act
            await _interceptor.ExecuteTeardownMethodsAsync(type);

            // Assert
            await _mockTeardownExecutor.Received(1).ExecuteAllTeardownMethodsAsync(type, null, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task StopAllBackgroundThreadsAsync_CallsThreadExecutor() {
            // Arrange
            _mockThreadExecutor.StopAllThreadsAsync(Arg.Any<CancellationToken>())
                .Returns(2);

            // Act
            var stoppedCount = await _interceptor.StopAllBackgroundThreadsAsync();

            // Assert
            Assert.Equal(2, stoppedCount);
            await _mockThreadExecutor.Received(1).StopAllThreadsAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public void GetRunningThreads_CallsThreadExecutor() {
            // Arrange
            var threads = new RunningThread[0];
            _mockThreadExecutor.GetRunningThreads().Returns(threads);

            // Act
            var result = _interceptor.GetRunningThreads();

            // Assert
            Assert.Same(threads, result);
            _mockThreadExecutor.Received(1).GetRunningThreads();
        }

        [Fact]
        public async Task ResetAsync_CallsAllExecutorResetMethods() {
            // Act
            await _interceptor.ResetAsync();

            // Assert
            await _mockThreadExecutor.Received(1).StopAllThreadsAsync(Arg.Any<CancellationToken>());
            await _mockTaskExecutor.Received(1).ClearCacheAsync(Arg.Any<CancellationToken>());
            await _mockSetupExecutor.Received(1).ClearCacheAsync(Arg.Any<CancellationToken>());
            await _mockThreadExecutor.Received(1).ClearCacheAsync(Arg.Any<CancellationToken>());
            await _mockTeardownExecutor.Received(1).ClearCacheAsync(Arg.Any<CancellationToken>());
            _mockSetupExecutor.Received(1).ResetSetupState();
        }

        // Test methods with various attributes for testing
        [Task]
        public int TestTaskMethod(int a, int b) => a + b;

        [Setup]
        [Fact]
        public void TestSetupMethod() { }

        [Thread]
        [Fact]
        public void TestThreadMethod() { }

        [Teardown]
        [Fact]
        public void TestTeardownMethod() { }

        public int TestMethodWithoutAttribute() => 42;

        private MethodInfo GetTaskMethod() => GetMethod(nameof(TestTaskMethod));
        private MethodInfo GetSetupMethod() => GetMethod(nameof(TestSetupMethod));
        private MethodInfo GetThreadMethod() => GetMethod(nameof(TestThreadMethod));
        private MethodInfo GetTeardownMethod() => GetMethod(nameof(TestTeardownMethod));
        private MethodInfo GetMethodWithoutAttribute() => GetMethod(nameof(TestMethodWithoutAttribute));

        private MethodInfo GetMethod(string methodName) {
            var method = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method);
            return method;
        }
    }
}
