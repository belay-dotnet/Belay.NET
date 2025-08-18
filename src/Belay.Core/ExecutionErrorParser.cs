// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Parses execution errors and classifies them for enhanced error handling and diagnostics.
/// Provides intelligent error pattern recognition and suggested remediation actions.
/// </summary>
internal static class ExecutionErrorParser {
    // Error pattern definitions for comprehensive classification
    private static readonly Dictionary<ExecutionErrorType, ErrorPattern[]> ErrorPatterns = new() {
        [ExecutionErrorType.SyntaxError] = new[]
        {
            new ErrorPattern(@"SyntaxError:", "Python syntax error detected"),
            new ErrorPattern(@"IndentationError:", "Incorrect indentation in Python code"),
            new ErrorPattern(@"TabError:", "Inconsistent use of tabs and spaces"),
            new ErrorPattern(@"invalid syntax", "Invalid Python syntax"),
        },

        [ExecutionErrorType.RuntimeError] = new[]
        {
            new ErrorPattern(@"NameError:", "Variable or function not defined"),
            new ErrorPattern(@"TypeError:", "Type-related error during execution"),
            new ErrorPattern(@"ValueError:", "Invalid value passed to function"),
            new ErrorPattern(@"AttributeError:", "Object does not have the specified attribute"),
            new ErrorPattern(@"KeyError:", "Dictionary key not found"),
            new ErrorPattern(@"IndexError:", "List index out of range"),
            new ErrorPattern(@"ZeroDivisionError:", "Division by zero"),
        },

        [ExecutionErrorType.MemoryError] = new[]
        {
            new ErrorPattern(@"MemoryError", "Device out of memory"),
            new ErrorPattern(@"OSError:.*memory", "Operating system memory error"),
            new ErrorPattern(@"OSError: \[Errno 12\]", "Cannot allocate memory"),
            new ErrorPattern(@"out of memory", "Insufficient memory available"),
        },

        [ExecutionErrorType.FileSystemError] = new[]
        {
            new ErrorPattern(@"OSError:.*ENOENT", "File or directory not found"),
            new ErrorPattern(@"OSError:.*EACCES", "Permission denied"),
            new ErrorPattern(@"OSError:.*ENOSPC", "No space left on device"),
            new ErrorPattern(@"OSError:.*EROFS", "Read-only file system"),
            new ErrorPattern(@"IOError:", "Input/output error"),
            new ErrorPattern(@"FileNotFoundError:", "File not found"),
            new ErrorPattern(@"PermissionError:", "Permission denied"),
        },

        [ExecutionErrorType.ImportError] = new[]
        {
            new ErrorPattern(@"ImportError:", "Module import failed"),
            new ErrorPattern(@"ModuleNotFoundError:", "Module not found on device"),
            new ErrorPattern(@"No module named", "Required module not available"),
        },

        [ExecutionErrorType.DeviceError] = new[]
        {
            new ErrorPattern(@"OSError:.*ENODEV", "Device not available"),
            new ErrorPattern(@"OSError:.*EIO", "Input/output error"),
            new ErrorPattern(@"OSError:.*EPIPE", "Broken pipe"),
            new ErrorPattern(@"OSError:.*ECONNRESET", "Connection reset"),
        },

        [ExecutionErrorType.InterruptedError] = new[]
        {
            new ErrorPattern(@"KeyboardInterrupt", "Execution interrupted"),
            new ErrorPattern(@"SystemExit", "System exit called"),
            new ErrorPattern(@"cancelled", "Operation cancelled"),
        },
    };

    // Suggested actions for different error types
    private static readonly Dictionary<ExecutionErrorType, string> SuggestedActions = new() {
        [ExecutionErrorType.SyntaxError] = "Check Python syntax, indentation, and parentheses/brackets matching",
        [ExecutionErrorType.RuntimeError] = "Verify variable names, function calls, and data types are correct",
        [ExecutionErrorType.MemoryError] = "Reduce memory usage by freeing unused objects or split operation into smaller chunks",
        [ExecutionErrorType.FileSystemError] = "Check file paths, permissions, and available storage space",
        [ExecutionErrorType.ImportError] = "Ensure required modules are available on the MicroPython device",
        [ExecutionErrorType.TimeoutError] = "Check device connection and increase timeout if necessary",
        [ExecutionErrorType.DeviceError] = "Verify device connection and try reconnecting",
        [ExecutionErrorType.InterruptedError] = "Operation was cancelled - retry if needed",
        [ExecutionErrorType.UnknownError] = "Check error details and device logs for more information",
    };

