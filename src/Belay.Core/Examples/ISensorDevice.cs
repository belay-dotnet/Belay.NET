// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Examples;

using System.Threading.Tasks;
using Belay.Attributes;

/// <summary>
/// Example interface demonstrating method interception with the [Task] attribute.
/// This interface shows the recommended pattern for using Belay.NET's attribute-based programming model.
/// </summary>
public interface ISensorDevice {
    /// <summary>
    /// Reads the temperature from the device sensor.
    /// Demonstrates basic [Task] attribute usage.
    /// </summary>
    /// <returns>The temperature reading in Celsius.</returns>
    [Task]
    Task<float> ReadTemperatureAsync();

    /// <summary>
    /// Reads humidity from the device sensor.
    /// Demonstrates [Task] attribute with custom name and timeout.
    /// </summary>
    /// <returns>The humidity reading as a percentage.</returns>
    [Task(Name = "read_humidity", TimeoutMs = 3000)]
    Task<float> ReadHumidityAsync();

    /// <summary>
    /// Gets device information.
    /// Demonstrates [Task] attribute with string return type.
    /// </summary>
    /// <returns>Device information string.</returns>
    [Task(Cache = false)]
    Task<string> GetDeviceInfoAsync();

    /// <summary>
    /// Calibrates the sensor.
    /// Demonstrates [Task] attribute with void return type and longer timeout.
    /// </summary>
    /// <returns>A task representing the calibration operation.</returns>
    [Task(TimeoutMs = 10000, Exclusive = true)]
    Task CalibrateAsync();

    /// <summary>
    /// Sets up the device for sensor readings.
    /// Demonstrates [Setup] attribute for initialization.
    /// </summary>
    /// <returns>A task representing the setup operation.</returns>
    [Setup(Order = 1)]
    Task InitializeSensorAsync();

    /// <summary>
    /// Cleans up the device after use.
    /// Demonstrates [Teardown] attribute for cleanup.
    /// </summary>
    /// <returns>A task representing the cleanup operation.</returns>
    [Teardown(Order = 1)]
    Task CleanupSensorAsync();
}
