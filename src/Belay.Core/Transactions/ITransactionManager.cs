// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Transactions {
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Service for managing device operation transactions.
    /// </summary>
    public interface ITransactionManager {
        /// <summary>
        /// Executes a function within a transaction boundary with automatic rollback on failure.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="operation">The operation to execute within the transaction.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The result of the operation.</returns>
        Task<T> ExecuteInTransactionAsync<T>(Func<IDeviceTransaction, Task<T>> operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an action within a transaction boundary with automatic rollback on failure.
        /// </summary>
        /// <param name="operation">The operation to execute within the transaction.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the operation.</returns>
        Task ExecuteInTransactionAsync(Func<IDeviceTransaction, Task> operation, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Transaction manager implementation for device operations.
    /// </summary>
    public sealed class TransactionManager : ITransactionManager {
        private readonly ILogger<TransactionManager> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionManager"/> class.
        /// </summary>
        /// <param name="logger">Logger for transaction operations.</param>
        public TransactionManager(ILogger<TransactionManager> logger) {
            this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TransactionManager>.Instance;
        }

        /// <inheritdoc />
        public async Task<T> ExecuteInTransactionAsync<T>(Func<IDeviceTransaction, Task<T>> operation, CancellationToken cancellationToken = default) {
            if (operation == null) {
                throw new ArgumentNullException(nameof(operation));
            }

            using var transaction = new DeviceTransaction();
            this.logger.LogDebug("Starting transaction {TransactionId}", transaction.TransactionId);

            try {
                var result = await operation(transaction).ConfigureAwait(false);
                
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                this.logger.LogDebug("Transaction {TransactionId} committed successfully", transaction.TransactionId);
                
                return result;
            }
            catch (Exception ex) {
                this.logger.LogWarning(ex, "Transaction {TransactionId} failed, initiating rollback", transaction.TransactionId);
                
                try {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    this.logger.LogDebug("Transaction {TransactionId} rolled back successfully", transaction.TransactionId);
                }
                catch (Exception rollbackEx) {
                    this.logger.LogError(rollbackEx, "Failed to rollback transaction {TransactionId}", transaction.TransactionId);
                    // Don't mask the original exception
                }
                
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ExecuteInTransactionAsync(Func<IDeviceTransaction, Task> operation, CancellationToken cancellationToken = default) {
            await ExecuteInTransactionAsync(async transaction => {
                await operation(transaction).ConfigureAwait(false);
                return (object?)null;
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}