// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    /// <summary>
    /// Provides file system context and state management for a device session.
    /// </summary>
    public interface IFileSystemContext {
        /// <summary>
        /// Gets or sets the current working directory on the device.
        /// </summary>
        string CurrentDirectory { get; set; }

        /// <summary>
        /// Gets cached file metadata for improved performance.
        /// </summary>
        IReadOnlyDictionary<string, FileMetadata> CachedFileInfo { get; }

        /// <summary>
        /// Gets a value indicating whether file system operations are supported.
        /// </summary>
        bool IsFileSystemSupported { get; }

        /// <summary>
        /// Gets the file system capabilities of the device.
        /// </summary>
        FileSystemCapabilities Capabilities { get; }

        /// <summary>
        /// Refreshes the directory cache for the specified path.
        /// </summary>
        /// <param name="path">The directory path to refresh.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RefreshDirectoryAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates cached file information for the specified path.
        /// </summary>
        /// <param name="path">The path to invalidate from cache.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task InvalidateCacheAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets file metadata from cache or queries the device if not cached.
        /// </summary>
        /// <param name="path">The file path to get metadata for.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>File metadata if available, null otherwise.</returns>
        Task<FileMetadata?> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists the contents of a directory with caching support.
        /// </summary>
        /// <param name="path">The directory path to list.</param>
        /// <param name="useCache">Whether to use cached results if available.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A list of file metadata for directory contents.</returns>
        Task<IReadOnlyList<FileMetadata>> ListDirectoryAsync(string path, bool useCache = true, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all cached file system information.
        /// </summary>
        void ClearCache();
    }

    /// <summary>
    /// File metadata information.
    /// </summary>
    public record FileMetadata {
        /// <summary>
        /// Gets the full path of the file.
        /// </summary>
        public required string Path { get; init; }

        /// <summary>
        /// Gets the name of the file (without directory path).
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets a value indicating whether this is a directory.
        /// </summary>
        public bool IsDirectory { get; init; }

        /// <summary>
        /// Gets the size of the file in bytes. Null for directories or if unknown.
        /// </summary>
        public long? Size { get; init; }

        /// <summary>
        /// Gets the last modified timestamp if available.
        /// </summary>
        public DateTime? LastModified { get; init; }

        /// <summary>
        /// Gets additional file attributes if supported by the device.
        /// </summary>
        public IReadOnlyDictionary<string, object> Attributes { get; init; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets the timestamp when this metadata was cached.
        /// </summary>
        public DateTime CachedAt { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// File system capabilities of the device.
    /// </summary>
    [Flags]
    public enum FileSystemCapabilities {
        /// <summary>
        /// No file system capabilities detected.
        /// </summary>
        None = 0,

        /// <summary>
        /// Basic file operations (read, write, delete).
        /// </summary>
        BasicFileOperations = 1 << 0,

        /// <summary>
        /// Directory operations (create, delete, list).
        /// </summary>
        DirectoryOperations = 1 << 1,

        /// <summary>
        /// File metadata access (size, timestamps).
        /// </summary>
        FileMetadata = 1 << 2,

        /// <summary>
        /// File permissions support.
        /// </summary>
        Permissions = 1 << 3,

        /// <summary>
        /// Symbolic links support.
        /// </summary>
        SymbolicLinks = 1 << 4,

        /// <summary>
        /// Extended attributes support.
        /// </summary>
        ExtendedAttributes = 1 << 5,

        /// <summary>
        /// File watching/monitoring support.
        /// </summary>
        FileWatching = 1 << 6,
    }

    /// <summary>
    /// Implementation of file system context for session management.
    /// </summary>
    internal sealed class FileSystemContext : IFileSystemContext {
        private readonly Dictionary<string, FileMetadata> fileCache = new();
        private readonly object cacheLock = new object();
        private string currentDirectory = "/";

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSystemContext"/> class.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="capabilities">The file system capabilities.</param>
        public FileSystemContext(string sessionId, FileSystemCapabilities capabilities = FileSystemCapabilities.BasicFileOperations | FileSystemCapabilities.DirectoryOperations) {
            this.SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            this.Capabilities = capabilities;
            this.IsFileSystemSupported = capabilities != FileSystemCapabilities.None;
        }

        /// <summary>
        /// Gets the session identifier.
        /// </summary>
        public string SessionId { get; }

        /// <inheritdoc />
        public string CurrentDirectory {
            get => this.currentDirectory;
            set {
                if (string.IsNullOrWhiteSpace(value)) {
                    throw new ArgumentException("Current directory cannot be null or whitespace", nameof(value));
                }

                this.currentDirectory = value;
            }
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, FileMetadata> CachedFileInfo {
            get {
                lock (this.cacheLock) {
                    return this.fileCache.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                }
            }
        }

        /// <inheritdoc />
        public bool IsFileSystemSupported { get; }

        /// <inheritdoc />
        public FileSystemCapabilities Capabilities { get; }

        /// <inheritdoc />
        public Task RefreshDirectoryAsync(string path, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            }

            // Invalidate cached entries for this directory
            lock (this.cacheLock) {
                var keysToRemove = this.fileCache.Keys
                    .Where(key => key.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                foreach (var key in keysToRemove) {
                    this.fileCache.Remove(key);
                }
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task InvalidateCacheAsync(string path, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(path)) {
                return Task.CompletedTask;
            }

            lock (this.cacheLock) {
                this.fileCache.Remove(path);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<FileMetadata?> GetFileMetadataAsync(string path, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(path)) {
                return Task.FromResult<FileMetadata?>(null);
            }

            lock (this.cacheLock) {
                if (this.fileCache.TryGetValue(path, out var metadata)) {
                    return Task.FromResult<FileMetadata?>(metadata);
                }
            }

            return Task.FromResult<FileMetadata?>(null);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<FileMetadata>> ListDirectoryAsync(string path, bool useCache = true, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Path cannot be null or whitespace", nameof(path));
            }

            if (useCache) {
                lock (this.cacheLock) {
                    var cachedEntries = this.fileCache.Values
                        .Where(metadata => {
                            var parentDir = System.IO.Path.GetDirectoryName(metadata.Path);
                            return string.Equals(parentDir, path, StringComparison.OrdinalIgnoreCase);
                        })
                        .ToArray();

                    if (cachedEntries.Length > 0) {
                        return Task.FromResult<IReadOnlyList<FileMetadata>>(cachedEntries);
                    }
                }
            }

            // Return empty list - actual implementation would query the device
            return Task.FromResult<IReadOnlyList<FileMetadata>>(Array.Empty<FileMetadata>());
        }

        /// <inheritdoc />
        public void ClearCache() {
            lock (this.cacheLock) {
                this.fileCache.Clear();
            }
        }

        /// <summary>
        /// Adds file metadata to the cache.
        /// </summary>
        /// <param name="metadata">The file metadata to cache.</param>
        internal void CacheFileMetadata(FileMetadata metadata) {
            if (metadata == null) {
                return;
            }

            lock (this.cacheLock) {
                this.fileCache[metadata.Path] = metadata;
            }
        }
    }
}
