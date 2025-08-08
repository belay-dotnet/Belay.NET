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

using System.Threading;

namespace Belay.Core.Caching
{
    /// <summary>
    /// Tracks performance metrics and statistics for method deployment cache.
    /// </summary>
    public sealed class CacheStatistics
    {
        private long _totalHits;
        private long _totalMisses;
        private long _totalEvictions;
        private long _currentEntryCount;

        /// <summary>
        /// Total number of successful cache hits.
        /// </summary>
        public long TotalHits => Interlocked.Read(ref _totalHits);

        /// <summary>
        /// Total number of cache misses.
        /// </summary>
        public long TotalMisses => Interlocked.Read(ref _totalMisses);

        /// <summary>
        /// Total number of cache entry evictions.
        /// </summary>
        public long TotalEvictions => Interlocked.Read(ref _totalEvictions);

        /// <summary>
        /// Current number of entries in the cache.
        /// </summary>
        public long CurrentEntryCount => Interlocked.Read(ref _currentEntryCount);

        /// <summary>
        /// Calculates the cache hit ratio.
        /// </summary>
        public double HitRatio
        {
            get
            {
                var total = TotalHits + TotalMisses;
                return total > 0 ? (double)TotalHits / total : 0;
            }
        }

        /// <summary>
        /// Records a cache hit.
        /// </summary>
        public void RecordHit() => Interlocked.Increment(ref _totalHits);

        /// <summary>
        /// Records a cache miss.
        /// </summary>
        public void RecordMiss() => Interlocked.Increment(ref _totalMisses);

        /// <summary>
        /// Records a cache entry eviction.
        /// </summary>
        public void RecordEviction() => Interlocked.Increment(ref _totalEvictions);

        /// <summary>
        /// Increments the current entry count.
        /// </summary>
        public void IncrementEntryCount() => Interlocked.Increment(ref _currentEntryCount);

        /// <summary>
        /// Decrements the current entry count.
        /// </summary>
        public void DecrementEntryCount() => Interlocked.Decrement(ref _currentEntryCount);

        /// <summary>
        /// Resets all statistics to their initial state.
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _totalHits, 0);
            Interlocked.Exchange(ref _totalMisses, 0);
            Interlocked.Exchange(ref _totalEvictions, 0);
            Interlocked.Exchange(ref _currentEntryCount, 0);
        }
    }
}