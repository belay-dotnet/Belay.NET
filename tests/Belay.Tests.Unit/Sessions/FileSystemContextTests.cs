// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using Belay.Core.Sessions;
using FluentAssertions;
using NUnit.Framework;

namespace Belay.Tests.Unit.Sessions {
    /// <summary>
    /// Tests for the FileSystemContext class.
    /// </summary>
    public class FileSystemContextTests {
        [Test]
        public void Constructor_WithValidParameters_InitializesCorrectly() {
            // Arrange
            var sessionId = "test-session";
            var capabilities = FileSystemCapabilities.BasicFileOperations | FileSystemCapabilities.DirectoryOperations;

            // Act
            var context = new FileSystemContext(sessionId, capabilities);

            // Assert
            context.SessionId.Should().Be(sessionId);
            context.Capabilities.Should().Be(capabilities);
            context.IsFileSystemSupported.Should().BeTrue();
            context.CurrentDirectory.Should().Be("/");
            context.CachedFileInfo.Should().BeEmpty();
        }

        [Test]
        public void Constructor_WithNullSessionId_ThrowsArgumentNullException() {
            // Act & Assert
            var act = () => new FileSystemContext(null!, FileSystemCapabilities.BasicFileOperations);
            act.Should().Throw<ArgumentNullException>()
                .Which.ParamName.Should().Be("sessionId");
        }

        [Test]
        public void Constructor_WithNoCapabilities_SetsFileSystemNotSupported() {
            // Arrange & Act
            var context = new FileSystemContext("test-session", FileSystemCapabilities.None);

            // Assert
            context.IsFileSystemSupported.Should().BeFalse();
            context.Capabilities.Should().Be(FileSystemCapabilities.None);
        }

