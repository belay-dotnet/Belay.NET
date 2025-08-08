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
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Belay.Core.Caching
{
    /// <summary>
    /// Provides an in-memory implementation of the method deployment cache.
    /// </summary>
    public sealed class MethodDeploymentCache : IMethodDeploymentCache, IDisposable
    {
        private readonly ConcurrentDictionary<MethodCacheKey, ICacheEntry> _cache;
        private readonly MethodCacheConfiguration _configuration;
        private readonly CacheStatistics _statistics;
        private readonly ILogger<MethodDeploymentCache> _logger;
        private readonly CancellationTokenSource _cleanupCancellationSource;
        private readonly Task _cleanupTask;

        /// <summary>
        /// Creates a new method deployment cache instance.
        /// </summary>
        /// <param name="configuration">Cache configuration options</param>
        /// <param name="logger">Logger for diagnostics</param>
        public MethodDeploymentCache(
            MethodCacheConfiguration configuration = null,
            ILogger<MethodDeploymentCache> logger = null)
        {
            _configuration = configuration ?? new MethodCacheConfiguration();
            _configuration.Validate();
            _cache = new ConcurrentDictionary<MethodCacheKey, ICacheEntry>();
            _statistics = new CacheStatistics();
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MethodDeploymentCache>.Instance;

            _cleanupCancellationSource = new CancellationTokenSource();
            _cleanupTask = StartPeriodicCleanup();
        }

        /// <inheritdoc/>
        public T? Get<T>(MethodCacheKey key)
        {
            if (_cache.TryGetValue(key, out var cachedEntry))
            {
                if (cachedEntry is MethodCacheEntry<T> entry && !entry.IsExpired)
                {
                    entry.UpdateLastAccessed();
                    _statistics.RecordHit();
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return entry.Value;
                }
                else if (cachedEntry.IsExpired)
                {
                    // Remove expired entry
                    _cache.TryRemove(key, out _);
                    _statistics.RecordEviction();
                }
            }

            _statistics.RecordMiss();
            _logger.LogDebug("Cache miss for key: {Key}", key);
            return default(T);
        }

        /// <inheritdoc/>
        public void Set<T>(MethodCacheKey key, T value, TimeSpan? expiresAfter = null)
        {
            if (_cache.Count >= _configuration.MaxCacheSize && _configuration.AutoEvictOnMaxSize)
            {
                EvictOldestEntry();
            }

            var entry = new MethodCacheEntry<T>(value, expiresAfter ?? _configuration.DefaultExpiration);
            _cache[key] = entry;
            _statistics.IncrementEntryCount();

            _logger.LogDebug("Added cache entry for key: {Key}", key);
        }

        /// <inheritdoc/>
        public bool Remove(MethodCacheKey key)
        {
            if (_cache.TryRemove(key, out _))
            {
                _statistics.DecrementEntryCount();
                _logger.LogDebug("Removed cache entry for key: {Key}", key);
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public CacheStatistics GetStatistics()
        {
            return _statistics;
        }

        /// <inheritdoc/>
        public void ClearAll()
        {
            var count = _cache.Count;
            _cache.Clear();
            _statistics.Reset();
            _logger.LogInformation("Cleared entire method deployment cache with {Count} entries", count);
        }

        private void EvictOldestEntry()
        {
            var oldestKey = default(MethodCacheKey);
            var oldestTimestamp = DateTime.MaxValue;

            foreach (var entry in _cache)
            {
                if (entry.Value is IMethodCacheEntryMetadata metadata && metadata.CreatedAt < oldestTimestamp)
                {
                    oldestKey = entry.Key;
                    oldestTimestamp = metadata.CreatedAt;
                }
            }

            if (oldestKey != null)
            {
                _cache.TryRemove(oldestKey, out _);
                _statistics.RecordEviction();
                _statistics.DecrementEntryCount();
                _logger?.LogDebug("Evicted oldest cache entry: {Key}", oldestKey);
            }
        }

        private Task StartPeriodicCleanup()
        {
            if (!_configuration.EnablePeriodicCleanup)
                return Task.CompletedTask;

            return Task.Run(async () =>
            {
                try
                {
                    while (!_cleanupCancellationSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(_configuration.CleanupInterval, _cleanupCancellationSource.Token);
                        CleanExpiredEntries();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when disposal is requested
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in cache cleanup task");
                }
            }, _cleanupCancellationSource.Token);
        }

        private void CleanExpiredEntries()
        {
            foreach (var key in _cache.Keys)
            {
                if (_cache.TryGetValue(key, out var entry) && 
                    entry is IMethodCacheEntryMetadata metadata && 
                    metadata.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    _statistics.RecordEviction();
                    _statistics.DecrementEntryCount();
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                // Cancel the cleanup task
                _cleanupCancellationSource.Cancel();
                
                // Wait for cleanup task to complete (with timeout to prevent hanging)
                if (!_cleanupTask.IsCompleted)
                {
                    try
                    {
                        _cleanupTask.Wait(TimeSpan.FromSeconds(5));
                    }
                    catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
                    {
                        // Expected when cleanup task is cancelled
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error waiting for cleanup task to complete during disposal");
                    }
                }
                
                _cleanupCancellationSource.Dispose();
                _cache.Clear();
                
                _logger.LogDebug("MethodDeploymentCache disposed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MethodDeploymentCache disposal");
            }
        }

        /// <summary>
        /// Metadata interface for cache entries to support expiration and cleanup.
        /// </summary>
        private interface IMethodCacheEntryMetadata
        {
            DateTime CreatedAt { get; }
            bool IsExpired { get; }
        }
    }
}