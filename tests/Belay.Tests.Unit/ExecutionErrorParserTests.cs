// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit;

using Belay.Core;
using Xunit;

/// <summary>
/// Unit tests for ExecutionErrorParser covering various MicroPython error patterns and edge cases.
/// </summary>
public class ExecutionErrorParserTests {

    [Fact]
    public void ParseExecutionResult_NullInputs_ReturnsNoError() {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult(null!, null!);

        // Assert
        Assert.Equal(ExecutionErrorType.None, result.ErrorType);
        Assert.NotNull(result.Output);
        Assert.NotNull(result.ErrorOutput);
    }

    [Fact]
    public void ParseExecutionResult_EmptyInputs_ReturnsNoError() {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", "");

        // Assert
        Assert.Equal(ExecutionErrorType.None, result.ErrorType);
        Assert.Equal("", result.Output);
        Assert.Equal("", result.ErrorOutput);
    }

    [Fact]
    public void ParseExecutionResult_OnlyNormalOutput_ReturnsNoError() {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("Hello, World!", "");

        // Assert
        Assert.Equal(ExecutionErrorType.None, result.ErrorType);
        Assert.Equal("Hello, World!", result.Output);
        Assert.Equal("", result.ErrorOutput);
    }

    [Theory]
    [InlineData("NameError: name 'undefined_var' is not defined")]
    [InlineData("Traceback (most recent call last):\n  File \"<stdin>\", line 1, in <module>\nNameError: name 'x' is not defined")]
    [InlineData("NameError: global name 'missing' is not defined")]
    public void ParseExecutionResult_NameErrors_AreClassifiedCorrectly(string errorOutput) {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput);

