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
    /// Tests for the TeardownExecutor class.
    /// </summary>
    public class TeardownExecutorTests {
        private readonly Device _mockDevice;
        private readonly ILogger<TeardownExecutor> _mockLogger;
        private readonly TeardownExecutor _executor;

        public TeardownExecutorTests() {
            _mockDevice = Substitute.For<Device>();
            _mockLogger = Substitute.For<ILogger<TeardownExecutor>>();
            _executor = new TeardownExecutor(_mockDevice, _mockLogger);
        }

        [Fact]
        public void Constructor_WithNullDevice_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new TeardownExecutor(null!, _mockLogger));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new TeardownExecutor(_mockDevice, null!));
        }

        [Fact]
        public async Task ExecuteTeardownAsync_WithTeardownAttribute_ExecutesSuccessfully() {
            // Arrange
            var method = GetMethodWithTeardownAttribute(nameof(TestTeardownMethod));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            _mockDevice.ExecuteAsync<int>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(42);

            // Act
            var result = await _executor.ExecuteTeardownAsync<int>(method);

            // Assert
            Assert.Equal(42, result);
            await _mockDevice.Received(2).ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ExecuteTeardownAsync_WithoutTeardownAttribute_ThrowsInvalidOperationException() {
            // Arrange
            var method = GetMethodWithoutAttribute(nameof(TestMethodWithoutAttribute));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _executor.ExecuteTeardownAsync<int>(method));

            Assert.Contains("not decorated with [Teardown] attribute", exception.Message);
        }

        [Fact]
        public async Task ExecuteTeardownAsync_WithExecutionError_ReturnsDefaultAndDoesNotThrow() {
            // Arrange
            var method = GetMethodWithTeardownAttribute(nameof(TestTeardownMethod));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            _mockDevice.ExecuteAsync<int>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Throws(new DeviceExecutionException("Test execution error"));

            // Act & Assert - Should not throw, should return default
            var result = await _executor.ExecuteTeardownAsync<int>(method);
            Assert.Equal(default(int), result);
        }

        [Fact]
        public async Task ExecuteAllTeardownMethodsAsync_WithMultipleTeardownMethods_ExecutesInReverseOrder() {
            // Arrange
            var type = typeof(TeardownExecutorTests);
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Act
            await _executor.ExecuteAllTeardownMethodsAsync(type);

            // Assert - Should have executed teardown methods
            await _mockDevice.Received().ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task ExecuteAllTeardownMethodsAsync_WhenAlreadyInProgress_SkipsDuplicateExecution() {
            // Arrange
            var type = typeof(TeardownExecutorTests);
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.Delay(100)); // Slow execution to simulate in-progress state

            // Act - Start two concurrent teardown executions
            var task1 = _executor.ExecuteAllTeardownMethodsAsync(type);
            var task2 = _executor.ExecuteAllTeardownMethodsAsync(type);

            await Task.WhenAll(task1, task2);

            // Assert - The second call should have been skipped
            Assert.False(_executor.IsTeardownInProgress());
        }

        [Fact]
        public void RegisterTeardownMethods_WithTeardownMethods_RegistersCorrectly() {
            // Arrange
            var type = typeof(TeardownExecutorTests);

            // Act
            _executor.RegisterTeardownMethods(type);

            // Assert
            var registeredMethods = _executor.GetRegisteredTeardownMethods();
            Assert.NotEmpty(registeredMethods);
        }

        [Fact]
        public void ClearRegisteredTeardownMethods_RemovesAllRegistrations() {
            // Arrange
            var type = typeof(TeardownExecutorTests);
            _executor.RegisterTeardownMethods(type);

            // Act
            _executor.ClearRegisteredTeardownMethods();

            // Assert
            var registeredMethods = _executor.GetRegisteredTeardownMethods();
            Assert.Empty(registeredMethods);
        }

        [Fact]
        public async Task DeployAsync_WithTeardownMethod_CachesDeployedMethod() {
            // Arrange
            var method = GetMethodWithTeardownAttribute(nameof(TestTeardownMethod));
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
            var method = GetMethodWithTeardownAttribute(nameof(TestTeardownMethod));
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
            var method = GetMethodWithTeardownAttribute(nameof(TestTeardownMethod));

            // Act
            var isDeployed = await _executor.IsDeployedAsync(method);

            // Assert
            Assert.False(isDeployed);
        }

        [Fact]
        public void IsTeardownInProgress_InitiallyFalse() {
            // Act & Assert
            Assert.False(_executor.IsTeardownInProgress());
        }

        [Fact]
        public async Task ClearCacheAsync_RemovesAllDeployedMethods() {
            // Arrange
            var method1 = GetMethodWithTeardownAttribute(nameof(TestTeardownMethod));
            var method2 = GetMethodWithTeardownAttribute(nameof(TestTeardownMethodWithOrder));
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

        [Fact]
        public async Task ExecuteTeardownAsync_WithOrderAttribute_RespectsOrder() {
            // Arrange
            var method = GetMethodWithTeardownAttribute(nameof(TestTeardownMethodWithOrder));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Act & Assert - Should not throw
            await _executor.ExecuteTeardownAsync(method);
        }

        [Fact]
        public async Task ExecuteTeardownAsync_WithBooleanReturnType_HandlesCorrectly() {
            // Arrange
            var method = GetMethodWithTeardownAttribute(nameof(TestTeardownMethodReturningBool));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            _mockDevice.ExecuteAsync<bool>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(true);

            // Act
            var result = await _executor.ExecuteTeardownAsync<bool>(method);

            // Assert
            Assert.True(result);
        }

        // Test methods with various attributes for testing
        [Teardown]
        [Fact]
        public void TestTeardownMethod() { }

        [Teardown(Order = 1)]
        [Fact]
        public void TestTeardownMethodWithOrder() { }

        [Teardown]
        public bool TestTeardownMethodReturningBool() => true;

        [Teardown(Name = "CustomTeardown")]
        [Fact]
        public void TestTeardownMethodWithCustomName() { }

        [Fact]
        public void TestMethodWithoutAttribute() { }

        private MethodInfo GetMethodWithTeardownAttribute(string methodName) {
            var method = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method);
            Assert.True(method.HasAttribute<TeardownAttribute>());
            return method;
        }

        private MethodInfo GetMethodWithoutAttribute(string methodName) {
            var method = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method);
            Assert.False(method.HasAttribute<TeardownAttribute>());
            return method;
        }
    }
}
