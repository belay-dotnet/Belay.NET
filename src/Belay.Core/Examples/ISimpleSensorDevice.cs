// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Examples;

using System.Threading.Tasks;
using Belay.Attributes;

/// <summary>
/// Example interface demonstrating method interception with the [Task] and [PythonCode] attributes.
/// This interface shows how to embed Python code directly in method declarations.
/// </summary>
public interface ISimpleSensorDevice {
    /// <summary>
    /// Gets a simple greeting from the device.
    /// Demonstrates basic [Task] and [PythonCode] attribute usage.
    /// </summary>
    /// <returns>A greeting message from the MicroPython device.</returns>
    [Task]
    [PythonCode("'Hello from MicroPython device!'")]
    Task<string> GetGreetingAsync();

    /// <summary>
    /// Reads a simulated temperature value.
    /// Demonstrates [PythonCode] with more complex Python code.
    /// </summary>
    /// <returns>The simulated temperature reading.</returns>
    [Task(TimeoutMs = 3000)]
    [PythonCode(@"
        import time
        # Simulate temperature reading with some variation
        base_temp = 23.5
        variation = (time.ticks_ms() % 100) / 100.0
        temperature = base_temp + variation
        temperature
    ")]
    Task<float> ReadTemperatureAsync();

    /// <summary>
    /// Controls an LED with specified pin and state.
    /// Demonstrates parameter substitution in Python code.
    /// </summary>
    /// <param name="pin">The GPIO pin number for the LED.</param>
    /// <param name="state">The LED state (True for on, False for off).</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Task]
    [PythonCode(@"
        import machine
        led = machine.Pin({pin}, machine.Pin.OUT)
        led.value({state})
        print(f'LED on pin {pin} set to {state}')
    ")]
    Task SetLEDAsync(int pin, bool state);

    /// <summary>
    /// Gets device information without parameter substitution.
    /// Demonstrates disabling parameter substitution.
    /// </summary>
    /// <returns>Device information string.</returns>
    [Task]
    [PythonCode(
        @"
        import sys
        import gc
        info = f'Platform: {sys.platform}, Memory: {gc.mem_free()} bytes'
        info
    ", EnableParameterSubstitution = false)]
    Task<string> GetDeviceInfoAsync();

    /// <summary>
    /// Initializes the device sensors.
    /// Demonstrates [Setup] attribute with [PythonCode].
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Setup(Order = 1)]
    [PythonCode(@"
        # Initialize device sensors
        print('Initializing sensors...')
        import machine
        import time
        time.sleep_ms(100)
        print('Sensors initialized successfully')
    ")]
    Task InitializeAsync();

    /// <summary>
    /// Cleans up the device.
    /// Demonstrates [Teardown] attribute with [PythonCode].
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Teardown(Order = 1)]
    [PythonCode(@"
        # Cleanup device resources
        print('Cleaning up device resources...')
        import gc
        gc.collect()
        print('Device cleanup complete')
    ")]
    Task CleanupAsync();
}
