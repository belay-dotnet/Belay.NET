// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit.Protocol;

using System.IO;
using Belay.Core.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class RawReplProtocolTests {
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
        var protocol = new RawReplProtocol(stream, NullLogger<RawReplProtocol>.Instance);

        // Act - Using reflection to access private method
        var parseMethod = typeof(RawReplProtocol)
            .GetMethod("ParseResponse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (RawReplResponse)parseMethod!.Invoke(protocol, new object[] { input })!;

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
        var protocol = new RawReplProtocol(stream, NullLogger<RawReplProtocol>.Instance);

        // Act
        var parseMethod = typeof(RawReplProtocol)
            .GetMethod("ParseResponse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = (RawReplResponse)parseMethod!.Invoke(protocol, new object[] { errorOutput })!;

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(errorOutput, result.ErrorOutput);
        Assert.NotNull(result.Exception);
    }
}
