using System.Text;
using Belay.Core.Communication;
using Belay.Core.Protocol;
using Belay.Core.Testing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Belay.Tests.Integration;

[Collection("MicroPython")]
[Trait("Category", "Integration")]
[Trait("Category", "UnixPort")]
public class RawReplProtocolTests : IDisposable {
    private readonly SubprocessDeviceCommunication _device;
    private readonly ILogger<RawReplProtocolTests> _logger;

    public RawReplProtocolTests() {
        var loggerFactory = LoggerFactory.Create(builder => {
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Debug);
        });

        _logger = loggerFactory.CreateLogger<RawReplProtocolTests>();

        // Ensure MicroPython unix port is built
        var micropythonPath = MicroPythonUnixPort.FindMicroPythonExecutable();
        if (string.IsNullOrEmpty(micropythonPath)) {
            MicroPythonUnixPort.BuildUnixPort();
            micropythonPath = MicroPythonUnixPort.FindMicroPythonExecutable();
        }

        _device = new SubprocessDeviceCommunication(
            micropythonPath!,
            logger: loggerFactory.CreateLogger<SubprocessDeviceCommunication>());
    }

    [Fact]
    public async Task Should_Connect_To_MicroPython_Subprocess() {
        // Act
        await _device.StartAsync();

        // Assert
        _device.State.Should().Be(DeviceConnectionState.Connected);
    }

    [Fact]
    public async Task Should_Execute_Simple_Expression() {
        // Arrange
        await _device.StartAsync();

        // Act
        var result = await _device.ExecuteAsync("1 + 2");

        // Assert
        result.Should().Contain("3");
    }

    [Fact]
    public async Task Should_Execute_Multiple_Commands_Sequentially() {
        // Arrange
        await _device.StartAsync();

        // Act & Assert
        var result1 = await _device.ExecuteAsync("x = 42");
        var result2 = await _device.ExecuteAsync("x * 2");

        result2.Should().Contain("84");
    }

    [Fact]
    public async Task Should_Handle_Multiline_Code() {
        // Arrange
        await _device.StartAsync();
        var code = @"
def factorial(n):
    if n <= 1:
        return 1
    return n * factorial(n - 1)

factorial(5)
";

        // Act
        var result = await _device.ExecuteAsync(code);

        // Assert
        result.Should().Contain("120");
    }

    [Fact]
    public async Task Should_Handle_Large_Code_Blocks_With_Flow_Control() {
        // Arrange
        await _device.StartAsync();

        // Generate a large code block that would trigger flow control
        var largeCode = new StringBuilder();
        largeCode.AppendLine("data = [");
        for (int i = 0; i < 100; i++) {
            largeCode.AppendLine($"    {i},");
        }
        largeCode.AppendLine("]");
        largeCode.AppendLine("sum(data)");

        // Act
        var result = await _device.ExecuteAsync(largeCode.ToString());

        // Assert
        result.Should().Contain("4950"); // Sum of 0..99
    }

    [Fact]
    public async Task Should_Capture_Print_Statements() {
        // Arrange
        await _device.StartAsync();
        var outputs = new List<string>();
        _device.OutputReceived += (sender, e) => outputs.Add(e.Output);

        // Act
        await _device.ExecuteAsync("print('Hello, World!')");

        // Assert
        outputs.Should().Contain(o => o.Contains("Hello, World!"));
    }

    [Fact]
    public async Task Should_Handle_Syntax_Errors() {
        // Arrange
        await _device.StartAsync();

        // Act
        Func<Task> act = async () => await _device.ExecuteAsync("invalid syntax here");

        // Assert
        await act.Should().ThrowAsync<DeviceExecutionException>()
            .WithMessage("*Code execution failed on device*");
    }

    [Fact]
    public async Task Should_Handle_Runtime_Errors() {
        // Arrange
        await _device.StartAsync();

        // Act
        Func<Task> act = async () => await _device.ExecuteAsync("1 / 0");

        // Assert
        await act.Should().ThrowAsync<DeviceExecutionException>()
            .Where(e => e.DeviceTraceback != null && e.DeviceTraceback.Contains("ZeroDivisionError"));
    }

    [Fact]
    public async Task Should_Execute_Import_Statements() {
        // Arrange
        await _device.StartAsync();

        // Act
        var result = await _device.ExecuteAsync(@"
import sys
sys.version_info
");

        // Assert
        result.Should().Contain("3"); // MicroPython version 3.x
    }

    [Fact]
    public async Task Should_Handle_Unicode_Strings() {
        // Arrange
        await _device.StartAsync();

        // Act
        var result = await _device.ExecuteAsync("'Hello 世界 🌍'");

        // Assert
        result.Should().Contain("Hello 世界 🌍");
    }

    [Fact]
    public async Task Should_Execute_List_Comprehensions() {
        // Arrange
        await _device.StartAsync();

        // Act
        var result = await _device.ExecuteAsync("[x**2 for x in range(5)]");

        // Assert
        result.Should().Contain("0");
        result.Should().Contain("1");
        result.Should().Contain("4");
        result.Should().Contain("9");
        result.Should().Contain("16");
    }

    [Fact]
    public async Task Should_Handle_Async_Code() {
        // Arrange
        await _device.StartAsync();
        var code = @"
import asyncio

async def hello():
    return 'Hello from async'

asyncio.run(hello())
";

        // Act
        var result = await _device.ExecuteAsync(code);

        // Assert
        result.Should().Contain("Hello from async");
    }

    [Fact]
    public async Task Should_Return_Typed_Results() {
        // Arrange
        await _device.StartAsync();

        // Act
        var intResult = await _device.ExecuteAsync<int>("42");
        var floatResult = await _device.ExecuteAsync<double>("3.14");
        var boolResult = await _device.ExecuteAsync<bool>("True");
        var stringResult = await _device.ExecuteAsync<string>("'test'");

        // Assert
        intResult.Should().Be(42);
        floatResult.Should().BeApproximately(3.14, 0.01);
        boolResult.Should().BeTrue();
        stringResult.Should().Contain("test");
    }

    [Fact]
    public async Task Should_Handle_Json_Serialization() {
        // Arrange
        await _device.StartAsync();
        var code = @"
import json
data = {'name': 'test', 'value': 42, 'items': [1, 2, 3]}
json.dumps(data)
";

        // Act
        var result = await _device.ExecuteAsync<Dictionary<string, object>>(code);

        // Assert
        result.Should().ContainKey("name");
        result.Should().ContainKey("value");
        result.Should().ContainKey("items");
    }

    [Fact]
    public async Task Should_Maintain_State_Between_Executions() {
        // Arrange
        await _device.StartAsync();

        // Act
        await _device.ExecuteAsync("counter = 0");
        await _device.ExecuteAsync("counter += 5");
        await _device.ExecuteAsync("counter *= 2");
        var result = await _device.ExecuteAsync<int>("counter");

        // Assert
        result.Should().Be(10);
    }

    [Fact]
    public async Task Should_Handle_File_Operations() {
        // Arrange
        await _device.StartAsync();
        var testContent = "Test file content";
        var code = $@"
with open('/tmp/test.txt', 'w') as f:
    f.write('{testContent}')

with open('/tmp/test.txt', 'r') as f:
    f.read()
";

        // Act
        var result = await _device.ExecuteAsync(code);

        // Assert
        result.Should().Contain(testContent);
    }

    public void Dispose() {
        _device?.Dispose();
    }
}
