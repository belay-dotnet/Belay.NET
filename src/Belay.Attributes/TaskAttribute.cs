// Copyright (c) 2024 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for more information.
/// <summary>
/// Marks a method as a remote task to be executed on a MicroPython device.
/// Methods decorated with this attribute will have their code deployed to the connected device
/// and executed remotely when called from the host application.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="TaskAttribute"/> is the core attribute of the Belay.NET programming model.
/// It enables seamless execution of C# methods on remote MicroPython devices by automatically
/// handling code deployment, parameter marshaling, and result retrieval.
/// </para>
/// <para>
/// When a method decorated with [Task] is called, the Belay.NET runtime:
/// <list type="number">
/// <item><description>Converts the method body to Python code</description></item>
/// <item><description>Deploys the code to the connected MicroPython device</description></item>
/// <item><description>Marshals parameters to Python-compatible types</description></item>
/// <item><description>Executes the code on the device</description></item>
/// <item><description>Retrieves and deserializes the result back to .NET types</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Basic Task Method</strong></para>
/// <code>
/// public class TemperatureSensor : Device
/// {
///     [Task]
///     public async Task&lt;float&gt; ReadTemperatureAsync()
///     {
///         // This code executes on the MicroPython device
///         return await ExecuteAsync&lt;float&gt;(@"
///             import machine
///             import time
///
///             # Read from analog temperature sensor
///             adc = machine.ADC(machine.Pin(26))
///             reading = adc.read_u16()
///             voltage = reading * 3.3 / 65535
///             temperature = 27 - (voltage - 0.706) / 0.001721
///             temperature
///         ");
///     }
/// }
/// </code>
/// <para><strong>Task with Parameters</strong></para>
/// <code>
/// public class LEDController : Device
/// {
///     [Task]
///     public async Task SetBrightnessAsync(int pin, float brightness)
///     {
///         // Parameters are automatically marshaled to the device
///         await ExecuteAsync($@"
///             import machine
///             led = machine.PWM(machine.Pin({pin}))
///             led.freq(1000)
///             led.duty_u16(int({brightness} * 65535))
///         ");
///     }
/// }
/// </code>
/// <para><strong>Complex Return Types</strong></para>
/// <code>
/// public class EnvironmentSensor : Device
/// {
///     [Task]
///     public async Task&lt;SensorReading&gt; GetReadingsAsync()
///     {
///         return await ExecuteAsync&lt;SensorReading&gt;(@"
///             import json
///             # Read multiple sensors
///             temp = read_temperature()
///             humidity = read_humidity()
///             pressure = read_pressure()
///
///             # Return as JSON for automatic deserialization
///             json.dumps({
///                 'temperature': temp,
///                 'humidity': humidity,
///                 'pressure': pressure,
///                 'timestamp': time.ticks_ms()
///             })
///         ");
///     }
/// }
///
/// public class SensorReading
/// {
///     public float Temperature { get; set; }
///     public float Humidity { get; set; }
///     public float Pressure { get; set; }
///     public long Timestamp { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TaskAttribute : Attribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskAttribute"/> class.
    /// </summary>
    public TaskAttribute() {
    }

    /// <summary>
    /// Gets or sets the name of the method on the device.
    /// If not specified, the C# method name will be used.
    /// </summary>
    /// <value>
    /// The name to use for the method when deployed to the device.
    /// If null or empty, the original method name is used.
    /// </value>
    /// <example>
    /// <code>
    /// [Task(Name = "read_sensor")]
    /// public async Task&lt;float&gt; ReadTemperatureAsync()
    /// {
    ///     // This method will be deployed as "read_sensor" on the device
    ///     return await ExecuteAsync&lt;float&gt;("read_sensor()");
    /// }
    /// </code>
    /// </example>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether the method should be cached on the device.
    /// When true, the method code is deployed once and reused for subsequent calls.
    /// When false, the code is sent for each invocation.
    /// </summary>
    /// <value>
    /// <c>true</c> if the method should be cached on the device; otherwise, <c>false</c>.
    /// Default is <c>true</c> for better performance.
    /// </value>
    /// <remarks>
    /// <para>
    /// Caching improves performance by avoiding repeated code deployment, but may consume
    /// device memory. Disable caching for methods that are called infrequently or when
    /// device memory is constrained.
    /// </para>
    /// <para>
    /// Cached methods are automatically invalidated and redeployed if the method implementation
    /// changes between application runs.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Task(Cache = false)]
    /// public async Task&lt;string&gt; GetDiagnosticInfoAsync()
    /// {
    ///     // This method's code is sent fresh each time
    ///     // Useful for debugging or infrequent operations
    ///     return await ExecuteAsync&lt;string&gt;(@"
    ///         import gc, sys
    ///         f'Memory: {gc.mem_free()}, Platform: {sys.platform}'
    ///     ");
    /// }
    /// </code>
    /// </example>
    public bool Cache { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for method execution in milliseconds.
    /// If not specified, uses the default timeout configured for the device.
    /// </summary>
    /// <value>
    /// The timeout in milliseconds, or <c>null</c> to use the default timeout.
    /// Must be a positive value if specified.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when setting a timeout value that is less than or equal to zero.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Use custom timeouts for methods that may take longer to execute than typical
    /// device operations, such as sensor calibration routines or file operations.
    /// </para>
    /// <para>
    /// Setting too short a timeout may cause premature cancellation of legitimate
    /// long-running operations. Setting too long a timeout may delay error detection
    /// for failed operations.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Task(TimeoutMs = 30000)] // 30 second timeout
    /// public async Task CalibrateAsync()
    /// {
    ///     // Long-running calibration process
    ///     await ExecuteAsync(@"
    ///         import time
    ///         # Perform multi-step calibration
    ///         for step in range(10):
    ///             perform_calibration_step(step)
    ///             time.sleep(2)  # Wait between steps
    ///         save_calibration_data()
    ///     ");
    /// }
    /// </code>
    /// </example>
    public int? TimeoutMs {
        get => this.timeoutMs;
        set {
            if (value.HasValue && value.Value <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Timeout must be a positive value in milliseconds");
            }

            this.timeoutMs = value;
        }
    }

    private int? timeoutMs;

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether this task method requires exclusive access to the device.
    /// When true, ensures no other methods execute concurrently on the device.
    /// </summary>
    /// <value>
    /// <c>true</c> if the method requires exclusive device access; otherwise, <c>false</c>.
    /// Default is <c>false</c> to allow concurrent execution where possible.
    /// </value>
    /// <remarks>
    /// <para>
    /// Use exclusive access for methods that:
    /// <list type="bullet">
    /// <item><description>Modify critical device state</description></item>
    /// <item><description>Require exclusive hardware resource access</description></item>
    /// <item><description>Cannot be safely interrupted by other operations</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Exclusive methods may reduce overall system performance by blocking other
    /// operations, so use sparingly and only when necessary for correctness.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Task(Exclusive = true)]
    /// public async Task UpdateFirmwareAsync(byte[] firmwareData)
    /// {
    ///     // Critical operation that must not be interrupted
    ///     await ExecuteAsync(@"
    ///         import machine
    ///         # Disable interrupts during firmware update
    ///         machine.disable_irq()
    ///         try:
    ///             write_firmware_data(data)
    ///             verify_firmware()
    ///         finally:
    ///             machine.enable_irq()
    ///     ");
    /// }
    /// </code>
    /// </example>
    public bool Exclusive { get; set; } = false;

    /// <summary>
    /// Returns a string that represents the current <see cref="TaskAttribute"/>.
    /// </summary>
    /// <returns>A string that represents the current attribute configuration.</returns>
    public override string ToString() {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(this.Name)) {
            parts.Add($"Name={this.Name}");
        }

        if (!this.Cache) {
            parts.Add("Cache=false");
        }

        if (this.TimeoutMs.HasValue) {
            parts.Add($"TimeoutMs={this.TimeoutMs}");
        }

        if (this.Exclusive) {
            parts.Add("Exclusive=true");
        }

        return parts.Count > 0 ? $"[Task({string.Join(", ", parts)})]" : "[Task]";
    }
}
