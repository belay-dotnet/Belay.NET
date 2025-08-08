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
    /// Configuration options for method deployment caching behavior.
    /// </summary>
    public sealed class MethodCacheConfiguration
    {
        /// <summary>
        /// Maximum number of cache entries allowed.
        /// </summary>
        public int MaxCacheSize { get; set; } = 1000;

        /// <summary>
        /// Default time-to-live for cache entries.
        /// </summary>
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Enables automatic cache entry eviction when max size is reached.
        /// </summary>
        public bool AutoEvictOnMaxSize { get; set; } = true;

        /// <summary>
        /// Enables periodic cache cleanup to remove expired entries.
        /// </summary>
        public bool EnablePeriodicCleanup { get; set; } = true;

        /// <summary>
        /// Interval for periodic cache cleanup.
        /// </summary>
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Validates the configuration settings.
        /// </summary>
        public void Validate()
        {
            if (MaxCacheSize <= 0)
                throw new ArgumentException("Max cache size must be greater than zero.", nameof(MaxCacheSize));

            if (DefaultExpiration <= TimeSpan.Zero)
                throw new ArgumentException("Default expiration must be a positive timespan.", nameof(DefaultExpiration));

            if (EnablePeriodicCleanup && CleanupInterval <= TimeSpan.Zero)
                throw new ArgumentException("Cleanup interval must be a positive timespan.", nameof(CleanupInterval));
        }
    }
}