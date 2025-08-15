// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

/// <summary>
/// Represents the type of error that occurred during code execution.
/// Provides enhanced error classification for better diagnostics and handling.
/// </summary>
public enum ExecutionErrorType
{
    /// <summary>
    /// No error occurred - execution was successful.
    /// </summary>
    None,
    
    /// <summary>
    /// Python syntax error (SyntaxError, IndentationError, etc.).
    /// Indicates malformed Python code that could not be parsed.
    /// </summary>
    SyntaxError,
    
    /// <summary>
    /// Runtime error during execution (NameError, TypeError, ValueError, etc.).
    /// Code was syntactically correct but failed during execution.
    /// </summary>
    RuntimeError,
    
    /// <summary>
    /// Memory-related error (MemoryError, OSError with memory indication).
    /// Device ran out of memory during execution.
    /// </summary>
    MemoryError,
    
    /// <summary>
    /// File system or I/O error (OSError, IOError, ENOENT, etc.).
    /// Issues with file operations or device storage.
    /// </summary>
    FileSystemError,
    
    /// <summary>
    /// Import or module error (ImportError, ModuleNotFoundError).
    /// Required modules or libraries are not available on the device.
    /// </summary>
    ImportError,
    
    /// <summary>
    /// Communication timeout or device unresponsive.
    /// Network or serial communication issues.
    /// </summary>
    TimeoutError,
    
    /// <summary>
    /// Device-specific error or hardware issue.
    /// Problems with device capabilities or hardware failures.
    /// </summary>
    DeviceError,
    
    /// <summary>
    /// Execution was interrupted or cancelled.
    /// Operation was stopped before completion.
    /// </summary>
    InterruptedError,
    
    /// <summary>
    /// Unknown or unclassified error.
    /// Error pattern not recognized by the classifier.
    /// </summary>
    UnknownError
}

/// <summary>
/// Enhanced execution result with detailed error classification and diagnostics.
/// </summary>
public class EnhancedExecutionResult
{
    /// <summary>
    /// Gets or sets the type of error that occurred during execution.
    /// </summary>
    public ExecutionErrorType ErrorType { get; set; } = ExecutionErrorType.None;
    
    /// <summary>
    /// Gets or sets a value indicating whether the execution was successful.
    /// </summary>
    public bool IsSuccess => this.ErrorType == ExecutionErrorType.None;
    
    /// <summary>
    /// Gets or sets the normal output from the execution.
    /// </summary>
    public string Output { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the error output from the execution.
    /// </summary>
    public string ErrorOutput { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the original exception if one occurred.
    /// </summary>
    public Exception? Exception { get; set; }
    
    /// <summary>
    /// Gets or sets additional diagnostic information about the error.
    /// </summary>
    public string? DiagnosticInfo { get; set; }
    
    /// <summary>
    /// Gets or sets suggested actions for resolving the error.
    /// </summary>
    public string? SuggestedAction { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether the error is recoverable.
    /// </summary>
    public bool IsRecoverable { get; set; } = true;
}