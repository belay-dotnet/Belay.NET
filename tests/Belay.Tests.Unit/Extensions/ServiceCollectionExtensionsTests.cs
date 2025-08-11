// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the Apache 2.0 License.

namespace Belay.Tests.Unit.Extensions;

using Belay.Extensions;
using Belay.Extensions.Configuration;
using Belay.Extensions.Factories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;

[TestFixture]
public class ServiceCollectionExtensionsTests {
    [Test]
    public void AddBelay_WithDefaultConfiguration_RegistersAllServices() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddBelay();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        Assert.That(serviceProvider.GetService<IDeviceFactory>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<ICommunicatorFactory>(), Is.Not.Null);
        Assert.That(serviceProvider.GetService<IExecutorFactory>(), Is.Not.Null);
    }

    [Test]
    public void AddBelay_WithActionConfiguration_ConfiguresOptions() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddBelay(config => {
            config.Device.DefaultConnectionTimeoutMs = 10000;
            config.Communication.Serial.DefaultBaudRate = 9600;
        });
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<IOptions<BelayConfiguration>>();
        Assert.That(options.Value.Device.DefaultConnectionTimeoutMs, Is.EqualTo(10000));
        Assert.That(options.Value.Communication.Serial.DefaultBaudRate, Is.EqualTo(9600));
    }

    [Test]
    public void AddBelay_WithIConfiguration_BindsConfiguration() {
        // Arrange
        var configData = new Dictionary<string, string?> {
            ["Belay:Device:DefaultConnectionTimeoutMs"] = "15000",
            ["Belay:Communication:Serial:DefaultBaudRate"] = "57600"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddBelay(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetRequiredService<IOptions<BelayConfiguration>>();
        Assert.That(options.Value.Device.DefaultConnectionTimeoutMs, Is.EqualTo(15000));
        Assert.That(options.Value.Communication.Serial.DefaultBaudRate, Is.EqualTo(57600));
    }

    [Test]
    public void GetBelayDeviceFactory_ReturnsDeviceFactory() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBelay();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var deviceFactory = serviceProvider.GetBelayDeviceFactory();

        // Assert
        Assert.That(deviceFactory, Is.Not.Null);
        Assert.That(deviceFactory, Is.InstanceOf<IDeviceFactory>());
    }

    [Test]
    public void GetBelayExecutorFactory_ReturnsExecutorFactory() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBelay();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var executorFactory = serviceProvider.GetBelayExecutorFactory();

        // Assert
        Assert.That(executorFactory, Is.Not.Null);
        Assert.That(executorFactory, Is.InstanceOf<IExecutorFactory>());
    }
}
