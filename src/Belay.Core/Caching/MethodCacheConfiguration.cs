// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Caching {
    using System;

    /// <summary>
    /// Configuration options for method deployment caching behavior.
    /// </summary>
    public sealed class MethodCacheConfiguration {
        /// <summary>
        /// Gets or sets maximum number of cache entries allowed.
        /// </summary>
        public int MaxCacheSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets default time-to-live for cache entries.
        /// </summary>
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets or sets a value indicating whether enables automatic cache entry eviction when max size is reached.
        /// </summary>
        public bool AutoEvictOnMaxSize { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether enables periodic cache cleanup to remove expired entries.
        /// </summary>
        public bool EnablePeriodicCleanup { get; set; } = true;

        /// <summary>
        /// Gets or sets interval for periodic cache cleanup.
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Validates the configuration settings.
        /// </summary>
        public void Validate() {
            if (this.MaxCacheSize <= 0) {
                throw new ArgumentException("Max cache size must be greater than zero.", nameof(this.MaxCacheSize));
            }

            if (this.DefaultExpiration <= TimeSpan.Zero) {
                throw new ArgumentException("Default expiration must be a positive timespan.", nameof(this.DefaultExpiration));
            }

            if (this.EnablePeriodicCleanup && this.CleanupInterval <= TimeSpan.Zero) {
                throw new ArgumentException("Cleanup interval must be a positive timespan.", nameof(this.CleanupInterval));
            }
        }
    }
}
