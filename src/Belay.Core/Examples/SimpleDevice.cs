// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Examples;

using System.Threading.Tasks;
using Belay.Attributes;
using Belay.Core.Communication;
using Microsoft.Extensions.Logging;

/// <summary>
/// Simple example device demonstrating [Task] attribute method interception.
/// This shows how users can inherit from Device and use attributes for method execution.
/// </summary>
public class SimpleDevice : Device {
    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleDevice"/> class.
    /// </summary>
    /// <param name="communication">The device communication implementation.</param>
    /// <param name="logger">Optional logger for device operations.</param>
    public SimpleDevice(IDeviceCommunication communication, ILogger<Device>? logger = null)
        : base(communication, logger) {
    }

    /// <summary>
    /// Simple task method that returns a greeting from the device.
    /// This demonstrates basic [Task] attribute usage with method interception.
    /// </summary>
    /// <returns>A greeting message from the MicroPython device.</returns>
    [Task]
    public virtual async Task<string> GetGreetingAsync() {
        // This code should be intercepted and executed on the device
        return await this.ExecuteAsync<string>("'Hello from MicroPython!'");
    }

    /// <summary>
    /// Task method that reads the device temperature.
    /// Demonstrates [Task] with more complex Python code execution.
    /// </summary>
    /// <returns>The temperature reading from the device.</returns>
    [Task(Name = "read_temp", TimeoutMs = 5000)]
    public virtual async Task<float> ReadTemperatureAsync() {
        // This should be intercepted and the Python code executed on device
        return await this.ExecuteAsync<float>(@"
import machine
import time

# Simulate temperature reading
# In real implementation, this would read from a sensor
temp = 23.5 + (time.ticks_ms() % 100) / 100.0
temp
");
    }

    /// <summary>
    /// Setup method that initializes the device.
    /// Demonstrates [Setup] attribute for initialization code.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Setup(Order = 1)]
    public virtual async Task InitializeAsync() {
        await this.ExecuteAsync(@"
# Initialize device
print('Device initialized')
import machine
import time
");
    }

    /// <summary>
    /// Teardown method that cleans up the device.
    /// Demonstrates [Teardown] attribute for cleanup code.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Teardown(Order = 1)]
    public virtual async Task CleanupAsync() {
        await this.ExecuteAsync(@"
# Cleanup device resources
print('Device cleanup complete')
");
    }
}
