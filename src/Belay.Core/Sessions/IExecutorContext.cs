// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Sessions {
    /// <summary>
    /// Provides session context for executor coordination and state sharing.
    /// </summary>
    public interface IExecutorContext {
        /// <summary>
        /// Gets the session identifier associated with this context.
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// Registers an executor with the session for coordination.
        /// </summary>
        /// <param name="executorType">The type of executor being registered.</param>
        /// <param name="executorInstance">The executor instance being registered.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task RegisterExecutorAsync(Type executorType, object executorInstance);

        /// <summary>
        /// Unregisters an executor from the session.
        /// </summary>
        /// <param name="executorType">The type of executor being unregistered.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task UnregisterExecutorAsync(Type executorType);

        /// <summary>
        /// Gets a registered executor of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of executor to retrieve.</typeparam>
        /// <returns>The registered executor instance, or null if not found.</returns>
        T? GetExecutor<T>()
            where T : class;

        /// <summary>
        /// Checks whether an executor of the specified type is registered.
        /// </summary>
        /// <param name="executorType">The type of executor to check for.</param>
        /// <returns>True if an executor of the specified type is registered; otherwise, false.</returns>
        bool IsExecutorRegistered(Type executorType);

        /// <summary>
        /// Gets all registered executor types.
        /// </summary>
        /// <returns>A collection of registered executor types.</returns>
        IReadOnlyCollection<Type> RegisteredExecutorTypes { get; }

        /// <summary>
        /// Gets shared data between executors within this session.
        /// </summary>
        /// <typeparam name="T">The type of shared data to retrieve.</typeparam>
        /// <param name="key">The key of the shared data.</param>
        /// <param name="defaultValue">The default value to return if not found.</param>
        /// <returns>The shared data value, or the default value if not found.</returns>
        T GetSharedData<T>(string key, T defaultValue = default!);

        /// <summary>
        /// Sets shared data between executors within this session.
        /// </summary>
        /// <typeparam name="T">The type of shared data to store.</typeparam>
        /// <param name="key">The key to associate with the shared data.</param>
        /// <param name="value">The shared data value to store.</param>
        void SetSharedData<T>(string key, T value);
    }
}
