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

using System.Text;
using Belay.Core;
using Belay.Sync;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Belay.Tests.Unit.Sync {
    /// <summary>
    /// Tests for the DeviceFileSystem class.
    /// </summary>
    public class DeviceFileSystemTests {
        private readonly Device _mockDevice;
        private readonly ILogger<DeviceFileSystem> _mockLogger;
        private readonly DeviceFileSystem _fileSystem;

        public DeviceFileSystemTests() {
            _mockDevice = Substitute.For<Device>();
            _mockLogger = Substitute.For<ILogger<DeviceFileSystem>>();
            _fileSystem = new DeviceFileSystem(_mockDevice, _mockLogger);
        }

        [Fact]
        public void Constructor_WithNullDevice_ThrowsArgumentNullException() {
            Assert.Throws<ArgumentNullException>(() => new DeviceFileSystem(null!));
        }

        [Fact]
        public void Constructor_WithNullLogger_CreatesInstance() {
            var fileSystem = new DeviceFileSystem(_mockDevice, null);
            Assert.NotNull(fileSystem);
        }

        [Fact]
        public async Task ListAsync_WithValidDirectory_ReturnsFileInfoList() {
            // Arrange
            var jsonResponse = """
                [
                    {"path": "/test/file1.txt", "is_directory": false, "size": 100, "modified": 1640995200},
                    {"path": "/test/dir1", "is_directory": true, "size": null, "modified": 1640995200}
                ]
                """;
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(jsonResponse);

            // Act
            var result = await _fileSystem.ListAsync("/test");

            // Assert
            Assert.Equal(2, result.Count);

            var file = result.First(f => !f.IsDirectory);
            Assert.Equal("/test/file1.txt", file.Path);
            Assert.False(file.IsDirectory);
            Assert.Equal(100, file.Size);
            Assert.NotNull(file.LastModified);

            var directory = result.First(f => f.IsDirectory);
            Assert.Equal("/test/dir1", directory.Path);
            Assert.True(directory.IsDirectory);
            Assert.Null(directory.Size);
        }

        [Fact]
        public async Task ListAsync_WithNonexistentDirectory_ThrowsDirectoryNotFoundException() {
            // Arrange
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Throws(new DeviceExecutionException("OSError: ENOENT"));

            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                () => _fileSystem.ListAsync("/nonexistent"));
        }

        [Fact]
        public async Task GetFileInfoAsync_WithExistingFile_ReturnsFileInfo() {
            // Arrange
            var jsonResponse = """{"path": "/test/file.txt", "is_directory": false, "size": 42, "modified": 1640995200}""";
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(jsonResponse);

            // Act
            var result = await _fileSystem.GetFileInfoAsync("/test/file.txt");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("/test/file.txt", result.Path);
            Assert.False(result.IsDirectory);
            Assert.Equal(42, result.Size);
            Assert.NotNull(result.LastModified);
        }

        [Fact]
        public async Task GetFileInfoAsync_WithNonexistentFile_ReturnsNull() {
            // Arrange
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("null");

            // Act
            var result = await _fileSystem.GetFileInfoAsync("/nonexistent/file.txt");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task ReadFileAsync_WithSmallFile_ReturnsFileContent() {
            // Arrange
            var testData = "Hello, World!"u8.ToArray();
            var hexData = Convert.ToHexString(testData);

            // Mock GetFileInfoAsync
            var fileInfoResponse = """{"path": "/test/file.txt", "is_directory": false, "size": 13, "modified": 1640995200}""";
            _mockDevice.ExecuteAsync<string>(Arg.Is<string>(s => s.Contains("os.stat")), Arg.Any<CancellationToken>())
                .Returns(fileInfoResponse);

            // Mock ReadFileAsync
            _mockDevice.ExecuteAsync<string>(Arg.Is<string>(s => s.Contains("binascii.hexlify")), Arg.Any<CancellationToken>())
                .Returns(hexData.ToLowerInvariant());

            // Act
            var result = await _fileSystem.ReadFileAsync("/test/file.txt");

            // Assert
            Assert.Equal(testData, result);
        }

        [Fact]
        public async Task ReadFileAsync_WithNonexistentFile_ThrowsFileNotFoundException() {
            // Arrange
            _mockDevice.ExecuteAsync<string>(Arg.Is<string>(s => s.Contains("os.stat")), Arg.Any<CancellationToken>())
                .Returns("null");

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _fileSystem.ReadFileAsync("/nonexistent/file.txt"));
        }

        [Fact]
        public async Task ReadFileAsync_WithDirectory_ThrowsUnauthorizedAccessException() {
            // Arrange
            var fileInfoResponse = """{"path": "/test/dir", "is_directory": true, "size": null, "modified": 1640995200}""";
            _mockDevice.ExecuteAsync<string>(Arg.Is<string>(s => s.Contains("os.stat")), Arg.Any<CancellationToken>())
                .Returns(fileInfoResponse);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => _fileSystem.ReadFileAsync("/test/dir"));
        }

        [Fact]
        public async Task ReadTextFileAsync_WithValidFile_ReturnsTextContent() {
            // Arrange
            var testText = "Hello, World!";
            var testData = Encoding.UTF8.GetBytes(testText);
            var hexData = Convert.ToHexString(testData);

            // Mock GetFileInfoAsync
            var fileInfoResponse = """{"path": "/test/file.txt", "is_directory": false, "size": 13, "modified": 1640995200}""";
            _mockDevice.ExecuteAsync<string>(Arg.Is<string>(s => s.Contains("os.stat")), Arg.Any<CancellationToken>())
                .Returns(fileInfoResponse);

            // Mock ReadFileAsync
            _mockDevice.ExecuteAsync<string>(Arg.Is<string>(s => s.Contains("binascii.hexlify")), Arg.Any<CancellationToken>())
                .Returns(hexData.ToLowerInvariant());

            // Act
            var result = await _fileSystem.ReadTextFileAsync("/test/file.txt");

            // Assert
            Assert.Equal(testText, result);
        }

        [Fact]
        public async Task WriteFileAsync_WithSmallFile_WritesSuccessfully() {
            // Arrange
            var testData = "Hello, World!"u8.ToArray();
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("success");

            // Act
            await _fileSystem.WriteFileAsync("/test/file.txt", testData);

            // Assert
            await _mockDevice.Received(1).ExecuteAsync<string>(
                Arg.Is<string>(s => s.Contains("binascii.unhexlify") && s.Contains("with open")),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task WriteTextFileAsync_WithValidText_WritesSuccessfully() {
            // Arrange
            var testText = "Hello, World!";
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("success");

            // Act
            await _fileSystem.WriteTextFileAsync("/test/file.txt", testText);

            // Assert
            await _mockDevice.Received(1).ExecuteAsync<string>(
                Arg.Is<string>(s => s.Contains("binascii.unhexlify") && s.Contains("with open")),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeleteFileAsync_WithExistingFile_DeletesSuccessfully() {
            // Arrange
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("success");

            // Act
            await _fileSystem.DeleteFileAsync("/test/file.txt");

            // Assert
            await _mockDevice.Received(1).ExecuteAsync<string>(
                Arg.Is<string>(s => s.Contains("os.remove")),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeleteFileAsync_WithNonexistentFile_ThrowsFileNotFoundException() {
            // Arrange
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("not_found");

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => _fileSystem.DeleteFileAsync("/nonexistent/file.txt"));
        }

        [Fact]
        public async Task CreateDirectoryAsync_WithValidPath_CreatesSuccessfully() {
            // Arrange
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("success");

            // Act
            await _fileSystem.CreateDirectoryAsync("/test/newdir");

            // Assert
            await _mockDevice.Received(1).ExecuteAsync<string>(
                Arg.Is<string>(s => s.Contains("os.mkdir")),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeleteDirectoryAsync_WithValidPath_DeletesSuccessfully() {
            // Arrange
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("success");

            // Act
            await _fileSystem.DeleteDirectoryAsync("/test/dir");

            // Assert
            await _mockDevice.Received(1).ExecuteAsync<string>(
                Arg.Is<string>(s => s.Contains("os.rmdir")),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task DeleteDirectoryAsync_WithNonexistentDirectory_ThrowsDirectoryNotFoundException() {
            // Arrange
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("not_found");

            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                () => _fileSystem.DeleteDirectoryAsync("/nonexistent/dir"));
        }

        [Fact]
        public async Task DeleteDirectoryAsync_WithNonEmptyDirectory_ThrowsIOException() {
            // Arrange
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("not_empty");

            // Act & Assert
            await Assert.ThrowsAsync<IOException>(
                () => _fileSystem.DeleteDirectoryAsync("/test/dir"));
        }

        [Fact]
        public async Task ExistsAsync_WithExistingPath_ReturnsTrue() {
            // Arrange
            var fileInfoResponse = """{"path": "/test/file.txt", "is_directory": false, "size": 42, "modified": 1640995200}""";
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(fileInfoResponse);

            // Act
            var result = await _fileSystem.ExistsAsync("/test/file.txt");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ExistsAsync_WithNonexistentPath_ReturnsFalse() {
            // Arrange
            _mockDevice.ExecuteAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns("null");

            // Act
            var result = await _fileSystem.ExistsAsync("/nonexistent/file.txt");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CalculateChecksumAsync_WithValidFile_ReturnsChecksum() {
            // Arrange
            var testData = "Hello, World!"u8.ToArray();
            var hexData = Convert.ToHexString(testData);

            // Mock GetFileInfoAsync
            var fileInfoResponse = """{"path": "/test/file.txt", "is_directory": false, "size": 13, "modified": 1640995200}""";
            _mockDevice.ExecuteAsync<string>(Arg.Is<string>(s => s.Contains("os.stat")), Arg.Any<CancellationToken>())
                .Returns(fileInfoResponse);

            // Mock ReadFileAsync
            _mockDevice.ExecuteAsync<string>(Arg.Is<string>(s => s.Contains("binascii.hexlify")), Arg.Any<CancellationToken>())
                .Returns(hexData.ToLowerInvariant());

            // Act
            var result = await _fileSystem.CalculateChecksumAsync("/test/file.txt", "md5");

            // Assert
            Assert.NotEmpty(result);
            Assert.Equal(32, result.Length); // MD5 hash length
        }

        [Fact]
        public async Task CalculateChecksumAsync_WithUnsupportedAlgorithm_ThrowsNotSupportedException() {
            // Arrange
            var testData = "Hello, World!"u8.ToArray();
            var hexData = Convert.ToHexString(testData);

            // Mock GetFileInfoAsync
            var fileInfoResponse = """{"path": "/test/file.txt", "is_directory": false, "size": 13, "modified": 1640995200}""";
            _mockDevice.ExecuteAsync<string>(Arg.Is<string>(s => s.Contains("os.stat")), Arg.Any<CancellationToken>())
                .Returns(fileInfoResponse);

            // Mock ReadFileAsync
            _mockDevice.ExecuteAsync<string>(Arg.Is<string>(s => s.Contains("binascii.hexlify")), Arg.Any<CancellationToken>())
                .Returns(hexData.ToLowerInvariant());

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(
                () => _fileSystem.CalculateChecksumAsync("/test/file.txt", "unsupported"));
        }
    }
}
