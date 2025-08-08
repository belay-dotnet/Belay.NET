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
using System.Threading;
using System.Threading.Tasks;

namespace Belay.Core.Caching
{
    /// <summary>
    /// Defines an interface for persistent cache storage in Belay.NET.
    /// </summary>
    /// <remarks>
    /// This is a placeholder interface for future persistent cache storage implementations.
    /// Future implementations may include file-system, database, or distributed cache backends.
    /// </remarks>
    public interface IPersistentCacheStorage
    {
        /// <summary>
        /// Reads a cached value from persistent storage.
        /// </summary>
        /// <typeparam name="T">The type of the cached value</typeparam>
        /// <param name="key">The cache key</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <returns>The cached value, or default if not found</returns>
        Task<T> ReadAsync<T>(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes a value to persistent storage.
        /// </summary>
        /// <typeparam name="T">The type of the value to cache</typeparam>
        /// <param name="key">The cache key</param>
        /// <param name="value">The value to store</param>
        /// <param name="expiration">Optional expiration time</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        Task WriteAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a specific entry from persistent storage.
        /// </summary>
        /// <param name="key">The cache key to remove</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        Task DeleteAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all entries from persistent storage.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        Task ClearAllAsync(CancellationToken cancellationToken = default);
    }
}