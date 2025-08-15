// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Belay.Core.Tests;

/// <summary>
/// Unit tests for the ExecutionErrorParser class.
/// </summary>
public class ExecutionErrorParserTests
{
    private readonly ILogger logger = NullLogger.Instance;

    [Fact]
    public void ParseExecutionResult_NoError_ReturnsSuccess()
    {
        // Arrange
        var normalOutput = "Hello, World!";
        var errorOutput = "";

        // Act
        var result = ExecutionErrorParser.ParseExecutionResult(normalOutput, errorOutput, null, this.logger);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ExecutionErrorType.None, result.ErrorType);
        Assert.Equal("Hello, World!", result.Output);
        Assert.Empty(result.ErrorOutput);
        Assert.True(result.IsRecoverable);
    }

    [Theory]
    [InlineData("SyntaxError: invalid syntax", ExecutionErrorType.SyntaxError)]
    [InlineData("IndentationError: expected an indented block", ExecutionErrorType.SyntaxError)]
    [InlineData("TabError: inconsistent use of tabs and spaces", ExecutionErrorType.SyntaxError)]
    public void ParseExecutionResult_SyntaxErrors_ClassifiesCorrectly(string errorOutput, ExecutionErrorType expectedType)
    {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput, null, this.logger);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedType, result.ErrorType);
        Assert.Contains("syntax", result.SuggestedAction, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.IsRecoverable);
    }

    [Theory]
    [InlineData("NameError: name 'undefined_var' is not defined", ExecutionErrorType.RuntimeError)]
    [InlineData("TypeError: unsupported operand type(s)", ExecutionErrorType.RuntimeError)]
    [InlineData("ValueError: invalid literal for int()", ExecutionErrorType.RuntimeError)]
    [InlineData("AttributeError: 'str' object has no attribute 'nonexistent'", ExecutionErrorType.RuntimeError)]
    [InlineData("KeyError: 'missing_key'", ExecutionErrorType.RuntimeError)]
    [InlineData("IndexError: list index out of range", ExecutionErrorType.RuntimeError)]
    [InlineData("ZeroDivisionError: division by zero", ExecutionErrorType.RuntimeError)]
    public void ParseExecutionResult_RuntimeErrors_ClassifiesCorrectly(string errorOutput, ExecutionErrorType expectedType)
    {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput, null, this.logger);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedType, result.ErrorType);
        Assert.Contains("variable", result.SuggestedAction, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.IsRecoverable);
    }

    [Theory]
    [InlineData("MemoryError: memory allocation failed", ExecutionErrorType.MemoryError)]
    [InlineData("OSError: [Errno 12] out of memory", ExecutionErrorType.MemoryError)]
    [InlineData("OSError: Cannot allocate memory", ExecutionErrorType.MemoryError)]
    public void ParseExecutionResult_MemoryErrors_ClassifiesCorrectly(string errorOutput, ExecutionErrorType expectedType)
    {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput, null, this.logger);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedType, result.ErrorType);
        Assert.Contains("memory", result.SuggestedAction, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.IsRecoverable); // Memory errors usually require restart
    }

    [Theory]
    [InlineData("OSError: [Errno 2] ENOENT", ExecutionErrorType.FileSystemError)]
    [InlineData("OSError: [Errno 13] EACCES", ExecutionErrorType.FileSystemError)]
    [InlineData("OSError: [Errno 28] ENOSPC", ExecutionErrorType.FileSystemError)]
    [InlineData("FileNotFoundError: [Errno 2] No such file or directory", ExecutionErrorType.FileSystemError)]
    [InlineData("PermissionError: [Errno 13] Permission denied", ExecutionErrorType.FileSystemError)]
    public void ParseExecutionResult_FileSystemErrors_ClassifiesCorrectly(string errorOutput, ExecutionErrorType expectedType)
    {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput, null, this.logger);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedType, result.ErrorType);
        Assert.Contains("file", result.SuggestedAction, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.IsRecoverable); // File system errors often permanent
    }

    [Theory]
    [InlineData("ImportError: No module named 'requests'", ExecutionErrorType.ImportError)]
    [InlineData("ModuleNotFoundError: No module named 'numpy'", ExecutionErrorType.ImportError)]
    [InlineData("ImportError: cannot import name 'function'", ExecutionErrorType.ImportError)]
    public void ParseExecutionResult_ImportErrors_ClassifiesCorrectly(string errorOutput, ExecutionErrorType expectedType)
    {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput, null, this.logger);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedType, result.ErrorType);
        Assert.Contains("module", result.SuggestedAction, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.IsRecoverable);
    }

    [Theory]
    [InlineData("KeyboardInterrupt", ExecutionErrorType.InterruptedError)]
    [InlineData("SystemExit: 0", ExecutionErrorType.InterruptedError)]
    [InlineData("Operation cancelled", ExecutionErrorType.InterruptedError)]
    public void ParseExecutionResult_InterruptedErrors_ClassifiesCorrectly(string errorOutput, ExecutionErrorType expectedType)
    {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput, null, this.logger);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedType, result.ErrorType);
        Assert.True(result.IsRecoverable);
    }

    [Theory]
    [InlineData("timeout occurred", ExecutionErrorType.TimeoutError)]
    [InlineData("Connection timed out", ExecutionErrorType.TimeoutError)]
    [InlineData("Operation TIMEOUT", ExecutionErrorType.TimeoutError)]
    public void ParseExecutionResult_TimeoutErrors_ClassifiesCorrectly(string errorOutput, ExecutionErrorType expectedType)
    {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput, null, this.logger);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedType, result.ErrorType);
        Assert.Contains("timeout", result.SuggestedAction, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.IsRecoverable);
    }

    [Fact]
    public void ParseExecutionResult_ErrorInNormalOutput_DetectsError()
    {
        // Arrange - Sometimes errors appear in normal output
        var normalOutput = "Traceback (most recent call last):\n  File \"<stdin>\", line 1\nNameError: name 'x' is not defined";
        var errorOutput = "";

        // Act
        var result = ExecutionErrorParser.ParseExecutionResult(normalOutput, errorOutput, null, this.logger);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorType.RuntimeError, result.ErrorType);
        Assert.NotNull(result.DiagnosticInfo);
    }

    [Fact]
    public void ParseExecutionResult_UnknownError_ClassifiesAsUnknown()
    {
        // Arrange
        var errorOutput = "SomeUnknownError: This is not a recognized pattern";

        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput, null, this.logger);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorType.UnknownError, result.ErrorType);
        Assert.True(result.IsRecoverable); // Assume recoverable unless proven otherwise
    }

    [Fact]
    public void ParseExecutionResult_WithException_IncludesException()
    {
        // Arrange
        var originalException = new InvalidOperationException("Test exception");
        var errorOutput = "SyntaxError: invalid syntax";

        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput, originalException, this.logger);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorType.SyntaxError, result.ErrorType);
        Assert.Equal(originalException, result.Exception);
    }

    [Fact]
    public void ParseExecutionResult_ComplexTraceback_ExtractsDiagnosticInfo()
    {
        // Arrange
        var errorOutput = @"Traceback (most recent call last):
  File ""<stdin>"", line 2, in <module>
  File ""<stdin>"", line 1, in test_function
NameError: name 'undefined_variable' is not defined";

        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput, null, this.logger);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorType.RuntimeError, result.ErrorType);
        Assert.NotNull(result.DiagnosticInfo);
        Assert.Contains("undefined_variable", result.DiagnosticInfo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ParseExecutionResult_EmptyErrorOutput_ReturnsSuccess(string errorOutput)
    {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("Valid output", errorOutput, null, this.logger);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(ExecutionErrorType.None, result.ErrorType);
    }
}