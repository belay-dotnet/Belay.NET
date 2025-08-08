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
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Belay.Tests.Integration.Hardware {
    /// <summary>
    /// ESP32 protocol validation tests.
    /// Validates raw REPL protocol compatibility with ESP32 devices.
    /// </summary>
    [Collection("Hardware")]
    [Trait("Category", "Hardware")]
    [Trait("Category", "ESP32")]
    public class Esp32ProtocolValidationTests : IDisposable {
        private readonly ITestOutputHelper _output;
        private SerialPort? _serialPort;
        private readonly string _devicePath;

        public Esp32ProtocolValidationTests(ITestOutputHelper output) {
            _output = output;
            // Device path should be configurable via environment variable
            _devicePath = Environment.GetEnvironmentVariable("ESP32_DEVICE_PATH")
                ?? "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        }

        [SkippableFact]
        public async Task BasicPrintStatement_ShouldReturnExpectedOutput() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            using var port = OpenSerialPort();
            await ResetDeviceAsync(port);

            // Enter raw mode
            await WriteAsync(port, new byte[] { 0x01 });
            await Task.Delay(100);
            await ClearBufferAsync(port);

            // Send print statement
            await WriteAsync(port, Encoding.ASCII.GetBytes("print('Hello ESP32')\x04"));
            await Task.Delay(500);

            var response = await ReadAsync(port, 1000);
            _output.WriteLine($"Response: {BitConverter.ToString(response)}");

            // Exit raw mode
            await WriteAsync(port, new byte[] { 0x02 });

            Assert.Contains(Encoding.ASCII.GetBytes("Hello ESP32"), response);
        }

        [SkippableFact]
        public async Task MathOperation_ShouldComputeCorrectly() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            using var port = OpenSerialPort();
            await ResetDeviceAsync(port);

            // Enter raw mode
            await WriteAsync(port, new byte[] { 0x01 });
            await Task.Delay(100);
            await ClearBufferAsync(port);

            // Send math operation
            await WriteAsync(port, Encoding.ASCII.GetBytes("print(25 + 17)\x04"));
            await Task.Delay(500);

            var response = await ReadAsync(port, 1000);
            _output.WriteLine($"Response: {BitConverter.ToString(response)}");

            // Exit raw mode
            await WriteAsync(port, new byte[] { 0x02 });

            Assert.Contains(Encoding.ASCII.GetBytes("42"), response);
        }

        [SkippableFact]
        public async Task VariableOperations_ShouldWorkCorrectly() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            using var port = OpenSerialPort();
            await ResetDeviceAsync(port);

            // Enter raw mode
            await WriteAsync(port, new byte[] { 0x01 });
            await Task.Delay(100);
            await ClearBufferAsync(port);

            // Send variable operations
            await WriteAsync(port, Encoding.ASCII.GetBytes("x = 100; print(x * 2)\x04"));
            await Task.Delay(500);

            var response = await ReadAsync(port, 1000);
            _output.WriteLine($"Response: {BitConverter.ToString(response)}");

            // Exit raw mode
            await WriteAsync(port, new byte[] { 0x02 });

            Assert.Contains(Encoding.ASCII.GetBytes("200"), response);
        }

        [SkippableFact]
        public async Task ErrorHandling_ShouldReturnExceptionDetails() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            using var port = OpenSerialPort();
            await ResetDeviceAsync(port);

            // Enter raw mode
            await WriteAsync(port, new byte[] { 0x01 });
            await Task.Delay(100);
            await ClearBufferAsync(port);

            // Send code that causes error
            await WriteAsync(port, Encoding.ASCII.GetBytes("1 / 0\x04"));
            await Task.Delay(500);

            var response = await ReadAsync(port, 1000);
            _output.WriteLine($"Response: {BitConverter.ToString(response)}");

            // Exit raw mode
            await WriteAsync(port, new byte[] { 0x02 });

            Assert.Contains(Encoding.ASCII.GetBytes("ZeroDivisionError"), response);
        }

        [SkippableFact]
        public async Task RecoveryAfterError_ShouldContinueNormally() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            using var port = OpenSerialPort();
            await ResetDeviceAsync(port);

            // First cause an error
            await WriteAsync(port, new byte[] { 0x01 });
            await Task.Delay(100);
            await ClearBufferAsync(port);
            await WriteAsync(port, Encoding.ASCII.GetBytes("1 / 0\x04"));
            await Task.Delay(500);
            await ReadAsync(port, 1000); // Read error response
            await WriteAsync(port, new byte[] { 0x02 });
            await Task.Delay(100);

            // Then test recovery
            await WriteAsync(port, new byte[] { 0x01 });
            await Task.Delay(100);
            await ClearBufferAsync(port);

            await WriteAsync(port, Encoding.ASCII.GetBytes("print('Recovered!')\x04"));
            await Task.Delay(500);

            var response = await ReadAsync(port, 1000);
            _output.WriteLine($"Response: {BitConverter.ToString(response)}");

            // Exit raw mode
            await WriteAsync(port, new byte[] { 0x02 });

            Assert.Contains(Encoding.ASCII.GetBytes("Recovered!"), response);
        }

        private SerialPort OpenSerialPort() {
            _serialPort = new SerialPort(_devicePath, 115200) {
                ReadTimeout = 3000,
                WriteTimeout = 3000
            };
            _serialPort.Open();
            Thread.Sleep(1000); // Wait for device to stabilize
            return _serialPort;
        }

        private async Task ResetDeviceAsync(SerialPort port) {
            // Send Ctrl-C + Ctrl-D to reset
            await WriteAsync(port, new byte[] { 0x03, 0x04 });
            await Task.Delay(1000);
            await ClearBufferAsync(port);
        }

        private async Task WriteAsync(SerialPort port, byte[] data) {
            await Task.Run(() => port.Write(data, 0, data.Length));
        }

        private async Task<byte[]> ReadAsync(SerialPort port, int maxBytes) {
            var buffer = new byte[maxBytes];
            var bytesRead = await Task.Run(() => {
                try {
                    return port.Read(buffer, 0, maxBytes);
                }
                catch (TimeoutException) {
                    return port.BytesToRead > 0 ? port.Read(buffer, 0, Math.Min(port.BytesToRead, maxBytes)) : 0;
                }
            });

            var result = new byte[bytesRead];
            Array.Copy(buffer, result, bytesRead);
            return result;
        }

        private async Task ClearBufferAsync(SerialPort port) {
            await Task.Run(() => {
                if (port.BytesToRead > 0) {
                    port.DiscardInBuffer();
                }
            });
        }

        public void Dispose() {
            _serialPort?.Close();
            _serialPort?.Dispose();
        }
    }
}
