// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Extensions.Factories;

using Belay.Core;
using Belay.Core.Communication;

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
/// </summary>
public interface IExecutorFactory {
    /// <summary>
    /// Creates a task executor instance.
    /// </summary>
    /// <param name="device">The device instance.</param>
    /// <returns>A configured task executor.</returns>
    Belay.Core.Execution.TaskExecutor CreateTaskExecutor(Device device);

    /// <summary>
    /// Creates a setup executor instance.
    /// </summary>
    /// <param name="device">The device instance.</param>
    /// <returns>A configured setup executor.</returns>
    Belay.Core.Execution.SetupExecutor CreateSetupExecutor(Device device);

    /// <summary>
    /// Creates a teardown executor instance.
    /// </summary>
    /// <param name="device">The device instance.</param>
    /// <returns>A configured teardown executor.</returns>
    Belay.Core.Execution.TeardownExecutor CreateTeardownExecutor(Device device);

    /// <summary>
    /// Creates a thread executor instance.
    /// </summary>
    /// <param name="device">The device instance.</param>
    /// <returns>A configured thread executor.</returns>
    Belay.Core.Execution.ThreadExecutor CreateThreadExecutor(Device device);
}
