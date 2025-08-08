// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using Belay.Core.Caching;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Belay.Tests.Unit.Caching;

[TestFixture]
public class MethodDeploymentCacheTests
{
    private MethodDeploymentCache cache = null!;
    private MethodCacheConfiguration configuration = null!;

    [SetUp]
    public void Setup()
    {
        configuration = new MethodCacheConfiguration
        {
            MaxCacheSize = 100,
            DefaultExpiration = TimeSpan.FromMinutes(5),
            EnablePeriodicCleanup = false // Disable for tests
        };
        cache = new MethodDeploymentCache(configuration);
    }

    [TearDown]
    public void TearDown()
    {
        cache?.Dispose();
    }

    [Test]
    public void Get_WhenKeyDoesNotExist_ReturnsDefault()
    {
        // Arrange
        var key = new MethodCacheKey("device1", "v1.0", "test_method");

        // Act
        var result = cache.Get<string>(key);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void Set_And_Get_WhenKeyExists_ReturnsValue()
    {
        // Arrange
        var key = new MethodCacheKey("device1", "v1.0", "test_method");
        var value = "test_result";

        // Act
        cache.Set(key, value);
        var result = cache.Get<string>(key);

        // Assert
        result.Should().Be(value);
    }

    [Test]
    public void Get_WhenEntryIsExpired_ReturnsDefault()
    {
        // Arrange
        var key = new MethodCacheKey("device1", "v1.0", "test_method");
        var value = "test_result";

        // Act
        cache.Set(key, value, TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10); // Wait for expiration
        var result = cache.Get<string>(key);

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void Remove_WhenKeyExists_RemovesEntryAndReturnsTrue()
    {
        // Arrange
        var key = new MethodCacheKey("device1", "v1.0", "test_method");
        var value = "test_result";
        cache.Set(key, value);

        // Act
        var removed = cache.Remove(key);
        var result = cache.Get<string>(key);

        // Assert
        removed.Should().BeTrue();
        result.Should().BeNull();
    }

    [Test]
    public void Remove_WhenKeyDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var key = new MethodCacheKey("device1", "v1.0", "test_method");

        // Act
        var removed = cache.Remove(key);

        // Assert
        removed.Should().BeFalse();
    }

    [Test]
    public void ClearAll_RemovesAllEntries()
    {
        // Arrange
        var key1 = new MethodCacheKey("device1", "v1.0", "method1");
        var key2 = new MethodCacheKey("device1", "v1.0", "method2");
        cache.Set(key1, "value1");
        cache.Set(key2, "value2");

        // Act
        cache.ClearAll();

        // Assert
        cache.Get<string>(key1).Should().BeNull();
        cache.Get<string>(key2).Should().BeNull();
        cache.GetStatistics().CurrentEntryCount.Should().Be(0);
    }

    [Test]
    public void GetStatistics_TracksHitsAndMisses()
    {
        // Arrange
        var key = new MethodCacheKey("device1", "v1.0", "test_method");
        var value = "test_result";

        // Act
        cache.Get<string>(key); // Miss
        cache.Set(key, value);
        cache.Get<string>(key); // Hit
        cache.Get<string>(key); // Hit

        var stats = cache.GetStatistics();

        // Assert
        stats.TotalHits.Should().Be(2);
        stats.TotalMisses.Should().Be(1);
        stats.CurrentEntryCount.Should().Be(1);
    }

    [Test]
    public void Set_WithGenericTypes_WorksCorrectly()
    {
        // Arrange
        var stringKey = new MethodCacheKey("device1", "v1.0", "string_method");
        var intKey = new MethodCacheKey("device1", "v1.0", "int_method");
        var stringValue = "hello";
        var intValue = 42;

        // Act
        cache.Set(stringKey, stringValue);
        cache.Set(intKey, intValue);

        // Assert
        cache.Get<string>(stringKey).Should().Be(stringValue);
        cache.Get<int>(intKey).Should().Be(intValue);
    }

    [Test]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var cache = new MethodDeploymentCache(configuration);

        // Act & Assert
        Assert.DoesNotThrow(() => cache.Dispose());
    }

    [Test]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        // Arrange
        var cache = new MethodDeploymentCache(configuration);

        // Act & Assert
        Assert.DoesNotThrow(() => 
        {
            cache.Dispose();
            cache.Dispose();
            cache.Dispose();
        });
    }
}