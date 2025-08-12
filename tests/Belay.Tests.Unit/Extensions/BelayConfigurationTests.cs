// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the Apache 2.0 License.

namespace Belay.Tests.Unit.Extensions;

using Belay.Extensions.Configuration;
using NUnit.Framework;

[TestFixture]
public class BelayConfigurationTests {
    [Test]
    public void BelayConfiguration_DefaultValues_AreSetCorrectly() {
        // Act
        var config = new BelayConfiguration();

        // Assert
        Assert.That(config.Device.DefaultConnectionTimeoutMs, Is.EqualTo(5000));
        Assert.That(config.Device.DefaultCommandTimeoutMs, Is.EqualTo(30000));
        Assert.That(config.Communication.Serial.DefaultBaudRate, Is.EqualTo(115200));
        Assert.That(config.Communication.Serial.ReadTimeoutMs, Is.EqualTo(1000));
        Assert.That(config.Executor.DefaultTaskTimeoutMs, Is.EqualTo(30000));
        Assert.That(config.Executor.MaxCacheSize, Is.EqualTo(1000));
        Assert.That(config.Executor.EnableCachingByDefault, Is.False);
    }

    [Test]
    public void DeviceDiscoveryConfiguration_DefaultValues_AreSetCorrectly() {
        // Act
        var config = new DeviceDiscoveryConfiguration();

        // Assert
        Assert.That(config.EnableAutoDiscovery, Is.True);
        Assert.That(config.DiscoveryTimeoutMs, Is.EqualTo(10000));
        Assert.That(config.SerialPortPatterns, Contains.Item("COM*"));
        Assert.That(config.SerialPortPatterns, Contains.Item("/dev/ttyUSB*"));
        Assert.That(config.SerialPortPatterns, Contains.Item("/dev/ttyACM*"));
    }

    [Test]
    public void RawReplConfiguration_DefaultValues_AreSetCorrectly() {
        // Act
        var config = new RawReplConfiguration();

        // Assert
        Assert.That(config.InitializationTimeoutMs, Is.EqualTo(2000));
        Assert.That(config.WindowSize, Is.EqualTo(256));
        Assert.That(config.MaxRetries, Is.EqualTo(3));
    }

    [Test]
    public void ExceptionHandlingConfiguration_DefaultValues_AreSetCorrectly() {
        // Act
        var config = new ExceptionHandlingConfiguration();

        // Assert
        Assert.That(config.RethrowExceptions, Is.True);
        Assert.That(config.LogExceptions, Is.True);
        Assert.That(config.IncludeStackTraces, Is.True);
        Assert.That(config.ExceptionLogLevel, Is.EqualTo(Microsoft.Extensions.Logging.LogLevel.Error));
        Assert.That(config.PreserveContext, Is.True);
        Assert.That(config.MaxContextEntries, Is.EqualTo(50));
    }

    [Test]
    public void RetryConfiguration_DefaultValues_AreSetCorrectly() {
        // Act
        var config = new RetryConfiguration();

        // Assert
        Assert.That(config.MaxRetries, Is.EqualTo(3));
        Assert.That(config.InitialRetryDelayMs, Is.EqualTo(1000));
        Assert.That(config.BackoffMultiplier, Is.EqualTo(2.0));
        Assert.That(config.MaxRetryDelayMs, Is.EqualTo(30000));
    }

    [Test]
    public void BelayConfiguration_CanBeModified() {
        // Arrange
        var config = new BelayConfiguration();

        // Act
        config.Device.DefaultConnectionTimeoutMs = 15000;
        config.Communication.Serial.DefaultBaudRate = 9600;
        // Assert
        Assert.That(config.Device.DefaultConnectionTimeoutMs, Is.EqualTo(15000));
        Assert.That(config.Communication.Serial.DefaultBaudRate, Is.EqualTo(9600));
    }
}
