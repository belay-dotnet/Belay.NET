// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Caching {
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the contract for a method deployment cache.
    /// </summary>
    public interface IMethodDeploymentCache : IDisposable {
        /// <summary>
        /// Attempts to retrieve a cached result for the specified method cache key.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="key">The method cache key.</param>
        /// <returns>The cached result, or null if not found.</returns>
        T? Get<T>(MethodCacheKey key);

        /// <summary>
        /// Sets a value in the cache for the specified method cache key.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="key">The method cache key.</param>
        /// <param name="value">The value to cache.</param>
        /// <param name="expiresAfter">Optional expiration time.</param>
        void Set<T>(MethodCacheKey key, T value, TimeSpan? expiresAfter = null);

        /// <summary>
        /// Removes a specific entry from the cache.
        /// </summary>
        /// <param name="key">The method cache key to remove.</param>
        /// <returns>True if the entry was removed, false if it didn't exist.</returns>
        bool Remove(MethodCacheKey key);

        /// <summary>
        /// Retrieves current cache statistics.
        /// </summary>
        /// <returns>Current cache statistics.</returns>
        CacheStatistics GetStatistics();

        /// <summary>
        /// Clears all entries from the cache.
        /// </summary>
        void ClearAll();
    }
}
