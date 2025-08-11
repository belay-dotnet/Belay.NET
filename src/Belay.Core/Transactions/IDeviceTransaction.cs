// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Transactions {
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a transaction boundary for device operations that ensures consistency across multiple operations.
    /// </summary>
    public interface IDeviceTransaction : IDisposable {
        /// <summary>
        /// Gets the unique transaction identifier.
        /// </summary>
        string TransactionId { get; }

        /// <summary>
        /// Gets a value indicating whether the transaction is still active.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Registers a compensating action to be executed if the transaction is rolled back.
        /// </summary>
        /// <param name="compensatingAction">The action to execute on rollback.</param>
        /// <param name="description">Description of what this compensation does.</param>
        void RegisterCompensatingAction(Func<CancellationToken, Task> compensatingAction, string description);

        /// <summary>
        /// Commits all operations in this transaction.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the commit operation.</returns>
        Task CommitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Rolls back all operations in this transaction by executing compensating actions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the rollback operation.</returns>
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Implementation of device transaction using compensating actions pattern.
    /// Since we can't have true ACID transactions with external devices, we use compensating actions.
    /// </summary>
    public sealed class DeviceTransaction : IDeviceTransaction {
        private readonly List<(Func<CancellationToken, Task> Action, string Description)> compensatingActions;
        private readonly object lockObject = new object();
        private bool isActive = true;
        private bool disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceTransaction"/> class.
        /// </summary>
        public DeviceTransaction() {
            this.TransactionId = Guid.NewGuid().ToString("N")[..8]; // Short ID for logs
            this.compensatingActions = new List<(Func<CancellationToken, Task>, string)>();
        }

        /// <inheritdoc />
        public string TransactionId { get; }

        /// <inheritdoc />
        public bool IsActive {
            get {
                lock (this.lockObject) {
                    return this.isActive && !this.disposed;
                }
            }
        }

        /// <inheritdoc />
        public void RegisterCompensatingAction(Func<CancellationToken, Task> compensatingAction, string description) {
            if (compensatingAction == null) {
                throw new ArgumentNullException(nameof(compensatingAction));
            }

            if (string.IsNullOrWhiteSpace(description)) {
                throw new ArgumentException("Description cannot be null or empty", nameof(description));
            }

            lock (this.lockObject) {
                if (!this.IsActive) {
                    throw new InvalidOperationException("Cannot register compensating actions on an inactive transaction");
                }

                this.compensatingActions.Add((compensatingAction, description));
            }
        }

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default) {
            lock (this.lockObject) {
                if (!this.IsActive) {
                    throw new InvalidOperationException("Transaction is not active");
                }

                this.isActive = false;

                // On commit, we don't need to run compensating actions
                // They are only for rollback scenarios
                return Task.CompletedTask;
            }
        }

        /// <inheritdoc />
        public async Task RollbackAsync(CancellationToken cancellationToken = default) {
            List<(Func<CancellationToken, Task>, string)> actionsToRun;

            lock (this.lockObject) {
                if (!this.IsActive) {
                    return; // Already rolled back or committed
                }

                this.isActive = false;

                // Copy the actions to avoid lock contention during execution
                actionsToRun = new List<(Func<CancellationToken, Task>, string)>(this.compensatingActions);
                this.compensatingActions.Clear();
            }

            // Execute compensating actions in reverse order (LIFO)
            for (int i = actionsToRun.Count - 1; i >= 0; i--) {
                try {
                    var (action, description) = actionsToRun[i];
                    await action(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) {
                    // Log but don't throw - we want to try all compensating actions
                    // In a production system, you'd use a proper logger here
                    System.Diagnostics.Debug.WriteLine($"Failed to execute compensating action '{actionsToRun[i].Item2}': {ex.Message}");
                }
            }
        }

        /// <inheritdoc />
        public void Dispose() {
            if (this.disposed) {
                return;
            }

            // If transaction is still active when disposed, roll it back
            if (this.IsActive) {
                try {
                    this.RollbackAsync().GetAwaiter().GetResult();
                }
                catch {
                    // Suppress exceptions during disposal
                }
            }

            this.disposed = true;
        }
    }
}
