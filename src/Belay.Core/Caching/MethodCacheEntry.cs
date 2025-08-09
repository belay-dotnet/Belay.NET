// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Caching {
    using System;

    /// <summary>
    /// Base interface for cache entries to enable type-safe storage.
    /// </summary>
    internal interface ICacheEntry {
        DateTime CreatedAt { get; }

        DateTime LastAccessedAt { get; }

        bool IsExpired { get; }

        void UpdateLastAccessed();

        object GetValue();
    }

    /// <summary>
    /// Represents a cached method deployment entry with metadata and expiration tracking.
    /// </summary>
    /// <typeparam name="T">The type of the cached result.</typeparam>
    public sealed class MethodCacheEntry<T> : ICacheEntry {
        /// <summary>
        /// Gets the cached result value.
        /// </summary>
        public T Value { get; }

        /// <summary>
        /// Gets the timestamp when the cache entry was created.
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Gets the expiration time for the cache entry.
        /// </summary>
        public TimeSpan ExpiresAfter { get; }

        /// <summary>
        /// Gets the timestamp when the cache entry was last accessed.
        /// </summary>
        public DateTime LastAccessedAt { get; private set; }

        /// <summary>
        /// Gets a value indicating whether determines whether the cache entry has expired.
        /// </summary>
        public bool IsExpired => DateTime.UtcNow - this.CreatedAt > this.ExpiresAfter;

        /// <summary>
        /// Initializes a new instance of the <see cref="MethodCacheEntry{T}"/> class.
        /// Creates a new cache entry with the specified value and expiration policy.
        /// </summary>
        /// <param name="value">The cached value.</param>
        /// <param name="expiresAfter">Optional time-to-live duration.</param>
        public MethodCacheEntry(T value, TimeSpan? expiresAfter = null) {
            this.Value = value;
            this.CreatedAt = DateTime.UtcNow;
            this.LastAccessedAt = this.CreatedAt;
            this.ExpiresAfter = expiresAfter ?? TimeSpan.FromMinutes(30); // Default 30-minute expiration
        }

        /// <summary>
        /// Updates the last accessed timestamp.
        /// </summary>
        public void UpdateLastAccessed() {
            this.LastAccessedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the cached value as an object.
        /// </summary>
        /// <returns>The cached value.</returns>
        public object GetValue() {
            return this.Value!;
        }

        /// <summary>
        /// Provides the time remaining before the cache entry expires.
        /// </summary>
        /// <returns></returns>
        public TimeSpan GetRemainingLifetime() {
            var elapsed = DateTime.UtcNow - this.CreatedAt;
            return elapsed > this.ExpiresAfter ? TimeSpan.Zero : this.ExpiresAfter - elapsed;
        }
    }
}
