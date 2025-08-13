// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Extensions.Factories;

using Belay.Core;

/// <summary>
/// Factory for creating device instances with dependency injection.
/// </summary>
public interface IDeviceFactory {
    /// <summary>
    /// Creates a device instance with the specified connection.
    /// </summary>
    /// <param name="connection">The device connection.</param>
    /// <returns>A configured device instance.</returns>
    SimplifiedDevice CreateDevice(DeviceConnection connection);

    /// <summary>
    /// Creates a device instance with serial communication.
    /// </summary>
    /// <param name="portName">The serial port name.</param>
    /// <param name="baudRate">Optional baud rate (uses configuration default if not specified).</param>
    /// <returns>A configured device instance.</returns>
    SimplifiedDevice CreateSerialDevice(string portName, int? baudRate = null);

    /// <summary>
    /// Creates a device instance with subprocess communication for testing.
    /// </summary>
    /// <param name="executablePath">Path to the MicroPython executable.</param>
    /// <param name="arguments">Optional command-line arguments.</param>
    /// <returns>A configured device instance.</returns>
    SimplifiedDevice CreateSubprocessDevice(string executablePath, string[]? arguments = null);
}

/// <summary>
/// Factory for creating communication instances (deprecated - kept for compatibility).
/// </summary>
public interface ICommunicatorFactory {
    /// <summary>
    /// Creates a serial communicator.
    /// </summary>
    /// <param name="portName">The serial port name.</param>
    /// <param name="baudRate">Optional baud rate (uses configuration default if not specified).</param>
    /// <returns>A configured serial communicator.</returns>
    DeviceConnection CreateSerialCommunicator(string portName, int? baudRate = null);

    /// <summary>
    /// Creates a subprocess communicator.
    /// </summary>
    /// <param name="executablePath">Path to the MicroPython executable.</param>
    /// <param name="arguments">Optional command-line arguments.</param>
    /// <returns>A configured subprocess communicator.</returns>
    DeviceConnection CreateSubprocessCommunicator(string executablePath, string[]? arguments = null);
}

/// <summary>
/// Factory for creating executor instances for simplified architecture.
/// </summary>
public interface IExecutorFactory {
    /// <summary>
    /// Gets the task executor instance from the device.
    /// </summary>
    /// <param name="device">The device instance.</param>
    /// <returns>The device's task executor.</returns>
    DirectExecutor GetTaskExecutor(SimplifiedDevice device);

    /// <summary>
    /// Gets the setup executor instance from the device.
    /// </summary>
    /// <param name="device">The device instance.</param>
    /// <returns>The device's setup executor.</returns>
    DirectExecutor GetSetupExecutor(SimplifiedDevice device);

    /// <summary>
    /// Gets the teardown executor instance from the device.
    /// </summary>
    /// <param name="device">The device instance.</param>
    /// <returns>The device's teardown executor.</returns>
    DirectExecutor GetTeardownExecutor(SimplifiedDevice device);

    /// <summary>
    /// Gets the thread executor instance from the device.
    /// </summary>
    /// <param name="device">The device instance.</param>
    /// <returns>The device's thread executor.</returns>
    DirectExecutor GetThreadExecutor(SimplifiedDevice device);
}
