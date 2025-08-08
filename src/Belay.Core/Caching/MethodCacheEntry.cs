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

namespace Belay.Core.Caching
{
    /// <summary>
    /// Base interface for cache entries to enable type-safe storage.
    /// </summary>
    internal interface ICacheEntry
    {
        DateTime CreatedAt { get; }
        DateTime LastAccessedAt { get; }
        bool IsExpired { get; }
        void UpdateLastAccessed();
        object GetValue();
    }

    /// <summary>
    /// Represents a cached method deployment entry with metadata and expiration tracking.
    /// </summary>
    /// <typeparam name="T">The type of the cached result</typeparam>
    public sealed class MethodCacheEntry<T> : ICacheEntry
    {
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
        /// Determines whether the cache entry has expired.
        /// </summary>
        public bool IsExpired => DateTime.UtcNow - CreatedAt > ExpiresAfter;

        /// <summary>
        /// Creates a new cache entry with the specified value and expiration policy.
        /// </summary>
        /// <param name="value">The cached value</param>
        /// <param name="expiresAfter">Optional time-to-live duration</param>
        public MethodCacheEntry(T value, TimeSpan? expiresAfter = null)
        {
            Value = value;
            CreatedAt = DateTime.UtcNow;
            LastAccessedAt = CreatedAt;
            ExpiresAfter = expiresAfter ?? TimeSpan.FromMinutes(30); // Default 30-minute expiration
        }

        /// <summary>
        /// Updates the last accessed timestamp.
        /// </summary>
        public void UpdateLastAccessed()
        {
            LastAccessedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the cached value as an object.
        /// </summary>
        /// <returns>The cached value.</returns>
        public object GetValue()
        {
            return Value!;
        }

        /// <summary>
        /// Provides the time remaining before the cache entry expires.
        /// </summary>
        public TimeSpan GetRemainingLifetime()
        {
            var elapsed = DateTime.UtcNow - CreatedAt;
            return elapsed > ExpiresAfter ? TimeSpan.Zero : ExpiresAfter - elapsed;
        }
    }
}