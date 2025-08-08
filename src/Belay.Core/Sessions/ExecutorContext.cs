// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Provides session context for executor coordination and state sharing.
    /// </summary>
    public sealed class ExecutorContext : IExecutorContext {
        private readonly ILogger<ExecutorContext> logger;
        private readonly ConcurrentDictionary<Type, object> registeredExecutors = new();
        private readonly ConcurrentDictionary<string, object?> sharedData = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutorContext"/> class.
        /// </summary>
        /// <param name="sessionId">The identifier of the session this context belongs to.</param>
        /// <param name="logger">The logger for diagnostic information.</param>
        public ExecutorContext(string sessionId, ILogger<ExecutorContext> logger) {
            this.SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public string SessionId { get; }

        /// <inheritdoc />
        public Task RegisterExecutorAsync(Type executorType, object executorInstance) {
            if (executorType == null) {
                throw new ArgumentNullException(nameof(executorType));
            }

            if (executorInstance == null) {
                throw new ArgumentNullException(nameof(executorInstance));
            }

            if (!executorType.IsInstanceOfType(executorInstance)) {
                throw new ArgumentException(
                    $"Executor instance is not of type {executorType.Name}",
                    nameof(executorInstance));
            }

            this.registeredExecutors.AddOrUpdate(executorType, executorInstance, (key, existing) => executorInstance);

            this.logger.LogDebug(
                "Registered executor {ExecutorType} in session {SessionId}",
                executorType.Name,
                this.SessionId);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task UnregisterExecutorAsync(Type executorType) {
            if (executorType == null) {
                throw new ArgumentNullException(nameof(executorType));
            }

            if (this.registeredExecutors.TryRemove(executorType, out _)) {
                this.logger.LogDebug(
                    "Unregistered executor {ExecutorType} from session {SessionId}",
                    executorType.Name,
                    this.SessionId);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public T? GetExecutor<T>()
            where T : class {
            var executorType = typeof(T);
            return this.registeredExecutors.TryGetValue(executorType, out var executor)
                ? executor as T
                : null;
        }

        /// <inheritdoc />
        public bool IsExecutorRegistered(Type executorType) {
            if (executorType == null) {
                throw new ArgumentNullException(nameof(executorType));
            }

            return this.registeredExecutors.ContainsKey(executorType);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<Type> RegisteredExecutorTypes =>
            this.registeredExecutors.Keys.ToArray();

        /// <inheritdoc />
        public T GetSharedData<T>(string key, T defaultValue = default!) {
            if (string.IsNullOrWhiteSpace(key)) {
                return defaultValue;
            }

            return this.sharedData.TryGetValue(key, out var value) && value is T typedValue
                ? typedValue
                : defaultValue;
        }

        /// <inheritdoc />
        public void SetSharedData<T>(string key, T value) {
            if (string.IsNullOrWhiteSpace(key)) {
                throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
            }

            this.sharedData.AddOrUpdate(key, value, (k, v) => value);
        }
    }
}
