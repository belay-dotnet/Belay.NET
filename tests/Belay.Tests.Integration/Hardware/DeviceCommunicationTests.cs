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

namespace Belay.Tests.Integration.Hardware
{
    /// <summary>
    /// Integration tests for device communication using real hardware.
    /// </summary>
    [Collection("Hardware")]
    [Trait("Category", "Hardware")]
    [Trait("Category", "ESP32")]
    public class DeviceCommunicationTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        private readonly ILoggerFactory _loggerFactory;
        private SerialDeviceCommunication? _device;
        private readonly string _devicePath;

        public DeviceCommunicationTests(ITestOutputHelper output)
        {
            _output = output;
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddXUnit(output).SetMinimumLevel(LogLevel.Debug);
            });

            // Device path should be configurable via environment variable
            _devicePath = Environment.GetEnvironmentVariable("ESP32_DEVICE_PATH") 
                ?? "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        }

        public async Task InitializeAsync()
        {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var logger = _loggerFactory.CreateLogger<SerialDeviceCommunication>();
            _device = new SerialDeviceCommunication(_devicePath, 115200, logger: logger);
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
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            Assert.NotNull(_device);
            Assert.Equal(DeviceState.Connected, _device.State);
            _output.WriteLine($"Successfully connected to {_devicePath}");
        }

        [SkippableFact]
        public async Task ExecuteBasicExpression_ShouldReturnCorrectResult()
        {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var result = await _device!.ExecuteAsync("2 + 3");
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"Result: {trimmedResult}");
            Assert.Equal("5", trimmedResult);
        }

        [SkippableFact]
        public async Task ExecuteMicroPythonVersion_ShouldReturnVersionString()
        {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var version = await _device!.ExecuteAsync("import sys; sys.version");
            var trimmedVersion = version.Trim();
            
            _output.WriteLine($"MicroPython version: {trimmedVersion}");
            Assert.Contains("MicroPython", trimmedVersion);
        }

        [SkippableFact]
        public async Task ExecuteLargeCode_ShouldHandleFlowControlCorrectly()
        {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var largeCode = @"
data = []
for i in range(50):
    data.append(i * i)
sum(data)
";
            var result = await _device!.ExecuteAsync(largeCode);
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"Large code result: {trimmedResult}");
            
            // Sum of squares from 0 to 49: sum(i^2 for i in range(50)) = 40425
            Assert.Equal("40425", trimmedResult);
        }

        [SkippableFact]
        public async Task ExecuteWithError_ShouldThrowDeviceExecutionException()
        {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var exception = await Assert.ThrowsAsync<DeviceExecutionException>(
                async () => await _device!.ExecuteAsync("1 / 0")
            );

            _output.WriteLine($"Exception message: {exception.Message}");
            Assert.Contains("ZeroDivisionError", exception.Message);
        }

        [SkippableFact]
        public async Task ExecuteMultipleCommands_ShouldMaintainState()
        {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            // Set a variable
            await _device!.ExecuteAsync("test_value = 42");
            
            // Use the variable in another command
            var result = await _device.ExecuteAsync("test_value * 2");
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"State persistence result: {trimmedResult}");
            Assert.Equal("84", trimmedResult);
        }

        [SkippableFact]
        public async Task ExecuteStringOperation_ShouldHandleStringsCorrectly()
        {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var result = await _device!.ExecuteAsync("'Hello' + ' ' + 'ESP32'");
            var trimmedResult = result.Trim().Trim('\'', '"');
            
            _output.WriteLine($"String operation result: {trimmedResult}");
            Assert.Equal("Hello ESP32", trimmedResult);
        }

        [SkippableFact]
        public async Task ExecuteListComprehension_ShouldWorkCorrectly()
        {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var result = await _device!.ExecuteAsync("[x**2 for x in range(5)]");
            var trimmedResult = result.Trim();
            
            _output.WriteLine($"List comprehension result: {trimmedResult}");
            Assert.Equal("[0, 1, 4, 9, 16]", trimmedResult);
        }

        [SkippableFact]
        public async Task RecoveryAfterError_ShouldContinueNormally()
        {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

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
    }
}