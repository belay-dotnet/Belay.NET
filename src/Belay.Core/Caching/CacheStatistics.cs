// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Caching {
    using System.Threading;

    /// <summary>
    /// Tracks performance metrics and statistics for method deployment cache.
    /// </summary>
    public sealed class CacheStatistics {
        private long totalHits;
        private long totalMisses;
        private long totalEvictions;
        private long currentEntryCount;

        /// <summary>
        /// Gets total number of successful cache hits.
        /// </summary>
        public long TotalHits => Interlocked.Read(ref this.totalHits);

        /// <summary>
        /// Gets total number of cache misses.
        /// </summary>
        public long TotalMisses => Interlocked.Read(ref this.totalMisses);

        /// <summary>
        /// Gets total number of cache entry evictions.
        /// </summary>
        public long TotalEvictions => Interlocked.Read(ref this.totalEvictions);

        /// <summary>
        /// Gets current number of entries in the cache.
        /// </summary>
        public long CurrentEntryCount => Interlocked.Read(ref this.currentEntryCount);

        /// <summary>
        /// Gets calculates the cache hit ratio.
        /// </summary>
        public double HitRatio {
            get {
                var total = this.TotalHits + this.TotalMisses;
                return total > 0 ? (double)this.TotalHits / total : 0;
            }
        }

        /// <summary>
        /// Records a cache hit.
        /// </summary>
        public void RecordHit() => Interlocked.Increment(ref this.totalHits);

        /// <summary>
        /// Records a cache miss.
        /// </summary>
        public void RecordMiss() => Interlocked.Increment(ref this.totalMisses);

        /// <summary>
        /// Records a cache entry eviction.
        /// </summary>
        public void RecordEviction() => Interlocked.Increment(ref this.totalEvictions);

        /// <summary>
        /// Increments the current entry count.
        /// </summary>
        public void IncrementEntryCount() => Interlocked.Increment(ref this.currentEntryCount);

        /// <summary>
        /// Decrements the current entry count.
        /// </summary>
        public void DecrementEntryCount() => Interlocked.Decrement(ref this.currentEntryCount);

        /// <summary>
        /// Resets all statistics to their initial state.
        /// </summary>
        public void Reset() {
            Interlocked.Exchange(ref this.totalHits, 0);
            Interlocked.Exchange(ref this.totalMisses, 0);
            Interlocked.Exchange(ref this.totalEvictions, 0);
            Interlocked.Exchange(ref this.currentEntryCount, 0);
        }
    }
}
