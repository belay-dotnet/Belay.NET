// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    using System.Collections.Concurrent;

    /// <summary>
    /// Thread-safe implementation of session state management.
    /// </summary>
    public sealed class SessionState : ISessionState {
        private readonly ConcurrentDictionary<string, object?> state = new();

        /// <inheritdoc />
        public T Get<T>(string key, T defaultValue = default!) {
            if (string.IsNullOrWhiteSpace(key)) {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            return this.state.TryGetValue(key, out var value) && value is T typedValue
                ? typedValue
                : defaultValue;
        }

        /// <inheritdoc />
        public void Set<T>(string key, T value) {
            if (string.IsNullOrWhiteSpace(key)) {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            this.state.AddOrUpdate(key, value, (k, v) => value);
        }

        /// <inheritdoc />
        public bool TryGet<T>(string key, out T value) {
            if (string.IsNullOrWhiteSpace(key)) {
                value = default!;
                return false;
            }

            if (this.state.TryGetValue(key, out var obj) && obj is T typedValue) {
                value = typedValue;
                return true;
            }

            value = default!;
            return false;
        }

        /// <inheritdoc />
        public bool Remove(string key) {
            if (string.IsNullOrWhiteSpace(key)) {
                return false;
            }

            return this.state.TryRemove(key, out _);
        }

        /// <inheritdoc />
        public bool ContainsKey(string key) {
            if (string.IsNullOrWhiteSpace(key)) {
                return false;
            }

            return this.state.ContainsKey(key);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<string> Keys => this.state.Keys.ToArray();

        /// <inheritdoc />
        public void Clear() {
            this.state.Clear();
        }
    }
}
