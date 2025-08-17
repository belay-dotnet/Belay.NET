using System;
using System.Threading.Tasks;
using Belay.Core;
using Xunit;

namespace Belay.Tests.Unit;

public class SimpleCacheTests {
    public SimpleCacheTests() {
        // Clear cache before each test to ensure test isolation
        SimpleCache.Clear();
    }
    [Fact]
    public async Task GetOrCreateAsync_ReturnsExpectedValue() {
        var key = "test_key";
        var result = await SimpleCache.GetOrCreateAsync(key, async () => await Task.FromResult("test_value"));

        Assert.Equal("test_value", result);
    }

    [Fact]
    public async Task GetOrCreateAsync_CachesResult() {
        var key = "test_key_cached";
        var callCount = 0;

        async Task<string> Factory() {
            callCount++;
            return await Task.FromResult("test_value");
        }

        var result1 = await SimpleCache.GetOrCreateAsync(key, Factory);
        var result2 = await SimpleCache.GetOrCreateAsync(key, Factory);

        Assert.Equal("test_value", result1);
        Assert.Equal("test_value", result2);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void ContainsKey_WorksCorrectly() {
        var rawKey = "test_key_contains_" + Guid.NewGuid().ToString();
        var typedKey = $"{typeof(string).FullName}::{rawKey}";
        var value = SimpleCache.GetOrCreate(rawKey, () => "test_value");

        Assert.True(SimpleCache.ContainsKey(typedKey));
        Assert.Equal("test_value", value);
    }

    // Error scenario tests

    [Fact]
    public void GetOrCreate_NullKey_ThrowsArgumentNullException() {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            SimpleCache.GetOrCreate<string>(null!, () => "value"));
    }

    [Fact]
    public void GetOrCreateAsync_NullKey_ThrowsArgumentNullException() {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await SimpleCache.GetOrCreateAsync<string>(null!, () => Task.FromResult("value")));
    }

    [Fact]
    public void GetOrCreate_NullFactory_ThrowsArgumentNullException() {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            SimpleCache.GetOrCreate<string>("key", null!));
    }

    [Fact]
    public void GetOrCreateAsync_NullFactory_ThrowsArgumentNullException() {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await SimpleCache.GetOrCreateAsync<string>("key", null!));
    }

    [Fact]
    public void GetOrCreate_EmptyKey_WorksCorrectly() {
        // Arrange
        var key = "";

        // Act
        var result = SimpleCache.GetOrCreate(key, () => "empty_key_value");

        // Assert
        Assert.Equal("empty_key_value", result);
    }

    [Fact]
    public void GetOrCreate_FactoryThrowsException_PropagatesException() {
        // Arrange
        var key = "exception_test";
        var expectedException = new InvalidOperationException("Test exception");

        // Act & Assert
        var actualException = Assert.Throws<InvalidOperationException>(() =>
            SimpleCache.GetOrCreate<string>(key, () => throw expectedException));

        Assert.Equal(expectedException.Message, actualException.Message);
    }

    [Fact]
    public async Task GetOrCreateAsync_FactoryThrowsException_PropagatesException() {
        // Arrange
        var key = "async_exception_test";
        var expectedException = new InvalidOperationException("Async test exception");

        // Act & Assert
        var actualException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await SimpleCache.GetOrCreateAsync<string>(key, () => throw expectedException));

        Assert.Equal(expectedException.Message, actualException.Message);
    }

    [Fact]
    public void GetOrCreate_FactoryReturnsNull_CachesNull() {
        // Arrange
        var key = "null_value_test";

        // Act
        var result1 = SimpleCache.GetOrCreate<string?>(key, () => null);
        var result2 = SimpleCache.GetOrCreate<string?>(key, () => "should_not_be_called");

        // Assert
        Assert.Null(result1);
        Assert.Null(result2); // Should return cached null, not call factory again
    }

    [Fact]
    public async Task GetOrCreateAsync_FactoryReturnsNull_CachesNull() {
        // Arrange
        var key = "async_null_value_test";

        // Act
        var result1 = await SimpleCache.GetOrCreateAsync<string?>(key, () => Task.FromResult<string?>(null));
        var result2 = await SimpleCache.GetOrCreateAsync<string?>(key, () => Task.FromResult<string?>("should_not_be_called"));

        // Assert
        Assert.Null(result1);
        Assert.Null(result2); // Should return cached null, not call factory again
    }

    [Fact]
    public void GetOrCreate_DifferentTypes_SameKey_StoredSeparately() {
        // Arrange
        var key = "type_test_key";

        // Act
        var stringResult = SimpleCache.GetOrCreate(key, () => "string_value");
        var intResult = SimpleCache.GetOrCreate(key, () => 42);

        // Assert
        Assert.Equal("string_value", stringResult);
        Assert.Equal(42, intResult);
    }

    [Fact]
    public void ContainsKey_NullKey_ThrowsArgumentNullException() {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SimpleCache.ContainsKey(null!));
    }

    [Fact]
    public void ContainsKey_NonExistentKey_ReturnsFalse() {
        // Arrange
        var key = "non_existent_" + Guid.NewGuid().ToString();

        // Act
        var exists = SimpleCache.ContainsKey(key);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public void GetOrCreate_VeryLongKey_HandledCorrectly() {
        // Arrange
        var longKey = new string('x', 10000);

        // Act
        var result = SimpleCache.GetOrCreate(longKey, () => "long_key_value");

        // Assert
        Assert.Equal("long_key_value", result);
    }

    [Fact]
    public void GetOrCreate_SpecialCharactersInKey_HandledCorrectly() {
        // Arrange
        var specialKey = "key::with::special::chars!@#$%^&*(){}[]|\\:;\"'<>?,./";

        // Act
        var result = SimpleCache.GetOrCreate(specialKey, () => "special_chars_value");

        // Assert
        Assert.Equal("special_chars_value", result);
    }

    [Fact]
    public async Task GetOrCreateAsync_CancellationToken_IsRespected() {
        // Arrange
        var key = "cancellation_test";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - TaskCanceledException inherits from OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await SimpleCache.GetOrCreateAsync(key, async () => {
                await Task.Delay(1000, cts.Token);
                return "should_not_complete";
            }, cts.Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n\r")]
    [InlineData("normal_key")]
    [InlineData("key with spaces")]
    [InlineData("key\nwith\nnewlines")]
    public void GetOrCreate_VariousKeyFormats_WorkCorrectly(string key) {
        // Act
        var result = SimpleCache.GetOrCreate(key, () => $"value_for_{key.GetHashCode()}");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("value_for_", result);
    }
}