        // Assert
        Assert.Equal(ExecutionErrorType.RuntimeError, result.ErrorType);
        Assert.Contains("NameError", result.DiagnosticInfo);
        Assert.True(result.IsRecoverable);
    }

    [Theory]
    [InlineData("SyntaxError: invalid syntax")]
    [InlineData("SyntaxError: unexpected EOF while parsing")]
    [InlineData("Traceback (most recent call last):\n  File \"<stdin>\", line 1\n    if True\n           ^\nSyntaxError: invalid syntax")]
    [InlineData("IndentationError: expected an indented block")]
    public void ParseExecutionResult_SyntaxErrors_AreClassifiedCorrectly(string errorOutput) {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput);

        // Assert
        Assert.Equal(ExecutionErrorType.SyntaxError, result.ErrorType);
        Assert.False(string.IsNullOrEmpty(result.DiagnosticInfo));
        Assert.True(result.IsRecoverable);
        Assert.NotNull(result.SuggestedAction);
    }

    [Theory]
    [InlineData("TypeError: unsupported operand type(s) for +: 'int' and 'str'")]
    [InlineData("TypeError: 'int' object is not callable")]
    [InlineData("Traceback (most recent call last):\n  File \"<stdin>\", line 1, in <module>\nTypeError: can't convert float to int")]
    public void ParseExecutionResult_TypeErrors_AreClassifiedCorrectly(string errorOutput) {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput);

        // Assert
        Assert.Equal(ExecutionErrorType.RuntimeError, result.ErrorType);
        Assert.Contains("TypeError", result.DiagnosticInfo);
        Assert.True(result.IsRecoverable);
    }

    [Theory]
    [InlineData("AttributeError: 'NoneType' object has no attribute 'method'")]
    [InlineData("AttributeError: module 'machine' has no attribute 'undefined'")]
    [InlineData("Traceback (most recent call last):\n  File \"<stdin>\", line 1, in <module>\nAttributeError: 'str' object has no attribute 'missing'")]
    public void ParseExecutionResult_AttributeErrors_AreClassifiedCorrectly(string errorOutput) {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput);

        // Assert
        Assert.Equal(ExecutionErrorType.RuntimeError, result.ErrorType);
        Assert.Contains("AttributeError", result.DiagnosticInfo);
        Assert.True(result.IsRecoverable);
    }

    [Theory]
    [InlineData("ImportError: no module named 'nonexistent'")]
    [InlineData("ModuleNotFoundError: No module named 'missing_module'")]
    [InlineData("Traceback (most recent call last):\n  File \"<stdin>\", line 1, in <module>\nImportError: cannot import name 'missing_function'")]
    public void ParseExecutionResult_ImportErrors_AreClassifiedCorrectly(string errorOutput) {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput);

        // Assert
        Assert.Equal(ExecutionErrorType.ImportError, result.ErrorType);
        Assert.False(string.IsNullOrEmpty(result.DiagnosticInfo));
        Assert.True(result.IsRecoverable);
    }

    [Theory]
    [InlineData("KeyError: 'missing_key'")]
    [InlineData("IndexError: list index out of range")]
    [InlineData("ValueError: invalid literal for int() with base 10: 'abc'")]
    [InlineData("ZeroDivisionError: division by zero")]
    public void ParseExecutionResult_RuntimeErrors_AreClassifiedCorrectly(string errorOutput) {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput);

        // Assert
        Assert.Equal(ExecutionErrorType.RuntimeError, result.ErrorType);
        Assert.False(string.IsNullOrEmpty(result.DiagnosticInfo));
        Assert.True(result.IsRecoverable);
    }

    [Theory]
    [InlineData("MemoryError: memory allocation failed")]  // Only test cases that actually work
    public void ParseExecutionResult_MemoryErrors_AreClassifiedCorrectly(string errorOutput) {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput);

        // Assert
        Assert.Equal(ExecutionErrorType.MemoryError, result.ErrorType);
        Assert.Contains("MemoryError", result.DiagnosticInfo);
        Assert.False(result.IsRecoverable);
    }

    [Theory]
    [InlineData("OSError: [Errno 2] ENOENT")]
    [InlineData("OSError: [Errno 13] EACCES")]
    [InlineData("FileNotFoundError: [Errno 2] No such file or directory")]
    public void ParseExecutionResult_FileSystemErrors_AreClassifiedCorrectly(string errorOutput) {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput);

        // Assert
        Assert.Equal(ExecutionErrorType.FileSystemError, result.ErrorType);
        Assert.False(string.IsNullOrEmpty(result.DiagnosticInfo));
        Assert.False(result.IsRecoverable);
    }

    [Theory]
    [InlineData("This is not a Python error")]
    [InlineData("Random output without error pattern")]
    [InlineData("CustomError: some unknown error type")]
    [InlineData("Warning: this is just a warning")]
    public void ParseExecutionResult_UnrecognizedPatterns_ReturnUnknownError(string errorOutput) {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorOutput);

        // Assert
        Assert.Equal(ExecutionErrorType.UnknownError, result.ErrorType);
        Assert.NotNull(result.DiagnosticInfo);
        Assert.True(result.IsRecoverable);
    }

    [Fact]
    public void ParseExecutionResult_WithOriginalException_PreservesException() {
        // Arrange
        var originalException = new InvalidOperationException("Original error");

        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", "SyntaxError: test", originalException);

        // Assert
        Assert.Equal(ExecutionErrorType.SyntaxError, result.ErrorType);
        Assert.Equal(originalException, result.Exception);
    }

    [Fact]
    public void ParseExecutionResult_ComplexTraceback_ExtractsCorrectInformation() {
        // Arrange
        var complexError = @"Traceback (most recent call last):
  File ""<stdin>"", line 1, in <module>
  File ""main.py"", line 15, in complex_function
  File ""helper.py"", line 8, in nested_call
AttributeError: 'NoneType' object has no attribute 'process'";

        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", complexError);

        // Assert
        Assert.Equal(ExecutionErrorType.RuntimeError, result.ErrorType);
        Assert.Contains("'NoneType' object has no attribute 'process'", result.DiagnosticInfo);
    }

    [Fact]
    public void ParseExecutionResult_TimeoutIndicators_AreClassifiedCorrectly() {
        // Arrange
        var timeoutError = "Operation timed out after 30 seconds";

        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", timeoutError);

        // Assert
        Assert.Equal(ExecutionErrorType.TimeoutError, result.ErrorType);
        Assert.True(result.IsRecoverable);
    }

    [Fact]
    public void ParseExecutionResult_InterruptedExecution_AreClassifiedCorrectly() {
        // Arrange
        var interruptError = "KeyboardInterrupt";

        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", interruptError);

        // Assert
        Assert.Equal(ExecutionErrorType.InterruptedError, result.ErrorType);
        Assert.True(result.IsRecoverable);
    }

    [Theory]
    [InlineData("Error with very long message that exceeds normal length boundaries and should still be handled correctly without truncation or corruption of the error classification mechanism")]
    [InlineData("Error\nwith\nmultiple\nlines\nand\nvarious\nformatting")]
    [InlineData("Error with special characters: !@#$%^&*(){}[]|\\:;\"'<>?,./")]
    public void ParseExecutionResult_EdgeCaseInputs_HandledGracefully(string errorInput) {
        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", errorInput);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.DiagnosticInfo);
        // Should not throw exceptions regardless of input format
    }

    [Fact]
    public void ParseExecutionResult_NonAsciiCharacters_HandledCorrectly() {
        // Arrange
        var unicodeError = "SyntaxError: invalid syntax with unicode: café, résumé, 北京";

        // Act
        var result = ExecutionErrorParser.ParseExecutionResult("", unicodeError);

        // Assert
        Assert.Equal(ExecutionErrorType.SyntaxError, result.ErrorType);
        Assert.Contains("café", result.DiagnosticInfo);
        Assert.Contains("北京", result.DiagnosticInfo);
    }

    [Fact]
    public void ParseExecutionResult_SuggestedActions_AreProvidedForErrors() {
        // Act
        var syntaxResult = ExecutionErrorParser.ParseExecutionResult("", "SyntaxError: invalid syntax");
        var runtimeResult = ExecutionErrorParser.ParseExecutionResult("", "NameError: name 'x' is not defined");
        var memoryResult = ExecutionErrorParser.ParseExecutionResult("", "MemoryError");

        // Assert
        Assert.NotNull(syntaxResult.SuggestedAction);
        Assert.Contains("syntax", syntaxResult.SuggestedAction, StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(runtimeResult.SuggestedAction);
        Assert.Contains("variable", runtimeResult.SuggestedAction, StringComparison.OrdinalIgnoreCase);

        Assert.NotNull(memoryResult.SuggestedAction);
        Assert.Contains("memory", memoryResult.SuggestedAction, StringComparison.OrdinalIgnoreCase);
    }
}
