// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using Belay.Core.Caching;
using FluentAssertions;
using NUnit.Framework;

namespace Belay.Tests.Unit.Caching;

[TestFixture]
public class MethodCacheEntryTests
{
    [Test]
    public void Constructor_WithValue_CreatesEntry()
    {
        // Arrange
        var value = "test_value";
        var expiration = TimeSpan.FromMinutes(5);

        // Act
        var entry = new MethodCacheEntry<string>(value, expiration);

        // Assert
        entry.Value.Should().Be(value);
        entry.ExpiresAfter.Should().Be(expiration);
        entry.IsExpired.Should().BeFalse();
        entry.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        entry.LastAccessedAt.Should().Be(entry.CreatedAt);
    }

    [Test]
    public void Constructor_WithoutExpiration_UsesDefaultExpiration()
    {
        // Arrange
        var value = "test_value";

        // Act
        var entry = new MethodCacheEntry<string>(value);

        // Assert
        entry.Value.Should().Be(value);
        entry.ExpiresAfter.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Test]
    public void IsExpired_WhenNotExpired_ReturnsFalse()
    {
        // Arrange
        var entry = new MethodCacheEntry<string>("value", TimeSpan.FromMinutes(5));

        // Act & Assert
        entry.IsExpired.Should().BeFalse();
    }

    [Test]
    public void IsExpired_WhenExpired_ReturnsTrue()
    {
        // Arrange
        var entry = new MethodCacheEntry<string>("value", TimeSpan.FromMilliseconds(1));

        // Act
        Thread.Sleep(10);

        // Assert
        entry.IsExpired.Should().BeTrue();
    }

    [Test]
    public void UpdateLastAccessed_UpdatesTimestamp()
    {
        // Arrange
        var entry = new MethodCacheEntry<string>("value");
        var originalTime = entry.LastAccessedAt;

        // Act
        Thread.Sleep(1);
        entry.UpdateLastAccessed();

        // Assert
        entry.LastAccessedAt.Should().BeAfter(originalTime);
    }

    [Test]
    public void GetValue_ReturnsValueAsObject()
    {
        // Arrange
        var value = "test_value";
        var entry = new MethodCacheEntry<string>(value);

        // Act
        var result = entry.GetValue();

        // Assert
        result.Should().Be(value);
        result.Should().BeOfType<string>();
    }

    [Test]
    public void GetRemainingLifetime_WhenNotExpired_ReturnsPositiveTimeSpan()
    {
        // Arrange
        var expiration = TimeSpan.FromMinutes(5);
        var entry = new MethodCacheEntry<string>("value", expiration);

        // Act
        var remaining = entry.GetRemainingLifetime();

        // Assert
        remaining.Should().BePositive();
        remaining.Should().BeLessThanOrEqualTo(expiration);
    }

    [Test]
    public void GetRemainingLifetime_WhenExpired_ReturnsZero()
    {
        // Arrange
        var entry = new MethodCacheEntry<string>("value", TimeSpan.FromMilliseconds(1));

        // Act
        Thread.Sleep(10);
        var remaining = entry.GetRemainingLifetime();

        // Assert
        remaining.Should().Be(TimeSpan.Zero);
    }

    [Test]
    public void Constructor_WithGenericTypes_WorksCorrectly()
    {
        // Arrange & Act
        var stringEntry = new MethodCacheEntry<string>("hello");
        var intEntry = new MethodCacheEntry<int>(42);
        var boolEntry = new MethodCacheEntry<bool>(true);

        // Assert
        stringEntry.Value.Should().Be("hello");
        stringEntry.GetValue().Should().BeOfType<string>();

        intEntry.Value.Should().Be(42);
        intEntry.GetValue().Should().BeOfType<int>();

        boolEntry.Value.Should().Be(true);
        boolEntry.GetValue().Should().BeOfType<bool>();
    }
}