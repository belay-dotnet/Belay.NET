// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Attributes;

/// <summary>
/// Marks a method to be executed once during device initialization.
/// Methods decorated with this attribute are automatically called when the device
/// connects and establishes communication, providing a hook for device-specific setup.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="SetupAttribute"/> is used to perform one-time initialization tasks
/// when a device connection is established. This is ideal for configuring hardware,
/// initializing sensors, setting up global variables, or preparing the device environment.
/// </para>
/// <para>
/// Setup methods are executed in the order they are declared in the class hierarchy,
/// with base class setup methods running before derived class setup methods.
/// Multiple setup methods in the same class are executed in declaration order.
/// </para>
/// <para>
/// If a setup method fails, the device connection will be considered failed and
/// subsequent setup methods will not be executed.
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Basic Device Setup</strong></para>
/// <code>
/// public class TemperatureSensor : Device
/// {
///     [Setup]
///     private async Task InitializeSensorAsync()
///     {
///         // Runs automatically when device connects
///         await ExecuteAsync(@"
///             import machine
///             import time
///
///             # Configure temperature sensor
///             sensor_pin = machine.Pin(26)
///             temp_adc = machine.ADC(sensor_pin)
///
///             # Set reference voltage
///             machine.ADC.ATTN_11DB = 3  # 3.3V range
///             temp_adc.atten(machine.ADC.ATTN_11DB)
///
///             print('Temperature sensor initialized')
///         ");
///     }
/// }
/// </code>
/// <para><strong>Multi-Step Setup</strong></para>
/// <code>
/// public class RobotController : Device
/// {
///     [Setup]
///     private async Task InitializeHardwareAsync()
///     {
///         await ExecuteAsync(@"
///             import machine
///
///             # Initialize motor pins
///             motor_left = machine.PWM(machine.Pin(12))
///             motor_right = machine.PWM(machine.Pin(13))
///             motor_left.freq(1000)
///             motor_right.freq(1000)
///         ");
///     }
///
///     [Setup]
///     private async Task LoadCalibrationAsync()
///     {
///         await ExecuteAsync(@"
///             # Load calibration data from file
///             try:
///                 with open('calibration.json', 'r') as f:
///                     calibration = json.loads(f.read())
///             except:
///                 # Use defaults if no calibration file
///                 calibration = {'motor_offset': 0, 'turn_rate': 1.0}
///         ");
///     }
/// }
/// </code>
/// <para><strong>Setup with Error Handling</strong></para>
/// <code>
/// public class DisplayController : Device
/// {
///     [Setup]
///     private async Task InitializeDisplayAsync()
///     {
///         await ExecuteAsync(@"
///             import machine
///             import ssd1306
///
///             try:
///                 i2c = machine.I2C(0, scl=machine.Pin(22), sda=machine.Pin(21))
///                 display = ssd1306.SSD1306_I2C(128, 64, i2c)
///                 display.fill(0)
///                 display.text('Ready', 0, 0, 1)
///                 display.show()
///                 print('Display initialized successfully')
///             except Exception as e:
///                 print(f'Display initialization failed: {e}')
///                 raise  # Re-raise to fail the setup
///         ");
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SetupAttribute : Attribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="SetupAttribute"/> class.
    /// </summary>
    public SetupAttribute() {
    }

    /// <summary>
    /// Gets or sets the order in which this setup method should be executed
    /// relative to other setup methods in the same class.
    /// </summary>
    /// <value>
    /// The execution order. Methods with lower order values execute first.
    /// Methods with the same order value execute in declaration order.
    /// Default is 0.
    /// </value>
    /// <remarks>
    /// <para>
    /// Use the Order property when you have multiple setup methods that must
    /// execute in a specific sequence. For example, hardware initialization
    /// should occur before loading configuration that depends on that hardware.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class SensorArray : Device
    /// {
    ///     [Setup(Order = 1)]
    ///     private async Task InitializeHardwareAsync()
    ///     {
    ///         // Initialize hardware first
    ///         await ExecuteAsync("setup_i2c_bus()");
    ///     }
    ///
    ///     [Setup(Order = 2)]
    ///     private async Task ConfigureSensorsAsync()
    ///     {
    ///         // Configure sensors after hardware is ready
    ///         await ExecuteAsync("configure_all_sensors()");
    ///     }
    ///
    ///     [Setup(Order = 3)]
    ///     private async Task StartDataCollectionAsync()
    ///     {
    ///         // Start collection after everything is configured
    ///         await ExecuteAsync("start_background_collection()");
    ///     }
    /// }
    /// </code>
    /// </example>
    public int Order { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether setup failure should be treated as a critical error.
    /// When true, setup failure will cause device connection to fail completely.
    /// When false, setup failure will be logged but connection will proceed.
    /// </summary>
    /// <value>
    /// <c>true</c> if setup failure should fail the connection; otherwise, <c>false</c>.
    /// Default is <c>true</c> to ensure proper device initialization.
    /// </value>
    /// <remarks>
    /// <para>
    /// Set Critical to false for optional setup operations that should not prevent
    /// device usage if they fail, such as loading optional configuration or
    /// initializing non-essential peripherals.
    /// </para>
    /// <para>
    /// Critical setup methods that fail will prevent the device from being marked
    /// as connected, and subsequent setup methods will not execute.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class IoTDevice : Device
    /// {
    ///     [Setup(Critical = true)]
    ///     private async Task InitializeCore()
    ///     {
    ///         // Essential initialization - must succeed
    ///         await ExecuteAsync("initialize_core_systems()");
    ///     }
    ///
    ///     [Setup(Critical = false)]
    ///     private async Task LoadOptionalConfig()
    ///     {
    ///         // Optional configuration - can fail gracefully
    ///         await ExecuteAsync("load_user_preferences()");
    ///     }
    /// }
    /// </code>
    /// </example>
    public bool Critical { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for setup method execution in milliseconds.
    /// Setup methods may need longer timeouts for hardware initialization.
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
    /// Hardware initialization often takes longer than normal operations,
    /// so setup methods may require extended timeouts. Consider the time needed
    /// for sensor stabilization, network connections, or file system operations.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Setup(TimeoutMs = 30000)] // 30 second timeout for WiFi connection
    /// private async Task ConnectToWiFiAsync()
    /// {
    ///     await ExecuteAsync(@"
    ///         import network
    ///         import time
    ///
    ///         wifi = network.WLAN(network.STA_IF)
    ///         wifi.active(True)
    ///         wifi.connect('MyNetwork', 'password')
    ///
    ///         # Wait for connection
    ///         timeout = 25  # Leave some buffer
    ///         while not wifi.isconnected() and timeout > 0:
    ///             time.sleep(1)
    ///             timeout -= 1
    ///
    ///         if not wifi.isconnected():
    ///             raise Exception('WiFi connection failed')
    ///     ");
    /// }
    /// </code>
    /// </example>
    public int TimeoutMs {
        get => this.timeoutMs;
        set {
            if (value != -1 && value <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Timeout must be a positive value in milliseconds, or -1 to use default timeout");
            }

            this.timeoutMs = value;
        }
    }

    private int timeoutMs = -1;

    /// <summary>
    /// Returns a string that represents the current <see cref="SetupAttribute"/>.
    /// </summary>
    /// <returns>A string that represents the current attribute configuration.</returns>
    public override string ToString() {
        var parts = new List<string>();

        if (this.Order != 0) {
            parts.Add($"Order={this.Order}");
        }

        if (!this.Critical) {
            parts.Add("Critical=false");
        }

        if (this.TimeoutMs != -1) {
            parts.Add($"TimeoutMs={this.TimeoutMs}");
        }

        return parts.Count > 0 ? $"[Setup({string.Join(", ", parts)})]" : "[Setup]";
    }
}