        [Test]
        public void CurrentDirectory_SetValidPath_UpdatesCurrentDirectory() {
            // Arrange
            var context = new FileSystemContext("test-session");
            var newPath = "/home/user";

            // Act
            context.CurrentDirectory = newPath;

            // Assert
            context.CurrentDirectory.Should().Be(newPath);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void CurrentDirectory_SetInvalidPath_ThrowsArgumentException(string? invalidPath) {
            // Arrange
            var context = new FileSystemContext("test-session");

            // Act & Assert
            var act = () => context.CurrentDirectory = invalidPath!;
            act.Should().Throw<ArgumentException>()
                .Which.ParamName.Should().Be("value");
        }

        [Test]
        public async Task RefreshDirectoryAsync_WithValidPath_ClearsMatchingCacheEntries() {
            // Arrange
            var context = new FileSystemContext("test-session");
            var directoryPath = "/home";

            // Add some test entries to cache
            var testFile = new FileMetadata {
                Path = "/home/test.txt",
                Name = "test.txt",
                IsDirectory = false
            };
            context.CacheFileMetadata(testFile);

            // Act
            await context.RefreshDirectoryAsync(directoryPath);

            // Assert
            context.CachedFileInfo.Should().BeEmpty();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public async Task RefreshDirectoryAsync_WithInvalidPath_ThrowsArgumentException(string? invalidPath) {
            // Arrange
            var context = new FileSystemContext("test-session");

            // Act & Assert
            var act = async () => await context.RefreshDirectoryAsync(invalidPath!);
            await act.Should().ThrowAsync<ArgumentException>()
                .Where(e => e.ParamName == "path");
        }

        [Test]
        public async Task InvalidateCacheAsync_WithValidPath_RemovesCachedEntry() {
            // Arrange
            var context = new FileSystemContext("test-session");
            var filePath = "/home/test.txt";
            var testFile = new FileMetadata {
                Path = filePath,
                Name = "test.txt",
                IsDirectory = false
            };
            context.CacheFileMetadata(testFile);

            // Act
            await context.InvalidateCacheAsync(filePath);

            // Assert
            context.CachedFileInfo.Should().BeEmpty();
        }

        [Test]
        public async Task InvalidateCacheAsync_WithNullPath_DoesNotThrow() {
            // Arrange
            var context = new FileSystemContext("test-session");

            // Act & Assert
            var act = async () => await context.InvalidateCacheAsync(null!);
            await act.Should().NotThrowAsync();
        }

        [Test]
        public async Task GetFileMetadataAsync_WithCachedFile_ReturnsCachedData() {
            // Arrange
            var context = new FileSystemContext("test-session");
            var filePath = "/home/test.txt";
            var expectedMetadata = new FileMetadata {
                Path = filePath,
                Name = "test.txt",
                IsDirectory = false,
                Size = 1024
            };
            context.CacheFileMetadata(expectedMetadata);

            // Act
            var result = await context.GetFileMetadataAsync(filePath);

            // Assert
            result.Should().NotBeNull();
            result.Should().Be(expectedMetadata);
        }

        [Test]
        public async Task GetFileMetadataAsync_WithUncachedFile_ReturnsNull() {
            // Arrange
            var context = new FileSystemContext("test-session");

            // Act
            var result = await context.GetFileMetadataAsync("/nonexistent/file.txt");

            // Assert
            result.Should().BeNull();
        }

        [Test]
        public async Task ListDirectoryAsync_WithCachedEntries_ReturnsCachedData() {
            // Arrange
            var context = new FileSystemContext("test-session");
            var directoryPath = "/home";
            var testFile = new FileMetadata {
                Path = "/home/test.txt",
                Name = "test.txt",
                IsDirectory = false
            };
            context.CacheFileMetadata(testFile);

            // Act
            var result = await context.ListDirectoryAsync(directoryPath, useCache: true);

            // Assert
            result.Should().NotBeEmpty();
            result.Should().Contain(testFile);
        }

        [Test]
        public async Task ListDirectoryAsync_WithoutCache_ReturnsEmptyList() {
            // Arrange
            var context = new FileSystemContext("test-session");

            // Act
            var result = await context.ListDirectoryAsync("/home", useCache: false);

            // Assert
            result.Should().BeEmpty();
        }

        [Test]
        public void ClearCache_RemovesAllCachedEntries() {
            // Arrange
            var context = new FileSystemContext("test-session");
            var testFile = new FileMetadata {
                Path = "/home/test.txt",
                Name = "test.txt",
                IsDirectory = false
            };
            context.CacheFileMetadata(testFile);

            // Act
            context.ClearCache();

            // Assert
            context.CachedFileInfo.Should().BeEmpty();
        }

        [Test]
        public void FileMetadata_WithValidData_InitializesCorrectly() {
            // Arrange
            var path = "/home/test.txt";
            var name = "test.txt";
            var size = 1024L;
            var lastModified = DateTime.UtcNow;

            // Act
            var metadata = new FileMetadata {
                Path = path,
                Name = name,
                IsDirectory = false,
                Size = size,
                LastModified = lastModified
            };

            // Assert
            metadata.Path.Should().Be(path);
            metadata.Name.Should().Be(name);
            metadata.IsDirectory.Should().BeFalse();
            metadata.Size.Should().Be(size);
            metadata.LastModified.Should().Be(lastModified);
            metadata.CachedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [TestCase(FileSystemCapabilities.BasicFileOperations)]
        [TestCase(FileSystemCapabilities.DirectoryOperations)]
        [TestCase(FileSystemCapabilities.FileMetadata)]
        [TestCase(FileSystemCapabilities.BasicFileOperations | FileSystemCapabilities.DirectoryOperations)]
        public void FileSystemCapabilities_EnumValues_WorkCorrectly(FileSystemCapabilities capabilities) {
            // Arrange & Act
            var context = new FileSystemContext("test-session", capabilities);

            // Assert
            context.Capabilities.Should().Be(capabilities);
            context.IsFileSystemSupported.Should().Be(capabilities != FileSystemCapabilities.None);
        }
    }
}
