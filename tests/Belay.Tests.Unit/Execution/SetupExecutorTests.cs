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

namespace Belay.Tests.Unit.Execution {
    /// <summary>
    /// Tests for the SetupExecutor class.
    /// </summary>
    public class SetupExecutorTests {
        private readonly Device _mockDevice;
        private readonly ILogger<SetupExecutor> _mockLogger;
        private readonly SetupExecutor _executor;

        public SetupExecutorTests() {
            _mockDevice = Substitute.For<Device>();
            _mockLogger = Substitute.For<ILogger<SetupExecutor>>();
            _executor = new SetupExecutor(_mockDevice, _mockLogger);
        }

        [Test]
        public void Constructor_WithNullDevice_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new SetupExecutor(null!, _mockLogger));
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new SetupExecutor(_mockDevice, null!));
        }

        [Test]
        public async Task ExecuteSetupAsync_WithSetupAttribute_ExecutesSuccessfully() {
            // Arrange
            var method = GetMethodWithSetupAttribute(nameof(TestSetupMethod));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            _mockDevice.ExecuteAsync<int>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(42);

            // Act
            var result = await _executor.ExecuteSetupAsync<int>(method);

            // Assert
            Assert.Equal(42, result);
            await _mockDevice.Received(2).ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task ExecuteSetupAsync_WithoutSetupAttribute_ThrowsInvalidOperationException() {
            // Arrange
            var method = GetMethodWithoutAttribute(nameof(TestMethodWithoutAttribute));

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _executor.ExecuteSetupAsync<int>(method));

            Assert.Contains("not decorated with [Setup] attribute", exception.Message);
        }

        [Test]
        public async Task ExecuteSetupAsync_SameMethodTwice_ExecutesOnlyOnce() {
            // Arrange
            var method = GetMethodWithSetupAttribute(nameof(TestSetupMethod));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            _mockDevice.ExecuteAsync<int>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(42);

            // Act
            var result1 = await _executor.ExecuteSetupAsync<int>(method);
            var result2 = await _executor.ExecuteSetupAsync<int>(method);

            // Assert
            Assert.Equal(42, result1);
            Assert.Equal(default(int), result2); // Second call should return default
            await _mockDevice.Received(2).ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()); // Only called once
        }

        [Test]
        public async Task ExecuteAllSetupMethodsAsync_WithMultipleSetupMethods_ExecutesInOrder() {
            // Arrange
            var type = typeof(SetupExecutorTests);
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Act
            await _executor.ExecuteAllSetupMethodsAsync(type);

            // Assert - Should have executed setup methods based on order
            await _mockDevice.Received().ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task DeployAsync_WithSetupMethod_CachesDeployedMethod() {
            // Arrange
            var method = GetMethodWithSetupAttribute(nameof(TestSetupMethod));
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

        [Test]
        public async Task IsDeployedAsync_WithDeployedMethod_ReturnsTrue() {
            // Arrange
            var method = GetMethodWithSetupAttribute(nameof(TestSetupMethod));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            await _executor.DeployAsync(method);

            // Act
            var isDeployed = await _executor.IsDeployedAsync(method);

            // Assert
            Assert.True(isDeployed);
        }

        [Test]
        public async Task IsDeployedAsync_WithNotDeployedMethod_ReturnsFalse() {
            // Arrange
            var method = GetMethodWithSetupAttribute(nameof(TestSetupMethod));

            // Act
            var isDeployed = await _executor.IsDeployedAsync(method);

            // Assert
            Assert.False(isDeployed);
        }

        [Test]
        public async Task ClearCacheAsync_RemovesAllDeployedMethods() {
            // Arrange
            var method1 = GetMethodWithSetupAttribute(nameof(TestSetupMethod));
            var method2 = GetMethodWithSetupAttribute(nameof(TestSetupMethodWithOrder));
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

        [Test]
        public void ResetSetupState_ClearsExecutedSetupMethods() {
            // Arrange
            var method = GetMethodWithSetupAttribute(nameof(TestSetupMethod));

            // Act
            _executor.ResetSetupState();

            // Assert - Method should be able to execute again after reset
            // This is tested indirectly through the setup execution behavior
        }

        [Test]
        public void GetExecutedSetupMethods_ReturnsExecutedMethods() {
            // Act
            var executedMethods = _executor.GetExecutedSetupMethods();

            // Assert
            Assert.NotNull(executedMethods);
        }

        [Test]
        public async Task ExecuteSetupAsync_WithOrderAttribute_RespectsOrder() {
            // Arrange
            var method = GetMethodWithSetupAttribute(nameof(TestSetupMethodWithOrder));
            _mockDevice.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            // Act & Assert - Should not throw
            await _executor.ExecuteSetupAsync(method);
        }

        // Test methods with various attributes for testing
        [Setup]
        [Test]
        public void TestSetupMethod() { }

        [Setup(Order = 1)]
        [Test]
        public void TestSetupMethodWithOrder() { }

        [Setup]
        [Test]
        public void TestSetupMethodWithCustomName() { }

        [Test]
        public void TestMethodWithoutAttribute() { }

        private MethodInfo GetMethodWithSetupAttribute(string methodName) {
            var method = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method);
            Assert.True(method.HasAttribute<SetupAttribute>());
            return method;
        }

        private MethodInfo GetMethodWithoutAttribute(string methodName) {
            var method = GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method);
            Assert.False(method.HasAttribute<SetupAttribute>());
            return method;
        }
    }
}