    /// <summary>
    /// Parses execution output and classifies any errors found.
    /// </summary>
    /// <param name="normalOutput">The normal output from execution.</param>
    /// <param name="errorOutput">The error output from execution.</param>
    /// <param name="originalException">The original exception if one occurred.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <returns>Enhanced execution result with error classification.</returns>
    public static EnhancedExecutionResult ParseExecutionResult(
        string normalOutput,
        string errorOutput,
        Exception? originalException = null,
        ILogger? logger = null) {
        normalOutput ??= string.Empty;
        errorOutput ??= string.Empty;

        var result = new EnhancedExecutionResult {
            Output = normalOutput.Trim(),
            ErrorOutput = errorOutput.Trim(),
            Exception = originalException,
        };

        // Check for errors in both normal and error output
        var combinedOutput = $"{normalOutput}\n{errorOutput}";

        // First, check if there's actually an error
        if (string.IsNullOrWhiteSpace(errorOutput) &&
            !ContainsErrorIndicators(normalOutput)) {
            result.ErrorType = ExecutionErrorType.None;
            return result;
        }

        // Classify the error type
        result.ErrorType = ClassifyError(combinedOutput, logger);

        // Add diagnostic information
        result.DiagnosticInfo = ExtractDiagnosticInfo(combinedOutput, result.ErrorType);

        // Add suggested action
        if (SuggestedActions.TryGetValue(result.ErrorType, out var suggestion)) {
            result.SuggestedAction = suggestion;
        }

        // Determine if the error is recoverable
        result.IsRecoverable = IsRecoverableError(result.ErrorType);

        logger?.LogDebug(
            "Classified execution error as {ErrorType}: {DiagnosticInfo}",
            result.ErrorType, result.DiagnosticInfo);

        return result;
    }

    private static ExecutionErrorType ClassifyError(string output, ILogger? logger) {
        foreach (var (errorType, patterns) in ErrorPatterns) {
            var matchedPattern = Array.Find(patterns, pattern => pattern.Regex.IsMatch(output));
            if (matchedPattern != null) {
                logger?.LogTrace(
                    "Matched error pattern '{Pattern}' for type {ErrorType}",
                    matchedPattern.Pattern, errorType);
                return errorType;
            }
        }

        // Check for timeout indicators
        if (output.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("timed out", StringComparison.OrdinalIgnoreCase)) {
            return ExecutionErrorType.TimeoutError;
        }

        return ExecutionErrorType.UnknownError;
    }

    private static bool ContainsErrorIndicators(string output) {
        // Use more precise patterns to avoid false positives from legitimate output
        // Only check for specific error patterns that indicate actual Python exceptions
        var preciseErrorPatterns = new[]
        {
            // Python traceback indicators (at start of line or after whitespace)
            @"(?:^|\s)Traceback\s*\(most recent call last\)",
            @"(?:^|\s)Traceback:",

            // Python exception patterns (end with colon followed by error message)
            @"\w*Error:\s",
            @"\w*Exception:\s",

            // System level errors
            @"(?:^|\s)Fatal:",
            @"(?:^|\s)Critical:",

            // MicroPython specific error patterns
            @"MemoryError:",
            @"OSError:",
            @"ImportError:",
            @"KeyboardInterrupt",
        };

        return Array.Exists(preciseErrorPatterns, pattern =>
            Regex.IsMatch(output, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline));
    }

    private static string ExtractDiagnosticInfo(string output, ExecutionErrorType errorType) {
        // Extract relevant lines from the error output
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // For syntax errors, find the specific syntax issue
        if (errorType == ExecutionErrorType.SyntaxError) {
            var syntaxLine = Array.Find(lines, line =>
                line.Contains("SyntaxError") || line.Contains("IndentationError"));
            if (syntaxLine != null) {
                return syntaxLine.Trim();
            }
        }

        // For runtime errors, extract the exception message
        if (errorType == ExecutionErrorType.RuntimeError) {
            string? errorLine = null;
            for (int i = lines.Length - 1; i >= 0; i--) {
                if (lines[i].Contains("Error:") && !lines[i].StartsWith("Traceback")) {
                    errorLine = lines[i];
                    break;
                }
            }
            if (errorLine != null) {
                return errorLine.Trim();
            }
        }

        // For file system errors, extract the OS error details
        if (errorType == ExecutionErrorType.FileSystemError) {
            var osErrorLine = Array.Find(lines, line => line.Contains("OSError"));
            if (osErrorLine != null) {
                return osErrorLine.Trim();
            }
        }

        // Default: return first non-empty error line
        var firstErrorLine = Array.Find(lines, line =>
            !string.IsNullOrWhiteSpace(line) &&
            !line.Trim().Equals(">>>") &&
            !line.StartsWith("Type "));

        return firstErrorLine?.Trim() ?? "Error details not available";
    }

    private static bool IsRecoverableError(ExecutionErrorType errorType) {
        return errorType switch {
            ExecutionErrorType.None => true,
            ExecutionErrorType.SyntaxError => true,  // Can fix syntax and retry
            ExecutionErrorType.RuntimeError => true, // Can fix code and retry
            ExecutionErrorType.ImportError => true,  // Can install modules
            ExecutionErrorType.TimeoutError => true, // Can retry with longer timeout
            ExecutionErrorType.InterruptedError => true, // Can retry
            ExecutionErrorType.MemoryError => false, // Usually requires device restart
            ExecutionErrorType.DeviceError => false, // Hardware/connection issue
            ExecutionErrorType.FileSystemError => false, // Often permanent issues
            ExecutionErrorType.UnknownError => true, // Assume recoverable unless proven otherwise
            _ => true,
        };
    }

    /// <summary>
    /// Represents an error pattern for classification.
    /// </summary>
    private sealed class ErrorPattern {
        public string Pattern { get; }

        public Regex Regex { get; }

        public string Description { get; }

        public ErrorPattern(string pattern, string description) {
            this.Pattern = pattern;
            this.Description = description;
            this.Regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }
}
