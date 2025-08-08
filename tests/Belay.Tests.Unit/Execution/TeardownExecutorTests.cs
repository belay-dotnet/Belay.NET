// Copyright (c) 2024 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for more information.
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
    /// Tests for the TeardownExecutor class.
    /// </summary>
    public class TeardownExecutorTests {
        private readonly IDeviceCommunication _mockCommunication;
        private readonly ILogger<Device> _mockDeviceLogger;
        private readonly ILogger<TeardownExecutor> _mockLogger;
        private readonly Device _device;
        private readonly TeardownExecutor _executor;

        public TeardownExecutorTests() {
            _mockCommunication = Substitute.For<IDeviceCommunication>();
            _mockDeviceLogger = Substitute.For<ILogger<Device>>();
            _mockLogger = Substitute.For<ILogger<TeardownExecutor>>();

            var loggerFactory = Substitute.For<ILoggerFactory>();
            loggerFactory.CreateLogger<TeardownExecutor>().Returns(_mockLogger);

            _device = new Device(_mockCommunication, _mockDeviceLogger, loggerFactory);
            _executor = _device.Teardown;
        }

        [Test]
        public void Constructor_WithNullDevice_ThrowsArgumentNullException() {
            var mockSessionManager = Substitute.For<IDeviceSessionManager>();
            Assert.Throws<ArgumentNullException>(() => new TeardownExecutor(null!, mockSessionManager, _mockLogger));
        }

        [Test]
        public void Constructor_WithNullSessionManager_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new TeardownExecutor(_device, null!, _mockLogger));
        }

        [Test]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException() {
            var mockSessionManager = Substitute.For<IDeviceSessionManager>();
            Assert.Throws<ArgumentNullException>(() => new TeardownExecutor(_device, mockSessionManager, null!));
        }

        [Test]
        public async Task ApplyPoliciesAndExecuteAsync_WithBasicCode_ExecutesSuccessfully() {
            // Arrange
            const string pythonCode = "print('Teardown complete')";
            const string expectedResult = "Teardown done";

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
