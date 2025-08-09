// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Caching {
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Provides an in-memory implementation of the method deployment cache.
    /// </summary>
    public sealed class MethodDeploymentCache : IMethodDeploymentCache {
        private readonly ConcurrentDictionary<MethodCacheKey, ICacheEntry> cache;
        private readonly MethodCacheConfiguration configuration;
        private readonly CacheStatistics statistics;
        private readonly ILogger<MethodDeploymentCache> logger;
        private readonly CancellationTokenSource cleanupCancellationSource;
        private readonly Task cleanupTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="MethodDeploymentCache"/> class.
        /// Creates a new method deployment cache instance.
        /// </summary>
        /// <param name="configuration">Cache configuration options.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        public MethodDeploymentCache(
            MethodCacheConfiguration? configuration = null,
            ILogger<MethodDeploymentCache>? logger = null) {
            this.configuration = configuration ?? new MethodCacheConfiguration();
            this.configuration.Validate();
            this.cache = new ConcurrentDictionary<MethodCacheKey, ICacheEntry>();
            this.statistics = new CacheStatistics();
            this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MethodDeploymentCache>.Instance;

            this.cleanupCancellationSource = new CancellationTokenSource();
            this.cleanupTask = this.StartPeriodicCleanup();
        }

        /// <inheritdoc/>
        public T? Get<T>(MethodCacheKey key) {
            if (this.cache.TryGetValue(key, out var cachedEntry)) {
                if (cachedEntry is MethodCacheEntry<T> entry && !entry.IsExpired) {
                    entry.UpdateLastAccessed();
                    this.statistics.RecordHit();
                    this.logger.LogDebug("Cache hit for key: {Key}", key);
                    return entry.Value;
                }
                else if (cachedEntry.IsExpired) {
                    // Remove expired entry
                    this.cache.TryRemove(key, out _);
                    this.statistics.RecordEviction();
                }
            }

            this.statistics.RecordMiss();
            this.logger.LogDebug("Cache miss for key: {Key}", key);
            return default(T);
        }

        /// <inheritdoc/>
        public void Set<T>(MethodCacheKey key, T value, TimeSpan? expiresAfter = null) {
            if (this.cache.Count >= this.configuration.MaxCacheSize && this.configuration.AutoEvictOnMaxSize) {
                this.EvictOldestEntry();
            }

            var entry = new MethodCacheEntry<T>(value, expiresAfter ?? this.configuration.DefaultExpiration);
            this.cache[key] = entry;
            this.statistics.IncrementEntryCount();

            this.logger.LogDebug("Added cache entry for key: {Key}", key);
        }

        /// <inheritdoc/>
        public bool Remove(MethodCacheKey key) {
            if (this.cache.TryRemove(key, out _)) {
                this.statistics.DecrementEntryCount();
                this.logger.LogDebug("Removed cache entry for key: {Key}", key);
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public CacheStatistics GetStatistics() {
            return this.statistics;
        }

        /// <inheritdoc/>
        public void ClearAll() {
            var count = this.cache.Count;
            this.cache.Clear();
            this.statistics.Reset();
            this.logger.LogInformation("Cleared entire method deployment cache with {Count} entries", count);
        }

        private void EvictOldestEntry() {
            var oldestKey = default(MethodCacheKey);
            var oldestTimestamp = DateTime.MaxValue;

            foreach (var entry in this.cache) {
                if (entry.Value is IMethodCacheEntryMetadata metadata && metadata.CreatedAt < oldestTimestamp) {
                    oldestKey = entry.Key;
                    oldestTimestamp = metadata.CreatedAt;
                }
            }

            if (oldestKey != null) {
                this.cache.TryRemove(oldestKey, out _);
                this.statistics.RecordEviction();
                this.statistics.DecrementEntryCount();
                this.logger?.LogDebug("Evicted oldest cache entry: {Key}", oldestKey);
            }
        }

        private Task StartPeriodicCleanup() {
            if (!this.configuration.EnablePeriodicCleanup) {
                return Task.CompletedTask;
            }

            return Task.Run(
                async () => {
                try {
                    while (!this.cleanupCancellationSource.Token.IsCancellationRequested) {
                        await Task.Delay(this.configuration.CleanupInterval, this.cleanupCancellationSource.Token);
                        this.CleanExpiredEntries();
                    }
                }
                catch (OperationCanceledException) {
                    // Expected when disposal is requested
                }
                catch (Exception ex) {
                    this.logger.LogError(ex, "Error in cache cleanup task");
                }
            }, this.cleanupCancellationSource.Token);
        }

        private void CleanExpiredEntries() {
            foreach (var key in this.cache.Keys) {
                if (this.cache.TryGetValue(key, out var entry) &&
                    entry is IMethodCacheEntryMetadata metadata &&
                    metadata.IsExpired) {
                    this.cache.TryRemove(key, out _);
                    this.statistics.RecordEviction();
                    this.statistics.DecrementEntryCount();
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose() {
            try {
                // Cancel the cleanup task
                this.cleanupCancellationSource.Cancel();

                // Wait for cleanup task to complete (with timeout to prevent hanging)
                if (!this.cleanupTask.IsCompleted) {
                    try {
                        this.cleanupTask.Wait(TimeSpan.FromSeconds(5));
                    }
                    catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException)) {
                        // Expected when cleanup task is cancelled
                    }
                    catch (Exception ex) {
                        this.logger.LogWarning(ex, "Error waiting for cleanup task to complete during disposal");
                    }
                }

                this.cleanupCancellationSource.Dispose();
                this.cache.Clear();

                this.logger.LogDebug("MethodDeploymentCache disposed successfully");
            }
            catch (Exception ex) {
                this.logger.LogError(ex, "Error during MethodDeploymentCache disposal");
            }
        }

        /// <summary>
        /// Metadata interface for cache entries to support expiration and cleanup.
        /// </summary>
        private interface IMethodCacheEntryMetadata {
            DateTime CreatedAt { get; }

            bool IsExpired { get; }
        }
    }
}
