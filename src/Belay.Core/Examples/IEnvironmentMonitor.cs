// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Examples;

using System.Threading.Tasks;
using Belay.Attributes;

/// <summary>
/// Interface demonstrating comprehensive attribute-driven device programming.
/// This environmental monitoring device showcases the complete lifecycle using
/// [Setup], [Task], [Thread], and [Teardown] attributes with method interception.
/// </summary>
/// <remarks>
/// <para>
/// This interface shows how to build complex device behavior using only attributes
/// and Python code embedding, eliminating the need for manual ExecuteAsync calls.
/// The method interception system automatically handles Python code execution.
/// </para>
/// </remarks>
public interface IEnvironmentMonitor {
    /// <summary>
    /// Initialize hardware pins and basic sensor configuration.
    /// This runs first during device connection.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Setup(Order = 1)]
    [PythonCode(@"
        import machine
        import time
        
        # Configure analog pins for sensors
        temp_pin = machine.Pin(26)  # Temperature sensor (ADC0)
        light_pin = machine.Pin(27) # Light sensor (ADC1) 
        
        # Initialize ADC with proper attenuation for 3.3V range
        temp_adc = machine.ADC(temp_pin)
        light_adc = machine.ADC(light_pin)
        temp_adc.atten(machine.ADC.ATTN_11DB)
        light_adc.atten(machine.ADC.ATTN_11DB)
        
        # Configure I2C for humidity sensor (SHT30)
        i2c = machine.I2C(0, scl=machine.Pin(22), sda=machine.Pin(21), freq=400000)
        
        print('Hardware initialized successfully')
    ")]
    Task InitializeHardwareAsync();

    /// <summary>
    /// Load calibration data and configure sensor parameters.
    /// This runs after hardware initialization.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Setup(Order = 2)]
    [PythonCode(@"
        import json
        
        # Default calibration values
        calibration = {
            'temp_offset': 0.0,
            'temp_scale': 1.0,
            'light_scale': 1.0,
            'humidity_offset': 0.0
        }
        
        # Try to load saved calibration
        try:
            with open('calibration.json', 'r') as f:
                saved_cal = json.loads(f.read())
                calibration.update(saved_cal)
            print('Calibration data loaded')
        except:
            print('Using default calibration values')
        
        # Store in global scope for use by other methods
        globals()['sensor_calibration'] = calibration
    ")]
    Task LoadCalibrationAsync();

    /// <summary>
    /// Initialize global monitoring state and data structures.
    /// This runs last in the setup sequence.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Setup(Order = 3, Critical = false)]
    [PythonCode(@"
        # Global monitoring state
        monitoring_active = False
        data_buffer = []
        alert_thresholds = {
            'max_temp': 35.0,
            'min_temp': 0.0,
            'max_humidity': 90.0,
            'min_light': 10
        }
        
        # Statistics tracking
        reading_count = 0
        error_count = 0
        last_reading_time = 0
        
        print('Monitoring state initialized')
    ")]
    Task InitializeMonitoringStateAsync();

    /// <summary>
    /// Read current environmental conditions from all sensors.
    /// </summary>
    /// <returns>Current environmental reading with temperature, humidity, and light levels.</returns>
    [Task]
    [PythonCode(@"
        import json
        import time
        
        def read_temperature():
            # Read temperature sensor (TMP36 or similar)
            raw = temp_adc.read_u16()
            voltage = (raw / 65535.0) * 3.3
            # TMP36: 10mV/°C with 500mV offset at 0°C
            temp_c = (voltage - 0.5) * 100
            # Apply calibration
            temp_c = temp_c * sensor_calibration['temp_scale'] + sensor_calibration['temp_offset']
            return round(temp_c, 2)
        
        def read_humidity():
            # Read humidity from SHT30 via I2C
            try:
                # SHT30 command: measure with high repeatability
                i2c.writeto(0x44, bytes([0x2C, 0x06]))
                time.sleep_ms(15)  # Wait for measurement
                data = i2c.readfrom(0x44, 6)
                
                # Convert raw data to humidity percentage
                temp_raw = (data[0] << 8) | data[1]
                humid_raw = (data[3] << 8) | data[4]
                humidity = (humid_raw * 100.0) / 65535.0
                
                # Apply calibration
                humidity += sensor_calibration['humidity_offset']
                return round(max(0, min(100, humidity)), 1)
            except:
                # Fallback: estimate from temperature (very rough)
                temp = read_temperature()
                return max(30, min(70, 60 - temp * 0.5))
        
        def read_light_level():
            # Read light sensor (photoresistor or photodiode)
            raw = light_adc.read_u16()
            # Convert to 0-100 light level scale
            light = (raw / 65535.0) * 100 * sensor_calibration['light_scale']
            return round(light, 1)
        
        try:
            # Take readings
            temperature = read_temperature()
            humidity = read_humidity()
            light = read_light_level()
            timestamp = time.ticks_ms()
            
            # Update statistics
            globals()['reading_count'] = globals().get('reading_count', 0) + 1
            globals()['last_reading_time'] = timestamp
            
            # Return structured data
            reading = {
                'temperature': temperature,
                'humidity': humidity,
                'lightLevel': light,
                'timestamp': timestamp,
                'readingId': reading_count
            }
            
            json.dumps(reading)
            
        except Exception as e:
            globals()['error_count'] = globals().get('error_count', 0) + 1
            raise Exception(f'Sensor reading failed: {str(e)}')
    ")]
    Task<EnvironmentReading> GetCurrentReadingAsync();

    /// <summary>
    /// Perform sensor calibration routine.
    /// This should be done in known environmental conditions.
    /// </summary>
    /// <param name="referenceTemp">Known temperature for calibration.</param>
    /// <param name="referenceHumidity">Known humidity for calibration.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Task(TimeoutMs = 30000)]
    [PythonCode(@"
        import json
        import time
        
        print('Starting sensor calibration...')
        
        # Take multiple readings for averaging
        temp_readings = []
        humidity_readings = []
        
        for i in range(10):
            # Read raw temperature
            raw = temp_adc.read_u16()
            voltage = (raw / 65535.0) * 3.3
            temp_c = (voltage - 0.5) * 100
            temp_readings.append(temp_c)
            
            # Read raw humidity (simplified)
            try:
                i2c.writeto(0x44, bytes([0x2C, 0x06]))
                time.sleep_ms(15)
                data = i2c.readfrom(0x44, 6)
                humid_raw = (data[3] << 8) | data[4]
                humidity = (humid_raw * 100.0) / 65535.0
                humidity_readings.append(humidity)
            except:
                humidity_readings.append({referenceHumidity})  # Use reference if sensor fails
            
            time.sleep_ms(500)  # Wait between readings
            print(f'Calibration reading {i+1}/10...')
        
        # Calculate averages
        avg_temp = sum(temp_readings) / len(temp_readings)
        avg_humidity = sum(humidity_readings) / len(humidity_readings)
        
        # Calculate calibration factors
        temp_offset = {referenceTemp} - avg_temp
        humidity_offset = {referenceHumidity} - avg_humidity
        
        # Update calibration
        sensor_calibration['temp_offset'] = temp_offset
        sensor_calibration['humidity_offset'] = humidity_offset
        
        # Save calibration to file
        with open('calibration.json', 'w') as f:
            json.dump(sensor_calibration, f)
        
        print(f'Calibration complete:')
        print(f'  Temperature offset: {temp_offset:.2f}°C')
        print(f'  Humidity offset: {humidity_offset:.1f}%')
    ")]
    Task CalibrateSensorsAsync(float referenceTemp = 25.0f, float referenceHumidity = 50.0f);

    /// <summary>
    /// Get device diagnostics and sensor health information.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Task]
    [PythonCode(
        @"
        import json
        import gc
        import machine
        import sys
        
        # Collect system diagnostics
        diagnostics = {
            'systemInfo': {
                'platform': sys.platform,
                'version': sys.version,
                'freqMHz': machine.freq() // 1000000,
                'freeMemory': gc.mem_free(),
                'resetCause': machine.reset_cause()
            },
            'sensorStatus': {
                'readingCount': globals().get('reading_count', 0),
                'errorCount': globals().get('error_count', 0),
                'lastReadingTime': globals().get('last_reading_time', 0),
                'monitoringActive': globals().get('monitoring_active', False)
            },
            'calibration': globals().get('sensor_calibration', {}),
            'alerts': globals().get('alert_thresholds', {})
        }
        
        # Add sensor health checks
        try:
            # Test temperature sensor
            temp_raw = temp_adc.read_u16()
            diagnostics['sensorStatus']['tempSensorRaw'] = temp_raw
            diagnostics['sensorStatus']['tempSensorOk'] = 1000 < temp_raw < 60000
            
            # Test light sensor  
            light_raw = light_adc.read_u16()
            diagnostics['sensorStatus']['lightSensorRaw'] = light_raw
            diagnostics['sensorStatus']['lightSensorOk'] = light_raw > 0
            
            # Test I2C humidity sensor
            i2c.scan()  # This will throw if I2C is not working
            diagnostics['sensorStatus']['humiditySensorOk'] = 0x44 in i2c.scan()
            
        except Exception as e:
            diagnostics['sensorStatus']['healthCheckError'] = str(e)
        
        json.dumps(diagnostics)
    ", EnableParameterSubstitution = false)]
    Task<Dictionary<string, object>> GetDiagnosticsAsync();

    /// <summary>
    /// Stop continuous monitoring thread.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Task]
    [PythonCode(@"
        print('Stopping continuous monitoring...')
        globals()['monitoring_active'] = False
        
        # Wait a moment for thread to notice
        import time
        time.sleep_ms(100)
        
        print('Monitoring stopped')
    ")]
    Task StopMonitoringAsync();

    /// <summary>
    /// Start continuous environmental monitoring in a background thread.
    /// Data is collected at the specified interval and can trigger alerts.
    /// </summary>
    /// <param name="intervalMs">Data collection interval in milliseconds.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Thread(Name = "env_monitor", AutoRestart = true)]
    [PythonCode(@"
        import _thread
        import time
        import json
        
        def continuous_monitor():
            print('Starting continuous environmental monitoring...')
            globals()['monitoring_active'] = True
            
            while globals().get('monitoring_active', False):
                try:
                    # Take sensor readings
                    temp_raw = temp_adc.read_u16()
                    temp_voltage = (temp_raw / 65535.0) * 3.3
                    temperature = (temp_voltage - 0.5) * 100
                    temperature = temperature * sensor_calibration['temp_scale'] + sensor_calibration['temp_offset']
                    
                    light_raw = light_adc.read_u16()
                    light = (light_raw / 65535.0) * 100 * sensor_calibration['light_scale']
                    
                    # Try humidity reading
                    try:
                        i2c.writeto(0x44, bytes([0x2C, 0x06]))
                        time.sleep_ms(15)
                        data = i2c.readfrom(0x44, 6)
                        humid_raw = (data[3] << 8) | data[4]
                        humidity = (humid_raw * 100.0) / 65535.0 + sensor_calibration['humidity_offset']
                    except:
                        humidity = 50.0  # Default if sensor fails
                    
                    # Update statistics
                    globals()['reading_count'] = globals().get('reading_count', 0) + 1
                    globals()['last_reading_time'] = time.ticks_ms()
                    
                    # Check alert thresholds
                    alerts = []
                    thresholds = alert_thresholds
                    
                    if temperature > thresholds['max_temp']:
                        alerts.append(f'High temperature: {temperature:.1f}°C')
                    elif temperature < thresholds['min_temp']:
                        alerts.append(f'Low temperature: {temperature:.1f}°C')
                        
                    if humidity > thresholds['max_humidity']:
                        alerts.append(f'High humidity: {humidity:.1f}%')
                        
                    if light < thresholds['min_light']:
                        alerts.append(f'Low light: {light:.1f}')
                    
                    # Print current readings
                    timestamp = time.ticks_ms()
                    print(f'[{timestamp}] T:{temperature:.1f}°C H:{humidity:.1f}% L:{light:.1f}')
                    
                    # Print alerts if any
                    for alert in alerts:
                        print(f'ALERT: {alert}')
                    
                    # Add to data buffer (keep last 100 readings)
                    if 'data_buffer' not in globals():
                        globals()['data_buffer'] = []
                    
                    data_buffer.append({
                        'temp': round(temperature, 1),
                        'humidity': round(humidity, 1), 
                        'light': round(light, 1),
                        'timestamp': timestamp,
                        'alerts': alerts
                    })
                    
                    # Keep buffer size manageable
                    if len(data_buffer) > 100:
                        data_buffer.pop(0)
                    
                    # Wait for next reading
                    time.sleep_ms({intervalMs})
                    
                except Exception as e:
                    print(f'Monitoring error: {e}')
                    globals()['error_count'] = globals().get('error_count', 0) + 1
                    time.sleep_ms(5000)  # Back off on error
            
            print('Continuous monitoring stopped')
        
        # Start the monitoring thread
        _thread.start_new_thread(continuous_monitor, ())
    ")]
    Task StartContinuousMonitoringAsync(int intervalMs = 5000);

    /// <summary>
    /// Start a watchdog thread that monitors system health.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Thread(Name = "health_watchdog", Priority = ThreadPriority.High)]
    [PythonCode(
        @"
        import _thread
        import time
        import gc
        
        def health_watchdog():
            print('Starting health watchdog...')
            last_check = time.ticks_ms()
            
            while globals().get('monitoring_active', False):
                try:
                    current_time = time.ticks_ms()
                    
                    # Check memory usage
                    free_mem = gc.mem_free()
                    if free_mem < 2000:
                        print(f'WARNING: Low memory: {free_mem} bytes')
                        gc.collect()  # Force garbage collection
                    
                    # Check if main monitoring is still running
                    last_reading = globals().get('last_reading_time', 0)
                    if time.ticks_diff(current_time, last_reading) > 30000:  # 30 second timeout
                        print('WARNING: Main monitoring appears stalled')
                    
                    # Check error rate
                    total_readings = globals().get('reading_count', 1)
                    total_errors = globals().get('error_count', 0) 
                    error_rate = total_errors / total_readings
                    if error_rate > 0.1:  # More than 10% errors
                        print(f'WARNING: High error rate: {error_rate:.1%}')
                    
                    # Health report every 60 seconds
                    if time.ticks_diff(current_time, last_check) > 60000:
                        print(f'Health: {total_readings} readings, {total_errors} errors, {free_mem} bytes free')
                        last_check = current_time
                    
                    time.sleep_ms(10000)  # Check every 10 seconds
                    
                except Exception as e:
                    print(f'Watchdog error: {e}')
                    time.sleep_ms(15000)  # Back off on watchdog errors
            
            print('Health watchdog stopped')
        
        _thread.start_new_thread(health_watchdog, ())
    ", EnableParameterSubstitution = false)]
    Task StartHealthWatchdogAsync();

    /// <summary>
    /// Stop all background monitoring threads.
    /// This runs first during disconnection.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Teardown(Order = 1)]
    [PythonCode(@"
        print('Stopping background threads...')
        
        # Signal all threads to stop
        globals()['monitoring_active'] = False
        
        # Wait for threads to notice and shut down
        import time
        time.sleep_ms(500)
        
        print('Background threads stopped')
    ")]
    Task StopBackgroundThreadsAsync();

    /// <summary>
    /// Save current data buffer and statistics to persistent storage.
    /// This runs after stopping threads but before hardware cleanup.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Teardown(Order = 2, IgnoreErrors = true)]
    [PythonCode(
        @"
        import json
        import time
        
        try:
            # Save data buffer if it exists
            if 'data_buffer' in globals() and data_buffer:
                backup_data = {
                    'readings': data_buffer,
                    'statistics': {
                        'totalReadings': globals().get('reading_count', 0),
                        'totalErrors': globals().get('error_count', 0),
                        'lastDisconnect': time.ticks_ms()
                    }
                }
                
                with open('sensor_backup.json', 'w') as f:
                    json.dump(backup_data, f)
                
                print(f'Saved {len(data_buffer)} readings to backup file')
            
            # Save device status
            status = {
                'lastDisconnect': time.ticks_ms(),
                'cleanShutdown': True,
                'readingCount': globals().get('reading_count', 0),
                'errorCount': globals().get('error_count', 0)
            }
            
            with open('device_status.json', 'w') as f:
                json.dump(status, f)
            
        except Exception as e:
            print(f'Data save warning: {e}')
    ", EnableParameterSubstitution = false)]
    Task SaveDataAsync();

    /// <summary>
    /// Clean up hardware resources and put sensors in safe state.
    /// This runs last during disconnection.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Teardown(Order = 3)]
    [PythonCode(
        @"
        print('Cleaning up hardware...')
        
        try:
            # Clean up ADC resources
            if 'temp_adc' in globals():
                temp_adc = None
            if 'light_adc' in globals():
                light_adc = None
            
            # Clean up I2C
            if 'i2c' in globals():
                i2c = None
            
            # Clear global state
            globals().pop('monitoring_active', None)
            globals().pop('data_buffer', None)
            globals().pop('sensor_calibration', None)
            
            print('Hardware cleanup completed')
            
        except Exception as e:
            print(f'Hardware cleanup error: {e}')
    ", EnableParameterSubstitution = false)]
    Task CleanupHardwareAsync();
}

/// <summary>
/// Represents a single environmental sensor reading.
/// </summary>
public class EnvironmentReading {
    /// <summary>
    /// Gets or sets temperature in degrees Celsius.
    /// </summary>
    public float Temperature { get; set; }

    /// <summary>
    /// Gets or sets relative humidity as a percentage (0-100).
    /// </summary>
    public float Humidity { get; set; }

    /// <summary>
    /// Gets or sets light level as a percentage (0-100).
    /// </summary>
    public float LightLevel { get; set; }

    /// <summary>
    /// Gets or sets timestamp when the reading was taken (device ticks).
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// Gets or sets unique identifier for this reading.
    /// </summary>
    public int ReadingId { get; set; }

    /// <summary>
    /// Returns a string representation of the environmental reading.
    /// </summary>
    /// <returns></returns>
    public override string ToString() {
        return $"T: {this.Temperature:F1}°C, H: {this.Humidity:F1}%, L: {this.LightLevel:F1}% [ID: {this.ReadingId}]";
    }
}
