// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Caching {
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines an interface for persistent cache storage in Belay.NET.
    /// </summary>
    /// <remarks>
    /// This is a placeholder interface for future persistent cache storage implementations.
    /// Future implementations may include file-system, database, or distributed cache backends.
    /// </remarks>
    public interface IPersistentCacheStorage {
        /// <summary>
        /// Reads a cached value from persistent storage.
        /// </summary>
        /// <typeparam name="T">The type of the cached value.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>The cached value, or default if not found.</returns>
        Task<T> ReadAsync<T>(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes a value to persistent storage.
        /// </summary>
        /// <typeparam name="T">The type of the value to cache.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="expiration">Optional expiration time.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        Task WriteAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a specific entry from persistent storage.
        /// </summary>
        /// <param name="key">The cache key to remove.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        Task DeleteAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all entries from persistent storage.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        Task ClearAllAsync(CancellationToken cancellationToken = default);
    }
}
