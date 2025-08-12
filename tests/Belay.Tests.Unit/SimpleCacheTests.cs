using System;
using System.Threading.Tasks;
using Belay.Core.Caching.Simplified;
using Xunit;

namespace Belay.Tests.Unit;

public class SimpleCacheTests
{
    [Fact]
    public async Task GetOrCreateAsync_ReturnsExpectedValue()
    {
        var cache = new SimpleCache(1000);
        var key = "test_key";
        var result = await cache.GetOrCreateAsync(key, async () => "test_value");
        
        Assert.Equal("test_value", result);
    }

    [Fact]
    public async Task GetOrCreateAsync_CachesResult()
    {
        var cache = new SimpleCache(1000);
        var key = "test_key";
        var callCount = 0;

        async Task<string> Factory()
        {
            callCount++;
            return "test_value";
        }

        var result1 = await cache.GetOrCreateAsync(key, Factory);
        var result2 = await cache.GetOrCreateAsync(key, Factory);
        
        Assert.Equal("test_value", result1);
        Assert.Equal("test_value", result2);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetOrCreateAsync_EnforcesMaxEntries()
    {
        var cache = new SimpleCache(2);
        
        await cache.GetOrCreateAsync("key1", () => Task.FromResult("value1"));
        await cache.GetOrCreateAsync("key2", () => Task.FromResult("value2"));
        await cache.GetOrCreateAsync("key3", () => Task.FromResult("value3"));
        
        Assert.False(await cache.TryGetValueAsync("key1"));
        Assert.True(await cache.TryGetValueAsync("key2"));
        Assert.True(await cache.TryGetValueAsync("key3"));
    }
}