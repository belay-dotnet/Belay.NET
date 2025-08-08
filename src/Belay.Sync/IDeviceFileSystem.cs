// Copyright (c) 2024 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for more information.
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents information about a file or directory on the device.
    /// </summary>
    public sealed class DeviceFileInfo {
        /// <summary>
        /// Gets the full path of the file or directory.
        /// </summary>
        public required string Path { get; init; }

        /// <summary>
        /// Gets a value indicating whether this entry is a directory.
        /// </summary>
        public required bool IsDirectory { get; init; }

        /// <summary>
        /// Gets the size of the file in bytes. Null for directories.
        /// </summary>
        public long? Size { get; init; }

        /// <summary>
        /// Gets the last modified timestamp. May be null if not supported by the device.
        /// </summary>
        public DateTime? LastModified { get; init; }

        /// <summary>
        /// Gets the checksum of the file content. May be null if not computed.
        /// </summary>
        public string? Checksum { get; init; }
    }

    /// <summary>
    /// Provides an abstraction for file system operations on MicroPython/CircuitPython devices.
    /// </summary>
    public interface IDeviceFileSystem {
        /// <summary>
        /// Lists the contents of a directory on the device.
        /// </summary>
        /// <param name="path">The directory path to list. Use "/" for root directory.</param>
        /// <param name="recursive">Whether to list contents recursively.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A collection of file and directory information.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the directory does not exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
        Task<IReadOnlyList<DeviceFileInfo>> ListAsync(
            string path = "/",
            bool recursive = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets information about a specific file or directory on the device.
        /// </summary>
        /// <param name="path">The path to examine.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>File information, or null if the path does not exist.</returns>
        Task<DeviceFileInfo?> GetFileInfoAsync(
            string path,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the entire contents of a file from the device.
        /// </summary>
        /// <param name="path">The file path to read.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The file contents as a byte array.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
        Task<byte[]> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the entire contents of a text file from the device.
        /// </summary>
        /// <param name="path">The file path to read.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The file contents as a string.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
        Task<string> ReadTextFileAsync(
            string path,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes data to a file on the device, creating it if necessary.
        /// </summary>
        /// <param name="path">The file path to write to.</param>
        /// <param name="content">The content to write.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs.</exception>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        Task WriteFileAsync(
            string path,
            byte[] content,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes text to a file on the device, creating it if necessary.
        /// </summary>
        /// <param name="path">The file path to write to.</param>
        /// <param name="content">The text content to write.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs.</exception>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        Task WriteTextFileAsync(
            string path,
            string content,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a file from the device.
        /// </summary>
        /// <param name="path">The file path to delete.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        Task DeleteFileAsync(
            string path,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a directory on the device.
        /// </summary>
        /// <param name="path">The directory path to create.</param>
        /// <param name="recursive">Whether to create parent directories if they don't exist.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs.</exception>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        Task CreateDirectoryAsync(
            string path,
            bool recursive = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a directory from the device.
        /// </summary>
        /// <param name="path">The directory path to delete.</param>
        /// <param name="recursive">Whether to delete the directory and all its contents.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <exception cref="DirectoryNotFoundException">Thrown when the directory does not exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
        /// <exception cref="IOException">Thrown when the directory is not empty and recursive is false.</exception>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        Task DeleteDirectoryAsync(
            string path,
            bool recursive = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks whether a file or directory exists on the device.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>True if the path exists, false otherwise.</returns>
        Task<bool> ExistsAsync(
            string path,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Calculates a checksum for the specified file.
        /// </summary>
        /// <param name="path">The file path to calculate checksum for.</param>
        /// <param name="algorithm">The checksum algorithm to use (e.g., "md5", "sha256").</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The hexadecimal checksum string.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="NotSupportedException">Thrown when the algorithm is not supported.</exception>
        Task<string> CalculateChecksumAsync(
            string path,
            string algorithm = "md5",
            CancellationToken cancellationToken = default);
    }
}
