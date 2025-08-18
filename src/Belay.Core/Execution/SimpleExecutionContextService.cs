// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution;

/// <summary>
/// Simple execution context service implementation.
/// Minimal implementation that provides no-op context management.
/// </summary>
public class SimpleExecutionContextService : IExecutionContextService {
    /// <inheritdoc/>
    public IDisposable SetContext(MethodExecutionContext context) {
        // Return a no-op disposable for simplified implementation
        return new NoOpDisposable();
    }

    private sealed class NoOpDisposable : IDisposable {
        public void Dispose() {
            // No-op implementation
        }
    }
}
