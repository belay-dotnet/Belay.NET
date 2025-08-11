// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using Belay.Core.Caching;
using FluentAssertions;
using NUnit.Framework;

namespace Belay.Tests.Unit.Caching;

[TestFixture]
public class MethodCacheKeyTests {
    [Test]
    public void Constructor_WithValidParameters_CreatesKey() {
        // Arrange & Act
        var key = new MethodCacheKey("device1", "v1.0", "method_signature");

        // Assert
        key.Should().NotBeNull();
        key.Hash.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Equals_WithSameValues_ReturnsTrue() {
        // Arrange
        var key1 = new MethodCacheKey("device1", "v1.0", "method");
        var key2 = new MethodCacheKey("device1", "v1.0", "method");

        // Act & Assert
        key1.Equals(key2).Should().BeTrue();
        key1.Should().Be(key2);
    }

    [Test]
    public void Equals_WithDifferentDeviceId_ReturnsFalse() {
        // Arrange
        var key1 = new MethodCacheKey("device1", "v1.0", "method");
        var key2 = new MethodCacheKey("device2", "v1.0", "method");

        // Act & Assert
        key1.Equals(key2).Should().BeFalse();
        key1.Should().NotBe(key2);
    }

    [Test]
    public void Equals_WithDifferentFirmwareVersion_ReturnsFalse() {
        // Arrange
        var key1 = new MethodCacheKey("device1", "v1.0", "method");
        var key2 = new MethodCacheKey("device1", "v2.0", "method");

        // Act & Assert
        key1.Equals(key2).Should().BeFalse();
        key1.Should().NotBe(key2);
    }

    [Test]
    public void Equals_WithDifferentMethodSignature_ReturnsFalse() {
        // Arrange
        var key1 = new MethodCacheKey("device1", "v1.0", "method1");
        var key2 = new MethodCacheKey("device1", "v1.0", "method2");

        // Act & Assert
        key1.Equals(key2).Should().BeFalse();
        key1.Should().NotBe(key2);
    }

    [Test]
    public void GetHashCode_WithSameValues_ReturnsSameHashCode() {
        // Arrange
        var key1 = new MethodCacheKey("device1", "v1.0", "method");
        var key2 = new MethodCacheKey("device1", "v1.0", "method");

        // Act & Assert
        key1.GetHashCode().Should().Be(key2.GetHashCode());
    }

    [Test]
    public void GetHashCode_WithDifferentValues_ReturnsDifferentHashCode() {
        // Arrange
        var key1 = new MethodCacheKey("device1", "v1.0", "method1");
        var key2 = new MethodCacheKey("device1", "v1.0", "method2");

        // Act & Assert
        key1.GetHashCode().Should().NotBe(key2.GetHashCode());
    }

    [Test]
    public void ToString_ReturnsFormattedString() {
        // Arrange
        var key = new MethodCacheKey("device1", "v1.0", "method");

        // Act
        var result = key.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain(key.Hash);
    }

    [Test]
    public void OperatorEquals_WithSameValues_ReturnsTrue() {
        // Arrange
        var key1 = new MethodCacheKey("device1", "v1.0", "method");
        var key2 = new MethodCacheKey("device1", "v1.0", "method");

        // Act & Assert
        (key1 == key2).Should().BeTrue();
        (key1 != key2).Should().BeFalse();
    }

    [Test]
    public void OperatorEquals_WithDifferentValues_ReturnsFalse() {
        // Arrange
        var key1 = new MethodCacheKey("device1", "v1.0", "method1");
        var key2 = new MethodCacheKey("device1", "v1.0", "method2");

        // Act & Assert
        (key1 == key2).Should().BeFalse();
        (key1 != key2).Should().BeTrue();
    }
}
