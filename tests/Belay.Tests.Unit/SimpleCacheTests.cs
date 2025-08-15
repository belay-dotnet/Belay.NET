using System;
using System.Threading.Tasks;
using Belay.Core;
using Xunit;

namespace Belay.Tests.Unit;

public class SimpleCacheTests {
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
}
