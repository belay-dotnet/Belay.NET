// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit.Protocol;

using System.IO;
using System.Text;
using Belay.Core.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class AdaptiveRawReplProtocolTests {
    [Theory]
    [InlineData("OKtest1\r\n\x04\x04>", "test1")]
    [InlineData("OK\x04\x04>", "")]
    [InlineData("OK4\r\n\x04\x04>", "4")]
    [InlineData("OKrp2\r\n\x04\x04>", "rp2")]
    [InlineData("OKhello world\r\n\x04\x04>", "hello world")]
    [InlineData("OK>", "")]
    [InlineData("test without OK prefix>", "test without OK prefix")]
    [InlineData("OKmultiline\nresponse\r\n\x04\x04>", "multiline\nresponse")]
    public void ParseResponse_ShouldCorrectlyExtractContent(string input, string expected) {
        // Arrange
        using var stream = new MemoryStream();
        using var protocol = new AdaptiveRawReplProtocol(stream, NullLogger<AdaptiveRawReplProtocol>.Instance);

        // Act - Using reflection to access private method
        var parseMethod = typeof(AdaptiveRawReplProtocol)
            .GetMethod("ParseResponse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (RawReplResponse)parseMethod!.Invoke(null, new object[] { input })!;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Result);
        Assert.Equal(input, result.Output);
    }

    [Theory]
    [InlineData("Traceback (most recent call last):\n  File \"<stdin>\", line 1")]
    [InlineData("Error: Something went wrong")]
    [InlineData("Exception: Test exception")]
    public void ParseResponse_ShouldHandleErrorResponses(string errorOutput) {
        // Arrange
        using var stream = new MemoryStream();
        using var protocol = new AdaptiveRawReplProtocol(stream, NullLogger<AdaptiveRawReplProtocol>.Instance);

        // Act
        var parseMethod = typeof(AdaptiveRawReplProtocol)
            .GetMethod("ParseResponse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (RawReplResponse)parseMethod!.Invoke(null, new object[] { errorOutput })!;

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(errorOutput, result.ErrorOutput);
        Assert.NotNull(result.Exception);
    }

    [Theory]
    [InlineData("1", "print(1)")]
    [InlineData("2+2", "print(2+2)")]
    [InlineData("len('hello')", "print(len('hello'))")]
    [InlineData("math.pi", "print(math.pi)")]
    [InlineData("sys.platform", "print(sys.platform)")]
    public void PreprocessCodeForRawRepl_ShouldWrapSimpleExpressions(string input, string expected) {
        // Arrange
        using var stream = new MemoryStream();
        using var protocol = new AdaptiveRawReplProtocol(stream, NullLogger<AdaptiveRawReplProtocol>.Instance);

        // Act
        var preprocessMethod = typeof(AdaptiveRawReplProtocol)
            .GetMethod("PreprocessCodeForRawRepl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (string)preprocessMethod!.Invoke(null, new object[] { input })!;

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("print('hello')")]
    [InlineData("def foo(): pass")]
    [InlineData("import sys")]
    [InlineData("from math import pi")]
    [InlineData("x = 5")]
    [InlineData("if True:\n    pass")]
    [InlineData("for i in range(5): pass")]
    [InlineData("class Test: pass")]
    public void PreprocessCodeForRawRepl_ShouldNotWrapComplexStatements(string input) {
        // Arrange
        using var stream = new MemoryStream();
        using var protocol = new AdaptiveRawReplProtocol(stream, NullLogger<AdaptiveRawReplProtocol>.Instance);

        // Act
        var preprocessMethod = typeof(AdaptiveRawReplProtocol)
            .GetMethod("PreprocessCodeForRawRepl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (string)preprocessMethod!.Invoke(null, new object[] { input })!;

        // Assert
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData("x == 5")]
    [InlineData("a != b")]
    [InlineData("value <= 10")]
    [InlineData("result >= 0")]
    [InlineData("x < y")]
    [InlineData("a > b")]
    public void PreprocessCodeForRawRepl_ShouldWrapComparisonExpressions(string input) {
        // Arrange
        using var stream = new MemoryStream();
        using var protocol = new AdaptiveRawReplProtocol(stream, NullLogger<AdaptiveRawReplProtocol>.Instance);

        // Act
        var preprocessMethod = typeof(AdaptiveRawReplProtocol)
            .GetMethod("PreprocessCodeForRawRepl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (string)preprocessMethod!.Invoke(null, new object[] { input })!;

        // Assert
        Assert.Equal($"print({input})", result);
    }

    [Fact]
    public void PreprocessCodeForRawRepl_ShouldHandleEmptyInput() {
        // Arrange
        using var stream = new MemoryStream();
        using var protocol = new AdaptiveRawReplProtocol(stream, NullLogger<AdaptiveRawReplProtocol>.Instance);

        // Act
        var preprocessMethod = typeof(AdaptiveRawReplProtocol)
            .GetMethod("PreprocessCodeForRawRepl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (string?)preprocessMethod!.Invoke(null, new object[] { "" });

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void PreprocessCodeForRawRepl_ShouldHandleNullInput() {
        // Arrange
        using var stream = new MemoryStream();
        using var protocol = new AdaptiveRawReplProtocol(stream, NullLogger<AdaptiveRawReplProtocol>.Instance);

        // Act
        var preprocessMethod = typeof(AdaptiveRawReplProtocol)
            .GetMethod("PreprocessCodeForRawRepl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (string?)preprocessMethod!.Invoke(null, new object?[] { null });

        // Assert
        Assert.Null(result);
    }
}
