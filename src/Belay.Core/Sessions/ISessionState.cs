// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    /// <summary>
    /// Provides session-scoped state management for device operations.
    /// </summary>
    public interface ISessionState {
        /// <summary>
        /// Gets a value from the session state.
        /// </summary>
        /// <typeparam name="T">The type of the value to retrieve.</typeparam>
        /// <param name="key">The key of the value to retrieve.</param>
        /// <param name="defaultValue">The default value to return if the key is not found.</param>
        /// <returns>The value associated with the key, or the default value if not found.</returns>
        T Get<T>(string key, T defaultValue = default!);

        /// <summary>
        /// Sets a value in the session state.
        /// </summary>
        /// <typeparam name="T">The type of the value to store.</typeparam>
        /// <param name="key">The key to associate with the value.</param>
        /// <param name="value">The value to store.</param>
        void Set<T>(string key, T value);

        /// <summary>
        /// Attempts to get a value from the session state.
        /// </summary>
        /// <typeparam name="T">The type of the value to retrieve.</typeparam>
        /// <param name="key">The key of the value to retrieve.</param>
        /// <param name="value">When this method returns, contains the value associated with the key, if found.</param>
        /// <returns>True if the key was found; otherwise, false.</returns>
        bool TryGet<T>(string key, out T value);

        /// <summary>
        /// Removes a value from the session state.
        /// </summary>
        /// <param name="key">The key of the value to remove.</param>
        /// <returns>True if the key was found and removed; otherwise, false.</returns>
        bool Remove(string key);

        /// <summary>
        /// Checks whether the session state contains the specified key.
        /// </summary>
        /// <param name="key">The key to check for.</param>
        /// <returns>True if the key exists; otherwise, false.</returns>
        bool ContainsKey(string key);

        /// <summary>
        /// Gets all keys in the session state.
        /// </summary>
        /// <returns>A collection of all keys in the session state.</returns>
        IReadOnlyCollection<string> Keys { get; }

        /// <summary>
        /// Clears all values from the session state.
        /// </summary>
        void Clear();
    }
}
