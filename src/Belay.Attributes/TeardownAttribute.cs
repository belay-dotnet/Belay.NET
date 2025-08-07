// Copyright 2025 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0

namespace Belay.Attributes;

/// <summary>
/// Marks a method to be executed during device disconnection or disposal.
/// Methods decorated with this attribute are automatically called when the device
/// connection is terminated, providing cleanup and resource management capabilities.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="TeardownAttribute"/> ensures proper cleanup when device connections
/// end, whether due to explicit disconnection, network issues, or application shutdown.
/// This is essential for releasing hardware resources, saving state, and graceful shutdown
/// of background operations.
/// </para>
/// <para>
/// Teardown methods are executed in reverse declaration order within a class,
/// with derived class teardown methods running before base class teardown methods.
/// This ensures proper cleanup hierarchy and dependency management.
/// </para>
/// <para>
/// Teardown execution characteristics:
/// <list type="bullet">
/// <item><description>Always executes, even if setup or other operations failed</description></item>
/// <item><description>Has limited time to complete before forcible disconnection</description></item>
/// <item><description>Should be robust against partial initialization states</description></item>
/// <item><description>Failures are logged but do not prevent disconnection</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Hardware Resource Cleanup</strong></para>
/// <code>
/// public class MotorController : Device
/// {
///     [Setup]
///     private async Task InitializeMotorsAsync()
///     {
///         await ExecuteAsync(@"
///             import machine
///             motor_left = machine.PWM(machine.Pin(12))
///             motor_right = machine.PWM(machine.Pin(13))
///             motor_left.freq(1000)
///             motor_right.freq(1000)
///         ");
///     }
/// 
///     [Teardown]
///     private async Task StopMotorsAsync()
///     {
///         await ExecuteAsync(@"
///             # Safely stop motors before disconnect
///             try:
///                 if 'motor_left' in globals():
///                     motor_left.duty_u16(0)
///                     motor_left.deinit()
///                 if 'motor_right' in globals():
///                     motor_right.duty_u16(0)
///                     motor_right.deinit()
///                 print('Motors stopped safely')
///             except Exception as e:
///                 print(f'Motor cleanup error: {e}')
///         ");
///     }
/// }
/// </code>
/// <para><strong>State Persistence</strong></para>
/// <code>
/// public class DataLogger : Device
/// {
///     [Teardown]
///     private async Task SaveDataAsync()
///     {
///         await ExecuteAsync(@"
///             import json
///             
///             try:
///                 # Save any pending data before disconnect
///                 if 'pending_data' in globals() and pending_data:
///                     with open('data_backup.json', 'w') as f:
///                         json.dump(pending_data, f)
///                     print(f'Saved {len(pending_data)} pending records')
///                 
///                 # Update status file
///                 status = {
///                     'last_disconnect': time.time(),
///                     'clean_shutdown': True
///                 }
///                 with open('status.json', 'w') as f:
///                     json.dump(status, f)
///                     
///             except Exception as e:
///                 print(f'Data save error: {e}')
///         ");
///     }
/// }
/// </code>
/// <para><strong>Multi-Stage Teardown</strong></para>
/// <code>
/// public class ComplexDevice : Device
/// {
///     [Teardown(Order = 1)] // Execute first
///     private async Task StopBackgroundTasksAsync()
///     {
///         await ExecuteAsync(@"
///             # Stop background threads
///             monitoring_active = False
///             data_collection_active = False
///             
///             # Wait briefly for threads to notice
///             import time
///             time.sleep_ms(100)
///         ");
///     }
/// 
///     [Teardown(Order = 2)] // Execute second
///     private async Task SaveStateAsync()
///     {
///         await ExecuteAsync(@"
///             # Save current state
///             save_device_state()
///             flush_data_buffers()
///         ");
///     }
/// 
///     [Teardown(Order = 3)] // Execute last
///     private async Task CleanupHardwareAsync()
///     {
///         await ExecuteAsync(@"
///             # Final hardware cleanup
///             disable_all_outputs()
///             release_hardware_resources()
///         ");
///     }
/// }
/// </code>
/// <para><strong>Error-Resilient Teardown</strong></para>
/// <code>
/// public class RobustDevice : Device
/// {
///     [Teardown(IgnoreErrors = true)]
///     private async Task BestEffortCleanupAsync()
///     {
///         await ExecuteAsync(@"
///             # Clean up everything we can, ignore individual failures
///             cleanup_tasks = [
///                 lambda: cleanup_sensors(),
///                 lambda: stop_background_threads(),
///                 lambda: save_critical_data(),
///                 lambda: disable_hardware()
///             ]
///             
///             for task in cleanup_tasks:
///                 try:
///                     task()
///                 except Exception as e:
///                     print(f'Cleanup task failed: {e}')
///             
///             print('Best-effort cleanup completed')
///         ");
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TeardownAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TeardownAttribute"/> class.
    /// </summary>
    public TeardownAttribute()
    {
    }

    /// <summary>
    /// Gets or sets the order in which this teardown method should be executed
    /// relative to other teardown methods in the same class.
    /// </summary>
    /// <value>
    /// The execution order. Methods with lower order values execute first.
    /// Methods with the same order value execute in reverse declaration order.
    /// Default is 0.
    /// </value>
    /// <remarks>
    /// <para>
    /// Use the Order property when you have multiple teardown methods that must
    /// execute in a specific sequence. Typically, you want to stop background
    /// operations before saving data, and save data before releasing hardware.
    /// </para>
    /// <para>
    /// Teardown methods in derived classes always execute before those in base
    /// classes, regardless of order values.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class SensorArray : Device
    /// {
    ///     [Teardown(Order = 1)]
    ///     private async Task StopCollectionAsync()
    ///     {
    ///         // Stop data collection first
    ///         await ExecuteAsync("stop_all_sensors()");
    ///     }
    /// 
    ///     [Teardown(Order = 2)]
    ///     private async Task SaveDataAsync()
    ///     {
    ///         // Save collected data second
    ///         await ExecuteAsync("save_buffered_data()");
    ///     }
    /// 
    ///     [Teardown(Order = 3)]
    ///     private async Task PowerDownAsync()
    ///     {
    ///         // Power down hardware last
    ///         await ExecuteAsync("power_down_sensors()");
    ///     }
    /// }
    /// </code>
    /// </example>
    public int Order { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether errors in this teardown method should be ignored.
    /// When true, exceptions from this method will be logged but will not prevent
    /// other teardown methods from executing or the disconnection from proceeding.
    /// </summary>
    /// <value>
    /// <c>true</c> if errors should be ignored; otherwise, <c>false</c>.
    /// Default is <c>false</c> to ensure teardown problems are noticed.
    /// </value>
    /// <remarks>
    /// <para>
    /// Set IgnoreErrors to true for teardown operations that are helpful but not
    /// critical, such as logging final state or cleaning up optional resources.
    /// Always keep it false for operations that could leave the device in an
    /// unsafe state if they fail.
    /// </para>
    /// <para>
    /// Even when errors are ignored, they are still logged for diagnostic purposes.
    /// Teardown methods should still implement their own error handling for
    /// non-critical operations.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Teardown(IgnoreErrors = true)]
    /// private async Task LogFinalStateAsync()
    /// {
    ///     // Nice to have, but not critical if it fails
    ///     await ExecuteAsync("log_final_device_state()");
    /// }
    /// 
    /// [Teardown(IgnoreErrors = false)]
    /// private async Task EmergencyStopAsync()
    /// {
    ///     // Critical safety operation - must succeed
    ///     await ExecuteAsync("emergency_stop_all_actuators()");
    /// }
    /// </code>
    /// </example>
    public bool IgnoreErrors { get; set; } = false;

    /// <summary>
    /// Gets or sets the timeout for teardown method execution in milliseconds.
    /// Teardown operations have limited time to complete before forcible disconnection.
    /// </summary>
    /// <value>
    /// The timeout in milliseconds, or <c>null</c> to use the default teardown timeout.
    /// Must be a positive value if specified.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when setting a timeout value that is less than or equal to zero.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Teardown operations should complete quickly to avoid delaying disconnection.
    /// The default timeout is typically much shorter than normal operation timeouts
    /// to ensure responsive disconnection behavior.
    /// </para>
    /// <para>
    /// Use custom timeouts sparingly and only for operations that genuinely need
    /// more time, such as saving large amounts of data or performing complex
    /// hardware shutdown sequences.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Teardown(TimeoutMs = 5000)] // 5 seconds for data save
    /// private async Task SaveLargeDatasetAsync()
    /// {
    ///     await ExecuteAsync(@"
    ///         # Save large dataset with progress indication
    ///         if 'large_dataset' in globals():
    ///             print('Saving large dataset...')
    ///             save_dataset_to_flash(large_dataset)
    ///             print('Dataset saved successfully')
    ///     ");
    /// }
    /// 
    /// [Teardown] // Use default timeout for quick operations
    /// private async Task QuickCleanupAsync()
    /// {
    ///     await ExecuteAsync("quick_cleanup()");
    /// }
    /// </code>
    /// </example>
    public int? TimeoutMs
    {
        get => _timeoutMs;
        set
        {
            if (value.HasValue && value.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value),
                    "Timeout must be a positive value in milliseconds");
            }
            _timeoutMs = value;
        }
    }
    private int? _timeoutMs;

    /// <summary>
    /// Gets or sets whether this teardown method is critical and must execute
    /// even in emergency disconnection scenarios.
    /// </summary>
    /// <value>
    /// <c>true</c> if this is a critical teardown operation; otherwise, <c>false</c>.
    /// Default is <c>false</c> to allow fast disconnection in normal cases.
    /// </value>
    /// <remarks>
    /// <para>
    /// Critical teardown operations are executed even when the disconnection
    /// is forced due to timeouts, errors, or emergency shutdown. Use this flag
    /// only for operations that are essential for safety or data integrity.
    /// </para>
    /// <para>
    /// Critical operations should be very fast and robust, as they may execute
    /// in adverse conditions where normal communication is already compromised.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Teardown(Critical = true)]
    /// private async Task EmergencyShutdownAsync()
    /// {
    ///     // Always execute, even in emergency disconnect
    ///     await ExecuteAsync(@"
    ///         # Critical safety shutdown
    ///         try:
    ///             emergency_stop()
    ///             disable_all_power()
    ///         except:
    ///             pass  # Must not throw in critical teardown
    ///     ");
    /// }
    /// </code>
    /// </example>
    public bool Critical { get; set; } = false;

    /// <summary>
    /// Returns a string that represents the current <see cref="TeardownAttribute"/>.
    /// </summary>
    /// <returns>A string that represents the current attribute configuration.</returns>
    public override string ToString()
    {
        var parts = new List<string>();

        if (Order != 0)
            parts.Add($"Order={Order}");

        if (IgnoreErrors)
            parts.Add("IgnoreErrors=true");

        if (TimeoutMs.HasValue)
            parts.Add($"TimeoutMs={TimeoutMs}");

        if (Critical)
            parts.Add("Critical=true");

        return parts.Count > 0 ? $"[Teardown({string.Join(", ", parts)})]" : "[Teardown]";
    }
}