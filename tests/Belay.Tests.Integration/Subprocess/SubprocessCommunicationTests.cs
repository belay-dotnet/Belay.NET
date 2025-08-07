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
using System.IO;
using System.Threading.Tasks;
using Belay.Core.Communication;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Belay.Tests.Integration.Subprocess
{
    /// <summary>
    /// Integration tests for subprocess communication using MicroPython Unix port.
    /// </summary>
    [Collection("Subprocess")]
    [Trait("Category", "Subprocess")]
    [Trait("Category", "UnixPort")]
    public class SubprocessCommunicationTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        private readonly ILoggerFactory _loggerFactory;
        private SubprocessDeviceCommunication? _device;
        private readonly string _micropythonPath;

        public SubprocessCommunicationTests(ITestOutputHelper output)
        {
            _output = output;
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddXUnit(output).SetMinimumLevel(LogLevel.Debug);
            });

            // MicroPython path should be configurable via environment variable
            _micropythonPath = Environment.GetEnvironmentVariable("MICROPYTHON_PATH") 
                ?? "./micropython/ports/unix/build-standard/micropython";
        }

        public async Task InitializeAsync()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            var logger = _loggerFactory.CreateLogger<SubprocessDeviceCommunication>();
            _device = new SubprocessDeviceCommunication(_micropythonPath, logger: logger);
            await _device.ConnectAsync();
        }

        public async Task DisposeAsync()
        {
            if (_device != null)
            {
                await _device.DisconnectAsync();
                _device.Dispose();
            }
            _loggerFactory.Dispose();
        }

        [SkippableFact]
        public async Task Connection_ShouldEstablishSuccessfully()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            Assert.NotNull(_device);
            Assert.Equal(DeviceState.Connected, _device.State);
            _output.WriteLine($"Successfully connected to subprocess: {_micropythonPath}");
        }

        [SkippableFact]
        public async Task EnterRawMode_ShouldReturnExpectedPrompt()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            // Raw mode is entered during connection
            // Test that we can execute code in raw mode
            var result = await _device!.ExecuteAsync("1+1");
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"Raw mode execution result: {trimmedResult}");
            Assert.Equal("2", trimmedResult);
        }

        [SkippableFact]
        public async Task ExecuteBasicExpression_ShouldReturnCorrectResult()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            var result = await _device!.ExecuteAsync("2 + 3");
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"Result: {trimmedResult}");
            Assert.Equal("5", trimmedResult);
        }

        [SkippableFact]
        public async Task ExecuteComplexExpression_ShouldHandleCorrectly()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            var result = await _device!.ExecuteAsync("sum([i**2 for i in range(10)])");
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"Complex expression result: {trimmedResult}");
            Assert.Equal("285", trimmedResult); // Sum of squares from 0-9
        }

        [SkippableFact]
        public async Task ExecuteMicroPythonVersion_ShouldReturnVersionString()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            var version = await _device!.ExecuteAsync("import sys; sys.version");
            var trimmedVersion = version.Trim();
            
            _output.WriteLine($"MicroPython version: {trimmedVersion}");
            Assert.Contains("MicroPython", trimmedVersion);
        }

        [SkippableFact]
        public async Task ExecuteMultilineCode_ShouldWorkCorrectly()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            var multilineCode = @"
def factorial(n):
    if n <= 1:
        return 1
    return n * factorial(n-1)

factorial(5)
";
            var result = await _device!.ExecuteAsync(multilineCode);
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"Multiline code result: {trimmedResult}");
            Assert.Equal("120", trimmedResult); // 5! = 120
        }

        [SkippableFact]
        public async Task ExecuteWithError_ShouldThrowDeviceExecutionException()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            var exception = await Assert.ThrowsAsync<DeviceExecutionException>(
                async () => await _device!.ExecuteAsync("1 / 0")
            );

            _output.WriteLine($"Exception message: {exception.Message}");
            Assert.Contains("ZeroDivisionError", exception.Message);
        }

        [SkippableFact]
        public async Task ExecuteImportStatement_ShouldWorkCorrectly()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            await _device!.ExecuteAsync("import math");
            var result = await _device.ExecuteAsync("math.pi");
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"Math.pi result: {trimmedResult}");
            Assert.StartsWith("3.14159", trimmedResult);
        }

        [SkippableFact]
        public async Task ExecuteListOperations_ShouldMaintainState()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            await _device!.ExecuteAsync("my_list = [1, 2, 3]");
            await _device.ExecuteAsync("my_list.append(4)");
            var result = await _device.ExecuteAsync("my_list");
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"List state: {trimmedResult}");
            Assert.Equal("[1, 2, 3, 4]", trimmedResult);
        }

        [SkippableFact]
        public async Task ExecuteDictionaryOperations_ShouldWorkCorrectly()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            await _device!.ExecuteAsync("my_dict = {'a': 1, 'b': 2}");
            await _device.ExecuteAsync("my_dict['c'] = 3");
            var result = await _device.ExecuteAsync("len(my_dict)");
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"Dictionary size: {trimmedResult}");
            Assert.Equal("3", trimmedResult);
        }

        [SkippableFact]
        public async Task RecoveryAfterError_ShouldContinueNormally()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            // First cause an error
            await Assert.ThrowsAsync<DeviceExecutionException>(
                async () => await _device!.ExecuteAsync("undefined_variable")
            );

            // Device should still be functional
            var result = await _device!.ExecuteAsync("10 + 20");
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"Recovery test result: {trimmedResult}");
            Assert.Equal("30", trimmedResult);
        }

        [SkippableFact]
        public async Task ExecuteLargeOutput_ShouldHandleCorrectly()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            var code = "print('x' * 1000); 'done'";
            var result = await _device!.ExecuteAsync(code);
            
            _output.WriteLine($"Large output handled, result length: {result.Length}");
            Assert.Contains("done", result);
            Assert.True(result.Length >= 1000);
        }

        [SkippableFact]
        public async Task ExecuteClassDefinition_ShouldWorkCorrectly()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            var classCode = @"
class TestClass:
    def __init__(self, value):
        self.value = value
    
    def double(self):
        return self.value * 2

obj = TestClass(21)
obj.double()
";
            var result = await _device!.ExecuteAsync(classCode);
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"Class method result: {trimmedResult}");
            Assert.Equal("42", trimmedResult);
        }

        [SkippableFact]
        public async Task InterruptDuringExecution_ShouldRecoverGracefully()
        {
            Skip.IfNot(File.Exists(_micropythonPath), $"MicroPython Unix port not found at {_micropythonPath}");

            // Start a long-running operation
            var longRunningTask = _device!.ExecuteAsync(@"
import time
for i in range(100):
    time.sleep(0.01)
print('Should not reach here')
");

            // Wait a bit then disconnect
            await Task.Delay(100);
            await _device.DisconnectAsync();

            // Reinitialize for next test
            var logger = _loggerFactory.CreateLogger<SubprocessDeviceCommunication>();
            _device = new SubprocessDeviceCommunication(_micropythonPath, logger: logger);
            await _device.ConnectAsync();

            // Should be able to execute normally
            var result = await _device.ExecuteAsync("5 * 5");
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"Post-interrupt result: {trimmedResult}");
            Assert.Equal("25", trimmedResult);
        }
    }
}