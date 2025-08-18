// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

using System.Collections.Concurrent;
using System.Linq;

/// <summary>
/// Simple, efficient caching implementation that replaces the complex caching infrastructure.
/// Uses basic memoization suitable for MicroPython device operations.
/// </summary>
public static class SimpleCache {
    private static readonly ConcurrentDictionary<string, object> Cache = new();
    private const int MaxCacheEntries = 1000;

    /// <summary>
    /// Gets a cached value or creates it using the factory function.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">The factory function to create the value if not cached.</param>
    /// <returns>The cached or newly created value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key or factory is null.</exception>
    public static T GetOrCreate<T>(string key, Func<T> factory) {
        if (key == null) {
            throw new ArgumentNullException(nameof(key));
        }

        if (factory == null) {
            throw new ArgumentNullException(nameof(factory));
        }

        var typedKey = $"{typeof(T).FullName}::{key}";
        EnforceSizeLimit();
        return (T)Cache.GetOrAdd(typedKey, _ => factory()!);
    }

    /// <summary>
    /// Gets a cached value or creates it asynchronously using the factory function.
    /// Thread-safe implementation prevents multiple concurrent factory executions.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">The async factory function to create the value if not cached.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The cached or newly created value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when key or factory is null.</exception>
    public static async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CancellationToken cancellationToken = default) {
        if (key == null) {
            throw new ArgumentNullException(nameof(key));
        }

        if (factory == null) {
            throw new ArgumentNullException(nameof(factory));
        }

        var typedKey = $"{typeof(T).FullName}::{key}";
        EnforceSizeLimit();

        // Use GetOrAdd with lazy evaluation to ensure single factory execution
        var lazy = (Lazy<Task<T>>)Cache.GetOrAdd(
            typedKey,
            _ => new Lazy<Task<T>>(() => factory(), LazyThreadSafetyMode.ExecutionAndPublication));

        return await lazy.Value.ConfigureAwait(false);
    }

    /// <summary>
    /// Clears all cached values.
    /// </summary>
    public static void Clear() => Cache.Clear();

    /// <summary>
    /// Removes a specific cached value.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /// <returns>True if the value was removed, false if it didn't exist.</returns>
    public static bool Remove(string key) => Cache.TryRemove(key, out _);

    /// <summary>
    /// Gets the current number of cached items.
    /// </summary>
    public static int Count => Cache.Count;

    /// <summary>
    /// Checks if a key exists in the cache.
    /// </summary>
    /// <param name="key">The cache key to check.</param>
    /// <returns>True if the key exists in the cache.</returns>
    public static bool ContainsKey(string key) => Cache.ContainsKey(key);

    /// <summary>
    /// Enforces the maximum cache size limit by removing entries when needed.
    /// </summary>
    private static void EnforceSizeLimit() {
        while (Cache.Count >= MaxCacheEntries) {
            // Remove entries until we're under the limit
            // Simple FIFO eviction - remove first found entry
            var firstKey = Cache.Keys.FirstOrDefault(); // Cannot use Array.Find on Keys collection
            if (firstKey != null) {
                Cache.TryRemove(firstKey, out _);
            }
            else {
                break; // Shouldn't happen, but prevent infinite loop
            }
        }
    }
}
