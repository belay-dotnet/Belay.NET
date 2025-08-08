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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Belay.Core;
using Belay.Sync;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Belay.Tests.Integration.Sync {
    /// <summary>
    /// Integration tests for DeviceFileSystem using real hardware.
    /// </summary>
    [Collection("Hardware")]
    [Trait("Category", "Hardware")]
    [Trait("Category", "FileSystem")]
    public class DeviceFileSystemIntegrationTests : IAsyncLifetime {
        private readonly ITestOutputHelper _output;
        private readonly ILoggerFactory _loggerFactory;
        private Device? _device;
        private DeviceFileSystem? _fileSystem;
        private readonly string _devicePath;
        private readonly string _testDirectory = "/belay_test";

        public DeviceFileSystemIntegrationTests(ITestOutputHelper output) {
            _output = output;
            _loggerFactory = LoggerFactory.Create(builder => {
                builder.AddXUnit(output).SetMinimumLevel(LogLevel.Debug);
            });

            // Device path should be configurable via environment variable
            _devicePath = Environment.GetEnvironmentVariable("ESP32_DEVICE_PATH")
                ?? "/dev/usb/tty-USB_JTAG_serial_debug_unit-40:4C:CA:5B:20:94";
        }

        public async Task InitializeAsync() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var logger = _loggerFactory.CreateLogger<Device>();
            _device = new Device(_devicePath, logger: logger);
            await _device.ConnectAsync();

            var fileSystemLogger = _loggerFactory.CreateLogger<DeviceFileSystem>();
            _fileSystem = new DeviceFileSystem(_device, fileSystemLogger);

            // Clean up any existing test directory
            try {
                await _fileSystem.DeleteDirectoryAsync(_testDirectory, recursive: true);
            }
            catch {
                // Ignore cleanup errors
            }

            // Create test directory
            await _fileSystem.CreateDirectoryAsync(_testDirectory);
        }

        public async Task DisposeAsync() {
            if (_fileSystem != null && _device != null) {
                try {
                    // Clean up test directory
                    await _fileSystem.DeleteDirectoryAsync(_testDirectory, recursive: true);
                }
                catch {
                    // Ignore cleanup errors
                }
            }

            if (_device != null) {
                await _device.DisconnectAsync();
                _device.Dispose();
            }

            _loggerFactory.Dispose();
        }

        [SkippableFact]
        public async Task CreateAndDeleteDirectory_ShouldWorkCorrectly() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var testDir = $"{_testDirectory}/new_directory";

            // Create directory
            await _fileSystem!.CreateDirectoryAsync(testDir);

            // Verify it exists
            var exists = await _fileSystem.ExistsAsync(testDir);
            Assert.True(exists);

            var info = await _fileSystem.GetFileInfoAsync(testDir);
            Assert.NotNull(info);
            Assert.True(info.IsDirectory);

            // Delete directory
            await _fileSystem.DeleteDirectoryAsync(testDir);

            // Verify it no longer exists
            exists = await _fileSystem.ExistsAsync(testDir);
            Assert.False(exists);
        }

        [SkippableFact]
        public async Task WriteAndReadTextFile_ShouldPreserveContent() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var testFile = $"{_testDirectory}/test.txt";
            var testContent = "Hello, ESP32!\nThis is a test file.\n🎉";

            // Write text file
            await _fileSystem!.WriteTextFileAsync(testFile, testContent);

            // Verify file exists
            var exists = await _fileSystem.ExistsAsync(testFile);
            Assert.True(exists);

            // Read text file
            var readContent = await _fileSystem.ReadTextFileAsync(testFile);
            Assert.Equal(testContent, readContent);

            // Get file info
            var info = await _fileSystem.GetFileInfoAsync(testFile);
            Assert.NotNull(info);
            Assert.False(info.IsDirectory);
            Assert.True(info.Size > 0);

            // Delete file
            await _fileSystem.DeleteFileAsync(testFile);

            // Verify file no longer exists
            exists = await _fileSystem.ExistsAsync(testFile);
            Assert.False(exists);
        }

        [SkippableFact]
        public async Task WriteAndReadBinaryFile_ShouldPreserveContent() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var testFile = $"{_testDirectory}/binary.bin";
            var testData = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0x80, 0x7F };

            // Write binary file
            await _fileSystem!.WriteFileAsync(testFile, testData);

            // Read binary file
            var readData = await _fileSystem.ReadFileAsync(testFile);
            Assert.Equal(testData, readData);

            // Delete file
            await _fileSystem.DeleteFileAsync(testFile);
        }

        [SkippableFact]
        public async Task ListDirectory_ShouldReturnCorrectEntries() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            // Create test structure
            var subDir = $"{_testDirectory}/subdir";
            var file1 = $"{_testDirectory}/file1.txt";
            var file2 = $"{_testDirectory}/file2.py";
            var file3 = $"{subDir}/nested.txt";

            await _fileSystem!.CreateDirectoryAsync(subDir);
            await _fileSystem.WriteTextFileAsync(file1, "Content 1");
            await _fileSystem.WriteTextFileAsync(file2, "print('Hello')");
            await _fileSystem.WriteTextFileAsync(file3, "Nested content");

            // List non-recursive
            var entries = await _fileSystem.ListAsync(_testDirectory, recursive: false);

            _output.WriteLine($"Found {entries.Count} entries in {_testDirectory}");
            foreach (var entry in entries) {
                _output.WriteLine($"  {entry.Path} - Directory: {entry.IsDirectory}, Size: {entry.Size}");
            }

            Assert.Equal(3, entries.Count);
            Assert.Contains(entries, e => e.Path.EndsWith("file1.txt") && !e.IsDirectory);
            Assert.Contains(entries, e => e.Path.EndsWith("file2.py") && !e.IsDirectory);
            Assert.Contains(entries, e => e.Path.EndsWith("subdir") && e.IsDirectory);

            // List recursive
            var recursiveEntries = await _fileSystem.ListAsync(_testDirectory, recursive: true);

            _output.WriteLine($"Found {recursiveEntries.Count} entries recursively in {_testDirectory}");
            foreach (var entry in recursiveEntries) {
                _output.WriteLine($"  {entry.Path} - Directory: {entry.IsDirectory}, Size: {entry.Size}");
            }

            Assert.Equal(4, recursiveEntries.Count); // 3 from above + nested.txt
            Assert.Contains(recursiveEntries, e => e.Path.EndsWith("nested.txt") && !e.IsDirectory);
        }

        [SkippableFact]
        public async Task CalculateChecksum_ShouldReturnCorrectHash() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var testFile = $"{_testDirectory}/checksum_test.txt";
            var testContent = "Hello, World!";

            await _fileSystem!.WriteTextFileAsync(testFile, testContent);

            // Calculate MD5 checksum
            var md5Hash = await _fileSystem.CalculateChecksumAsync(testFile, "md5");
            Assert.NotEmpty(md5Hash);
            Assert.Equal(32, md5Hash.Length); // MD5 is 32 hex characters

            // Calculate SHA256 checksum
            var sha256Hash = await _fileSystem.CalculateChecksumAsync(testFile, "sha256");
            Assert.NotEmpty(sha256Hash);
            Assert.Equal(64, sha256Hash.Length); // SHA256 is 64 hex characters

            _output.WriteLine($"MD5: {md5Hash}");
            _output.WriteLine($"SHA256: {sha256Hash}");

            // Verify checksums are different
            Assert.NotEqual(md5Hash, sha256Hash);

            await _fileSystem.DeleteFileAsync(testFile);
        }

        [SkippableFact]
        public async Task LargeFile_ShouldUseChunkedTransfer() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var testFile = $"{_testDirectory}/large_file.txt";
            var largeContent = new StringBuilder();

            // Create content larger than chunk size (8KB)
            for (int i = 0; i < 1000; i++) {
                largeContent.AppendLine($"Line {i}: This is a test line with some content to make the file larger.");
            }

            var content = largeContent.ToString();
            _output.WriteLine($"Creating large file with {content.Length} characters");

            // Write large file (should trigger chunked write)
            await _fileSystem!.WriteTextFileAsync(testFile, content);

            // Read large file (should trigger chunked read)
            var readContent = await _fileSystem.ReadTextFileAsync(testFile);

            Assert.Equal(content, readContent);

            // Get file info to verify size
            var info = await _fileSystem.GetFileInfoAsync(testFile);
            Assert.NotNull(info);
            Assert.True(info.Size > 8192); // Larger than single chunk

            _output.WriteLine($"Large file size: {info.Size} bytes");

            await _fileSystem.DeleteFileAsync(testFile);
        }

        [SkippableFact]
        public async Task DeleteNonexistentFile_ShouldThrowFileNotFoundException() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var nonexistentFile = $"{_testDirectory}/nonexistent.txt";

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _fileSystem!.DeleteFileAsync(nonexistentFile));
        }

        [SkippableFact]
        public async Task DeleteNonexistentDirectory_ShouldThrowDirectoryNotFoundException() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var nonexistentDir = $"{_testDirectory}/nonexistent_dir";

            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                () => _fileSystem!.DeleteDirectoryAsync(nonexistentDir));
        }

        [SkippableFact]
        public async Task CreateDirectoryRecursive_ShouldCreateParentDirectories() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var nestedDir = $"{_testDirectory}/level1/level2/level3";

            // Create nested directory structure
            await _fileSystem!.CreateDirectoryAsync(nestedDir, recursive: true);

            // Verify all levels exist
            Assert.True(await _fileSystem.ExistsAsync($"{_testDirectory}/level1"));
            Assert.True(await _fileSystem.ExistsAsync($"{_testDirectory}/level1/level2"));
            Assert.True(await _fileSystem.ExistsAsync($"{_testDirectory}/level1/level2/level3"));

            // Clean up
            await _fileSystem.DeleteDirectoryAsync($"{_testDirectory}/level1", recursive: true);
        }

        [SkippableFact]
        public async Task ReadDirectory_ShouldThrowUnauthorizedAccessException() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            // Try to read the test directory as if it were a file
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _fileSystem!.ReadFileAsync(_testDirectory));
        }

        [SkippableFact]
        public async Task ListNonexistentDirectory_ShouldThrowDirectoryNotFoundException() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var nonexistentDir = $"{_testDirectory}/nonexistent";

            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                () => _fileSystem!.ListAsync(nonexistentDir));
        }

        [SkippableFact]
        public async Task FileOperations_WithSpecialCharacters_ShouldWork() {
            Skip.IfNot(File.Exists(_devicePath), $"ESP32 device not found at {_devicePath}");

            var testFile = $"{_testDirectory}/special_chars_äöü_测试.txt";
            var testContent = "Content with special characters: äöü 测试 🎉";

            // Write file with special characters
            await _fileSystem!.WriteTextFileAsync(testFile, testContent);

            // Read and verify content
            var readContent = await _fileSystem.ReadTextFileAsync(testFile);
            Assert.Equal(testContent, readContent);

            // Clean up
            await _fileSystem.DeleteFileAsync(testFile);
        }
    }
}
