// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System;
    using System.Threading;

    /// <summary>
    /// Service for managing method execution context without stack trace inspection.
    /// This provides a secure alternative to stack frame reflection.
    /// </summary>
    public interface IExecutionContextService {
        /// <summary>
        /// Gets the current method execution context.
        /// </summary>
        IMethodExecutionContext? Current { get; }

        /// <summary>
        /// Sets the current execution context for the current async context.
        /// </summary>
        /// <param name="context">The execution context to set.</param>
        /// <returns>A disposable that restores the previous context when disposed.</returns>
        IDisposable SetContext(IMethodExecutionContext context);

        /// <summary>
        /// Clears the current execution context.
        /// </summary>
        void ClearContext();
    }

    /// <summary>
    /// Thread-safe implementation of execution context service using AsyncLocal.
    /// </summary>
    public sealed class ExecutionContextService : IExecutionContextService {
        private readonly AsyncLocal<IMethodExecutionContext?> currentContext = new();

        /// <inheritdoc />
        public IMethodExecutionContext? Current => this.currentContext.Value;

        /// <inheritdoc />
        public IDisposable SetContext(IMethodExecutionContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            var previousContext = this.currentContext.Value;
            this.currentContext.Value = context;

            return new ExecutionContextScope(this, previousContext);
        }

        /// <inheritdoc />
        public void ClearContext() {
            this.currentContext.Value = null;
        }

        /// <summary>
        /// Internal method to restore previous context.
        /// </summary>
        /// <param name="previousContext">The previous context to restore.</param>
        internal void RestoreContext(IMethodExecutionContext? previousContext) {
            this.currentContext.Value = previousContext;
        }
    }

    /// <summary>
    /// Disposable scope for execution context management.
    /// </summary>
    internal sealed class ExecutionContextScope : IDisposable {
        private readonly ExecutionContextService service;
        private readonly IMethodExecutionContext? previousContext;
        private bool disposed = false;

        internal ExecutionContextScope(ExecutionContextService service, IMethodExecutionContext? previousContext) {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.previousContext = previousContext;
        }

        /// <inheritdoc/>
        public void Dispose() {
            if (!this.disposed) {
                this.service.RestoreContext(this.previousContext);
                this.disposed = true;
            }
        }
    }
}
