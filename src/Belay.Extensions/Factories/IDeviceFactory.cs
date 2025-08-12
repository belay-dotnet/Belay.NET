// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Extensions.Factories;

using Belay.Core;
using Belay.Core.Communication;
using Belay.Core.Execution;

/// <summary>
/// Factory for creating device instances with dependency injection.
/// </summary>
public interface IDeviceFactory {
    /// <summary>
    /// Creates a device instance with the specified communicator.
    /// </summary>
    /// <param name="communicator">The device communicator.</param>
    /// <returns>A configured device instance.</returns>
    Device CreateDevice(IDeviceCommunication communicator);

    /// <summary>
    /// Creates a device instance with serial communication.
    /// </summary>
    /// <param name="portName">The serial port name.</param>
    /// <param name="baudRate">Optional baud rate (uses configuration default if not specified).</param>
    /// <returns>A configured device instance.</returns>
    Device CreateSerialDevice(string portName, int? baudRate = null);

    /// <summary>
    /// Creates a device instance with subprocess communication for testing.
    /// </summary>
    /// <param name="executablePath">Path to the MicroPython executable.</param>
    /// <param name="arguments">Optional command-line arguments.</param>
    /// <returns>A configured device instance.</returns>
    Device CreateSubprocessDevice(string executablePath, string[]? arguments = null);
}

/// <summary>
/// Factory for creating communication instances.
/// </summary>
public interface ICommunicatorFactory {
    /// <summary>
    /// Creates a serial communicator.
    /// </summary>
    /// <param name="portName">The serial port name.</param>
    /// <param name="baudRate">Optional baud rate (uses configuration default if not specified).</param>
    /// <returns>A configured serial communicator.</returns>
    IDeviceCommunication CreateSerialCommunicator(string portName, int? baudRate = null);

    /// <summary>
    /// Creates a subprocess communicator.
    /// </summary>
    /// <param name="executablePath">Path to the MicroPython executable.</param>
    /// <param name="arguments">Optional command-line arguments.</param>
    /// <returns>A configured subprocess communicator.</returns>
    IDeviceCommunication CreateSubprocessCommunicator(string executablePath, string[]? arguments = null);
}

/// <summary>
/// Factory for creating executor instances.
/// Provides access to the simplified executors from Device instances.
/// </summary>
public interface IExecutorFactory {
    /// <summary>
    /// Gets the task executor instance from the device.
    /// </summary>
    /// <param name="device">The device instance.</param>
    /// <returns>The device's task executor.</returns>
    IExecutor GetTaskExecutor(Device device);

    /// <summary>
    /// Gets the setup executor instance from the device.
    /// </summary>
    /// <param name="device">The device instance.</param>
    /// <returns>The device's setup executor.</returns>
    IExecutor GetSetupExecutor(Device device);

    /// <summary>
    /// Gets the teardown executor instance from the device.
    /// </summary>
    /// <param name="device">The device instance.</param>
    /// <returns>The device's teardown executor.</returns>
    IExecutor GetTeardownExecutor(Device device);

    /// <summary>
    /// Gets the thread executor instance from the device.
    /// </summary>
    /// <param name="device">The device instance.</param>
    /// <returns>The device's thread executor.</returns>
    IExecutor GetThreadExecutor(Device device);
}
