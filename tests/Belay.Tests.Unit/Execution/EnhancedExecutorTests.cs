// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit.Execution {
    using System;
    using System.Reflection;
    using System.Threading.Tasks;
    using Belay.Attributes;
    using Belay.Core;
    using Belay.Core.Communication;
    using Belay.Core.Execution;
    using Microsoft.Extensions.Logging;
    using Xunit;
    using Xunit.Abstractions;

    /// <summary>
    /// Unit tests for the enhanced executor functionality.
    /// </summary>
    public class EnhancedExecutorTests : IDisposable {
        private readonly ILogger<EnhancedExecutorTests> logger;
        private readonly Device device;
        private readonly IEnhancedExecutor enhancedExecutor;

        public EnhancedExecutorTests(ITestOutputHelper output) {
            // Setup test logging
            var loggerFactory = new TestLoggerFactory(output);
            this.logger = loggerFactory.CreateLogger<EnhancedExecutorTests>();

            // Create test device with subprocess communication for unit testing
            var communication = new SubprocessDeviceCommunication();
            this.device = new Device(communication, loggerFactory.CreateLogger<Device>(), loggerFactory);
            this.enhancedExecutor = this.device.GetEnhancedExecutor(this.logger);
        }

        [Fact]
        public void CanHandle_WithTaskAttribute_ReturnsTrue() {
            // Arrange
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.TaskMethod))!;

            // Act
            var canHandle = this.enhancedExecutor.CanHandle(method);

            // Assert
            Assert.True(canHandle);
        }

        [Fact]
        public void CanHandle_WithoutAttributes_ReturnsFalse() {
            // Arrange
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.PlainMethod))!;

            // Act
            var canHandle = this.enhancedExecutor.CanHandle(method);

            // Assert
            Assert.False(canHandle);
        }

        [Fact]
        public async Task ExecuteAsync_WithTaskMethod_ExecutesSuccessfully() {
            // Arrange
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.SimpleCalculation))!;
            var testInstance = new TestMethods();

            // Act
            var result = await this.enhancedExecutor.ExecuteAsync<int>(method, testInstance, new object[] { 5, 3 });

            // Assert
            Assert.Equal(8, result);
        }

        [Fact]
        public async Task ExecuteAsync_WithCachedMethod_UsesCaching() {
            // Arrange
            var method = typeof(TestMethods).GetMethod(nameof(TestMethods.CachedMethod))!;
            var testInstance = new TestMethods();

            // Act - Execute twice
            var result1 = await this.enhancedExecutor.ExecuteAsync<string>(method, testInstance);
            var result2 = await this.enhancedExecutor.ExecuteAsync<string>(method, testInstance);

            // Assert
            Assert.Equal(result1, result2);
            
            var stats = this.enhancedExecutor.GetExecutionStatistics();
            Assert.True(stats.InterceptedMethodCount > 0);
        }

        [Fact]
        public void GetExecutionStatistics_ReturnsValidStatistics() {
            // Act
            var stats = this.enhancedExecutor.GetExecutionStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.True(stats.SpecializedExecutorCount >= 0);
            Assert.True(stats.InterceptedMethodCount >= 0);
        }

        [Fact]
        public void ClearExecutionCache_ClearsAllCaches() {
            // Arrange
            var stats1 = this.enhancedExecutor.GetExecutionStatistics();

            // Act
            this.enhancedExecutor.ClearExecutionCache();

            // Assert - Should not throw
            var stats2 = this.enhancedExecutor.GetExecutionStatistics();
            Assert.NotNull(stats2);
        }

        [Fact]
        public void DeviceProxy_CanProxyInterface() {
            // Act
            var canProxy = this.device.CanProxy(typeof(ITestInterface));

            // Assert
            Assert.True(canProxy);
        }

        [Fact]
        public void DeviceProxy_CannotProxyConcreteClass() {
            // Act
            var canProxy = this.device.CanProxy(typeof(TestMethods));

            // Assert
            Assert.False(canProxy);
        }

        [Fact]
        public async Task DeviceProxy_ExecutesMethodsCorrectly() {
            // Arrange
            var proxy = this.device.CreateProxy<ITestInterface>(this.logger);

            // Act & Assert - Should not throw
            await proxy.TestTaskMethod();
            var result = await proxy.TestCalculation(10, 5);
            Assert.Equal(15, result);
        }

        [Fact]
        public void EnhancedDevice_WrapsDeviceCorrectly() {
            // Act
            using var enhancedDevice = new EnhancedDevice(this.device, this.logger);

            // Assert
            Assert.NotNull(enhancedDevice.EnhancedExecutor);
            Assert.Same(this.device, enhancedDevice.UnderlyingDevice);
            
            var stats = enhancedDevice.GetExecutionStatistics();
            Assert.NotNull(stats);
        }

        public void Dispose() {
            if (this.enhancedExecutor is IDisposable disposableExecutor) {
                disposableExecutor.Dispose();
            }
            this.device?.Dispose();
        }

        /// <summary>
        /// Test methods with various attributes for testing.
        /// </summary>
        public class TestMethods {
            [Task]
            public static string TaskMethod() => "print('Task method executed')";

            [Task(Cache = true)]
            public static string CachedMethod() => "print('Cached method executed')";

            [Task]
            public static string SimpleCalculation(int a, int b) => $"print({a} + {b})";  // TimeoutMs cannot be set as attribute parameter

            public static string PlainMethod() => "print('Plain method')";
        }

        /// <summary>
        /// Test interface for proxy testing.
        /// </summary>
        public interface ITestInterface {
            [Task]
            Task TestTaskMethod();

            [Task]
            Task<int> TestCalculation(int a, int b);  // TimeoutMs cannot be set as attribute parameter

            [Setup]
            Task Initialize();
        }
    }

    /// <summary>
    /// Test logger factory for unit tests.
    /// </summary>
    public class TestLoggerFactory : ILoggerFactory {
        private readonly ITestOutputHelper output;

        public TestLoggerFactory(ITestOutputHelper output) {
            this.output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public ILogger CreateLogger(string categoryName) {
            return new TestLogger(categoryName, this.output);
        }

        public ILogger<T> CreateLogger<T>() {
            return new TestLogger<T>(this.output);
        }

        public void AddProvider(ILoggerProvider provider) {
            // Not needed for tests
        }

        public void Dispose() {
            // Nothing to dispose
        }
    }

    /// <summary>
    /// Test logger that outputs to xUnit test output.
    /// </summary>
    public class TestLogger : ILogger {
        private readonly string categoryName;
        private readonly ITestOutputHelper output;

        public TestLogger(string categoryName, ITestOutputHelper output) {
            this.categoryName = categoryName;
            this.output = output;
        }

        IDisposable? ILogger.BeginScope<TState>(TState state) => new TestScope();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            try {
                var message = formatter(state, exception);
                this.output.WriteLine($"[{logLevel}] {this.categoryName}: {message}");
                
                if (exception != null) {
                    this.output.WriteLine($"Exception: {exception}");
                }
            }
            catch {
                // Ignore logging errors in tests
            }
        }

        private class TestScope : IDisposable {
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Generic test logger.
    /// </summary>
    /// <typeparam name="T">The category type.</typeparam>
    public class TestLogger<T> : TestLogger, ILogger<T> {
        public TestLogger(ITestOutputHelper output) : base(typeof(T).Name, output) {
        }
    }
}