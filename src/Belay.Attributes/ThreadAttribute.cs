// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Attributes;

/// <summary>
/// Marks a method to be executed as a background thread on the MicroPython device.
/// Methods decorated with this attribute run asynchronously on the device without
/// blocking the host application or other device operations.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ThreadAttribute"/> enables long-running or continuous operations
/// to execute on MicroPython devices using the _thread module. This is essential
/// for background monitoring, data collection, or reactive behavior that should
/// run independently of host application calls.
/// </para>
/// <para>
/// Thread methods are non-blocking from the host perspective - they start the
/// background operation and return immediately. The background code continues
/// to run on the device until explicitly stopped or the device disconnects.
/// </para>
/// <para>
/// Thread management includes:
/// <list type="bullet">
/// <item><description>Automatic thread lifecycle tracking</description></item>
/// <item><description>Graceful shutdown on device disconnection</description></item>
/// <item><description>Thread monitoring and health checks</description></item>
/// <item><description>Inter-thread communication support</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Continuous Sensor Monitoring</strong></para>
/// <code>
/// public class EnvironmentMonitor : Device
/// {
///     [Thread]
///     public async Task StartContinuousMonitoringAsync(int intervalMs = 1000)
///     {
///         await ExecuteAsync($@"
///             import _thread
///             import time
///             import machine
///
///             def monitor_environment():
///                 adc = machine.ADC(machine.Pin(26))
///                 while globals().get('monitoring_active', True):
///                     try:
///                         # Read sensor values
///                         temp = read_temperature(adc)
///                         humidity = read_humidity()
///
///                         # Log or transmit data
///                         print(f'Temp: {{temp}}C, Humidity: {{humidity}}%')
///
///                         time.sleep_ms({intervalMs})
///                     except Exception as e:
///                         print(f'Monitoring error: {{e}}')
///                         time.sleep_ms(5000)  # Back off on error
///
///             # Start monitoring thread
///             _thread.start_new_thread(monitor_environment, ())
///         ");
///     }
///
///     [Task]
///     public async Task StopMonitoringAsync()
///     {
///         await ExecuteAsync("monitoring_active = False");
///     }
/// }
/// </code>
/// <para><strong>Event-Driven Responses</strong></para>
/// <code>
/// public class ButtonHandler : Device
/// {
///     [Thread]
///     public async Task StartButtonWatcherAsync()
///     {
///         await ExecuteAsync(@"
///             import _thread
///             import machine
///             import time
///
///             def watch_buttons():
///                 button1 = machine.Pin(2, machine.Pin.IN, machine.Pin.PULL_UP)
///                 button2 = machine.Pin(3, machine.Pin.IN, machine.Pin.PULL_UP)
///
///                 last_state = [True, True]  # Pulled up initially
///
///                 while globals().get('button_watching', True):
///                     current_state = [button1.value(), button2.value()]
///
///                     # Check for button presses (high to low transition)
///                     for i, (last, current) in enumerate(zip(last_state, current_state)):
///                         if last and not current:  # Button pressed
///                             print(f'Button {i+1} pressed!')
///                             handle_button_press(i+1)
///
///                     last_state = current_state
///                     time.sleep_ms(50)  # 50ms polling
///
///             _thread.start_new_thread(watch_buttons, ())
///         ");
///     }
/// }
/// </code>
/// <para><strong>Watchdog and Health Monitoring</strong></para>
/// <code>
/// public class SystemMonitor : Device
/// {
///     [Thread(Name = "system_watchdog")]
///     public async Task StartSystemWatchdogAsync()
///     {
///         await ExecuteAsync(@"
///             import _thread
///             import machine
///             import gc
///             import time
///
///             def system_watchdog():
///                 last_heartbeat = time.ticks_ms()
///
///                 while globals().get('watchdog_active', True):
///                     try:
///                         current_time = time.ticks_ms()
///
///                         # Check system health
///                         free_mem = gc.mem_free()
///                         if free_mem &lt; 1000:  # Low memory warning
///                             print(f'WARNING: Low memory: {{free_mem}} bytes')
///                             gc.collect()
///
///                         # Check for system heartbeat
///                         if time.ticks_diff(current_time, last_heartbeat) > 30000:
///                             print('WARNING: No heartbeat for 30 seconds')
///
///                         # Update heartbeat if main loop is responsive
///                         if globals().get('system_heartbeat', 0) > last_heartbeat:
///                             last_heartbeat = globals()['system_heartbeat']
///
///                         time.sleep_ms(5000)  # Check every 5 seconds
///
///                     except Exception as e:
///                         print(f'Watchdog error: {e}')
///                         time.sleep_ms(10000)  # Back off on error
///
///             _thread.start_new_thread(system_watchdog, ())
///         ");
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ThreadAttribute : Attribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="ThreadAttribute"/> class.
    /// </summary>
    public ThreadAttribute() {
    }

    /// <summary>
    /// Gets or sets the name of the thread for identification and management.
    /// If not specified, a name is automatically generated based on the method name.
    /// </summary>
    /// <value>
    /// The name to use for the thread. If null or empty, an automatic name is generated.
    /// Thread names should be unique within a device instance.
    /// </value>
    /// <remarks>
    /// <para>
    /// Thread names are used for:
    /// <list type="bullet">
    /// <item><description>Identifying threads in logs and diagnostics</description></item>
    /// <item><description>Managing thread lifecycle (start/stop/query)</description></item>
    /// <item><description>Inter-thread communication and synchronization</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Thread(Name = "sensor_poller")]
    /// public async Task StartSensorPollingAsync()
    /// {
    ///     // Thread will be known as "sensor_poller" on the device
    ///     await ExecuteAsync("start_polling_thread()");
    /// }
    /// </code>
    /// </example>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether the thread should automatically restart if it terminates unexpectedly.
    /// When true, the thread will be monitored and restarted if it exits due to an error.
    /// </summary>
    /// <value>
    /// <c>true</c> if the thread should automatically restart on failure; otherwise, <c>false</c>.
    /// Default is <c>false</c> to avoid resource leaks from repeatedly failing threads.
    /// </value>
    /// <remarks>
    /// <para>
    /// Auto-restart is useful for critical background operations that should continue
    /// running despite occasional errors. However, use with caution as rapidly failing
    /// threads can consume device resources.
    /// </para>
    /// <para>
    /// Auto-restart includes backoff logic to prevent rapid restart loops:
    /// <list type="bullet">
    /// <item><description>Initial restart delay of 1 second</description></item>
    /// <item><description>Exponential backoff up to maximum of 60 seconds</description></item>
    /// <item><description>Reset backoff after successful run of 5 minutes</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Thread(AutoRestart = true)]
    /// public async Task StartDataLoggerAsync()
    /// {
    ///     await ExecuteAsync(@"
    ///         import _thread
    ///
    ///         def data_logger():
    ///             while True:
    ///                 try:
    ///                     # Log data with error recovery
    ///                     log_sensor_data()
    ///                     time.sleep(60)
    ///                 except Exception as e:
    ///                     print(f'Logger error: {e}')
    ///                     time.sleep(10)  # Brief recovery delay
    ///
    ///         _thread.start_new_thread(data_logger, ())
    ///     ");
    /// }
    /// </code>
    /// </example>
    public bool AutoRestart { get; set; } = false;

    /// <summary>
    /// Gets or sets the priority level for thread execution.
    /// Higher priority threads may receive more CPU time on capable platforms.
    /// </summary>
    /// <value>
    /// The thread priority level. Default is <see cref="ThreadPriority.Normal"/>.
    /// </value>
    /// <remarks>
    /// <para>
    /// Thread priority is a hint to the MicroPython scheduler and may not be
    /// supported on all platforms. Use primarily for documentation and future
    /// platform compatibility.
    /// </para>
    /// <para>
    /// Priority guidelines:
    /// <list type="bullet">
    /// <item><description><see cref="ThreadPriority.Low"/>: Background tasks, logging</description></item>
    /// <item><description><see cref="ThreadPriority.Normal"/>: Standard monitoring, periodic tasks</description></item>
    /// <item><description><see cref="ThreadPriority.High"/>: Time-critical operations, safety systems</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Thread(Priority = ThreadPriority.High)]
    /// public async Task StartSafetyMonitorAsync()
    /// {
    ///     // High priority for safety-critical monitoring
    ///     await ExecuteAsync("start_safety_monitor_thread()");
    /// }
    ///
    /// [Thread(Priority = ThreadPriority.Low)]
    /// public async Task StartDataLoggerAsync()
    /// {
    ///     // Low priority for background logging
    ///     await ExecuteAsync("start_logging_thread()");
    /// }
    /// </code>
    /// </example>
    public ThreadPriority Priority { get; set; } = ThreadPriority.Normal;

    /// <summary>
    /// Gets or sets the maximum runtime for the thread in milliseconds.
    /// If specified, the thread will be automatically terminated after this duration.
    /// </summary>
    /// <value>
    /// The maximum runtime in milliseconds, or <c>null</c> for unlimited runtime.
    /// Must be a positive value if specified.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when setting a runtime value that is less than or equal to zero.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Use MaxRuntimeMs to prevent runaway threads or to implement time-bounded
    /// operations. This is particularly useful for data collection windows or
    /// temporary monitoring periods.
    /// </para>
    /// <para>
    /// When the runtime limit is reached, the thread is gracefully terminated
    /// by setting a stop flag that the thread should check periodically.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Thread(MaxRuntimeMs = 300000)] // Run for 5 minutes
    /// public async Task StartBenchmarkAsync()
    /// {
    ///     await ExecuteAsync(@"
    ///         import _thread
    ///         import time
    ///
    ///         def benchmark():
    ///             start_time = time.ticks_ms()
    ///             iterations = 0
    ///
    ///             # Run until stop flag is set (by runtime limit)
    ///             while not globals().get('benchmark_stop', False):
    ///                 perform_benchmark_iteration()
    ///                 iterations += 1
    ///
    ///                 # Check stop condition periodically
    ///                 if iterations % 100 == 0:
    ///                     if globals().get('benchmark_stop', False):
    ///                         break
    ///
    ///             elapsed = time.ticks_diff(time.ticks_ms(), start_time)
    ///             print(f'Benchmark completed: {iterations} iterations in {elapsed}ms')
    ///
    ///         _thread.start_new_thread(benchmark, ())
    ///     ");
    /// }
    /// </code>
    /// </example>
    public int? MaxRuntimeMs {
        get => maxRuntimeMs;
        set {
            if (value.HasValue && value.Value <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Maximum runtime must be a positive value in milliseconds");
            }

            maxRuntimeMs = value;
        }
    }

    private int? maxRuntimeMs;

    /// <summary>
    /// Returns a string that represents the current <see cref="ThreadAttribute"/>.
    /// </summary>
    /// <returns>A string that represents the current attribute configuration.</returns>
    public override string ToString() {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(Name)) {
            parts.Add($"Name=\"{Name}\"");
        }

        if (AutoRestart) {
            parts.Add("AutoRestart=true");
        }

        if (Priority != ThreadPriority.Normal) {
            parts.Add($"Priority={Priority}");
        }

        if (MaxRuntimeMs.HasValue) {
            parts.Add($"MaxRuntimeMs={MaxRuntimeMs}");
        }

        return parts.Count > 0 ? $"[Thread({string.Join(", ", parts)})]" : "[Thread]";
    }
}

/// <summary>
/// Defines the priority levels for thread execution.
/// </summary>
/// <remarks>
/// Thread priority is a hint to the scheduler and may not be supported
/// on all MicroPython platforms. Actual scheduling behavior depends on
/// the underlying platform and available threading implementation.
/// </remarks>
public enum ThreadPriority {
    /// <summary>
    /// Low priority thread for background tasks.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority thread for standard operations.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// High priority thread for time-critical operations.
    /// </summary>
    High = 2,
}
