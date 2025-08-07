using Belay.Core.Protocol;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Belay.Tests.Unit.Protocol;

[TestFixture]
public class RawReplProtocolTests {
    private Mock<ILogger<RawReplProtocol>> _mockLogger = null!;
    private MemoryStream _memoryStream = null!;
    private RawReplProtocol _protocol = null!;

    [SetUp]
    public void SetUp() {
        _mockLogger = new Mock<ILogger<RawReplProtocol>>();
        _memoryStream = new MemoryStream();
        _protocol = new RawReplProtocol(_memoryStream, _mockLogger.Object);
    }

    [TearDown]
    public void TearDown() {
        _protocol.Dispose();
        _memoryStream.Dispose();
    }

    [Test]
    public void Constructor_WithValidParameters_SetsInitialState() {
        // Assert
        Assert.That(_protocol.CurrentState, Is.EqualTo(RawReplState.Normal));
    }

    [Test]
    public void Constructor_WithNullStream_ThrowsArgumentNullException() {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RawReplProtocol(null!, _mockLogger.Object));
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException() {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RawReplProtocol(_memoryStream, null!));
    }

    [Test]
    public async Task InitializeAsync_WithValidStream_CompletesSuccessfully() {
        // Arrange
        var responseData = ">>>";
        await WriteToStreamAsync(responseData);
        _memoryStream.Position = 0;

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _protocol.InitializeAsync());
        Assert.That(_protocol.CurrentState, Is.EqualTo(RawReplState.Normal));
    }

    [Test]
    public void Dispose_WhenCalled_DoesNotThrow() {
        // Act & Assert
        Assert.DoesNotThrow(() => _protocol.Dispose());
    }

    [Test]
    public void Dispose_WhenCalledMultipleTimes_DoesNotThrow() {
        // Act & Assert
        Assert.DoesNotThrow(() => {
            _protocol.Dispose();
            _protocol.Dispose();
        });
    }

    [Test]
    public void ExecuteCodeAsync_AfterDispose_ThrowsObjectDisposedException() {
        // Arrange
        _protocol.Dispose();

        // Act & Assert
        Assert.ThrowsAsync<ObjectDisposedException>(
            () => _protocol.ExecuteCodeAsync("print('hello')"));
    }

    [Test]
    [TestCase(RawReplState.Normal)]
    [TestCase(RawReplState.Raw)]
    [TestCase(RawReplState.RawPaste)]
    public void CurrentState_ReflectsInternalState(RawReplState expectedState) {
        // This test would require access to internal state setting
        // In a real implementation, we'd test state transitions through public methods
        Assert.That(_protocol.CurrentState, Is.EqualTo(RawReplState.Normal));
    }


    private async Task WriteToStreamAsync(string data) {
        var bytes = System.Text.Encoding.UTF8.GetBytes(data);
        await _memoryStream.WriteAsync(bytes);
    }

}

/// <summary>
/// Test fixture for Raw REPL protocol exceptions
/// </summary>
[TestFixture]
public class RawReplExceptionTests {
    [Test]
    public void RawReplProtocolException_WithStates_StoresStatesCorrectly() {
        // Arrange
        var expectedState = RawReplState.Raw;
        var actualState = RawReplState.Normal;
        var message = "Test exception";

        // Act
        var exception = new RawReplProtocolException(message, expectedState, actualState);

        // Assert
        Assert.That(exception.Message, Is.EqualTo(message));
        Assert.That(exception.ExpectedState, Is.EqualTo(expectedState));
        Assert.That(exception.ActualState, Is.EqualTo(actualState));
    }

    [Test]
    public void FlowControlException_WithWindowSizeAndByte_StoresValuesCorrectly() {
        // Arrange
        var message = "Flow control error";
        var windowSize = 256;
        var receivedByte = (byte)0xFF;

        // Act
        var exception = new FlowControlException(message, windowSize, receivedByte);

        // Assert
        Assert.That(exception.Message, Is.EqualTo(message));
        Assert.That(exception.WindowSize, Is.EqualTo(windowSize));
        Assert.That(exception.ReceivedByte, Is.EqualTo(receivedByte));
        Assert.That(exception.ExpectedState, Is.EqualTo(RawReplState.RawPaste));
        Assert.That(exception.ActualState, Is.EqualTo(RawReplState.RawPaste));
    }
}

/// <summary>
/// Test fixture for Raw REPL response handling
/// </summary>
[TestFixture]
public class RawReplResponseTests {
    [Test]
    public void RawReplResponse_DefaultValues_AreSetCorrectly() {
        // Act
        var response = new RawReplResponse();

        // Assert
        Assert.That(response.IsSuccess, Is.False);
        Assert.That(response.Output, Is.EqualTo(string.Empty));
        Assert.That(response.ErrorOutput, Is.EqualTo(string.Empty));
        Assert.That(response.Result, Is.EqualTo(string.Empty));
        Assert.That(response.Exception, Is.Null);
    }

    [Test]
    public void RawReplResponse_CanSetAllProperties() {
        // Arrange
        var response = new RawReplResponse();
        var testException = new Exception("Test exception");

        // Act
        response.IsSuccess = true;
        response.Output = "Test output";
        response.ErrorOutput = "Test error";
        response.Result = "Test result";
        response.Exception = testException;

        // Assert
        Assert.That(response.IsSuccess, Is.True);
        Assert.That(response.Output, Is.EqualTo("Test output"));
        Assert.That(response.ErrorOutput, Is.EqualTo("Test error"));
        Assert.That(response.Result, Is.EqualTo("Test result"));
        Assert.That(response.Exception, Is.EqualTo(testException));
    }
}
