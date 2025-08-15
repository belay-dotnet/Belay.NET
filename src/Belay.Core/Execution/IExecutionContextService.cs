// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution;

/// <summary>
/// Simple execution context service interface.
/// Minimal stub to replace the sophisticated execution context system.
/// </summary>
public interface IExecutionContextService {
    /// <summary>
    /// Sets the execution context for the current operation.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <returns>A disposable scope for the context.</returns>
    IDisposable SetContext(MethodExecutionContext context);
}
