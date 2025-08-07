# Hardware Testing Guide for Belay.NET

This guide provides comprehensive instructions for validating the Belay.NET implementation with real MicroPython and CircuitPython devices after completing foundation unit tests.

## Prerequisites

- Completed Belay.NET foundation implementation with all unit tests passing
- .NET 6+ SDK installed on target testing machine
- Access to physical MicroPython/CircuitPython hardware

## Required Hardware

### Primary Testing Hardware (Required)

#### Raspberry Pi Pico
- **MicroPython Version**: Raspberry Pi Pico with MicroPython firmware v1.20+
- **CircuitPython Version**: Raspberry Pi Pico with CircuitPython firmware v8.0+
- **Rationale**: Most widely used, stable platform with excellent USB CDC support
- **Connection**: USB-C to USB-A cable
- **Identification**: Appears as `/dev/ttyACM0` (Linux), `COMx` (Windows), `/dev/cu.usbmodem*` (macOS)

#### ESP32 Development Board
- **Model**: ESP32-DevKitC or ESP32-WROOM-32 based board
- **MicroPython Version**: v1.20+ with ESP32 port
- **CircuitPython Version**: v8.0+ (limited ESP32 support)
- **Rationale**: Tests wireless-capable platform with different USB-Serial implementation
- **Connection**: USB-C or Micro-USB cable (board dependent)
- **Identification**: Appears as `/dev/ttyUSB0` (Linux), `COMx` (Windows), `/dev/cu.usbserial-*` (macOS)

### Secondary Testing Hardware (Recommended)

#### STM32-based Pyboard
- **Model**: Pyboard v1.1 or MicroPython Pyboard D-series
- **MicroPython Version**: v1.20+ 
- **Rationale**: Reference implementation platform with native USB CDC
- **Connection**: Micro-USB cable
- **Special Features**: Tests original MicroPython platform design

#### Adafruit CircuitPython Boards
- **Models**: Feather M4 Express, Metro M4 Express, or similar
- **CircuitPython Version**: v8.0+
- **Rationale**: Tests CircuitPython-specific protocol variations
- **Connection**: USB-C or Micro-USB (board dependent)

## Hardware Setup Procedures

### 1. Firmware Installation

#### MicroPython Installation
```bash
# Download firmware from https://micropython.org/download/
# For Raspberry Pi Pico:
# 1. Hold BOOTSEL button while connecting USB
# 2. Copy .uf2 file to mounted RPI-RP2 drive
# 3. Device will reboot automatically

# For ESP32:
pip install esptool
esptool.py --chip esp32 --port /dev/ttyUSB0 erase_flash
esptool.py --chip esp32 --port /dev/ttyUSB0 write_flash -z 0x1000 firmware.bin
```

#### CircuitPython Installation
```bash
# Download firmware from https://circuitpython.org/downloads
# For Raspberry Pi Pico:
# 1. Hold BOOTSEL button while connecting USB
# 2. Copy .uf2 file to mounted RPI-RP2 drive
# 3. Device will reboot and appear as CIRCUITPY drive
```

### 2. Connection Verification

#### Linux Connection Test
```bash
# List available serial ports
ls -la /dev/tty* | grep -E "(USB|ACM)"

# Test basic communication
screen /dev/ttyACM0 115200
# Press Ctrl-C to get REPL prompt
# Type: print("Hello from MicroPython")
# Press Ctrl-A, K, Y to exit screen
```

#### Windows Connection Test
```powershell
# List COM ports
Get-WmiObject Win32_SerialPort | Select-Object DeviceID,Description

# Use PuTTY or Windows Terminal for testing:
# Connect to appropriate COMx port at 115200 baud
# Press Ctrl-C to get REPL prompt
# Type: print("Hello from MicroPython")
```

#### macOS Connection Test
```bash
# List available serial ports
ls -la /dev/cu.*

# Test basic communication
screen /dev/cu.usbmodem* 115200
# Press Ctrl-C to get REPL prompt  
# Type: print("Hello from MicroPython")
# Press Ctrl-A, K, Y to exit screen
```

### 3. Device Configuration

#### Enable Raw REPL Testing
Create `/test_setup.py` on the device root filesystem:

```python
# test_setup.py - MicroPython/CircuitPython test configuration
import sys
import gc

# Display system information for validation
print(f"MicroPython Version: {sys.version}")
print(f"Platform: {sys.platform}")
print(f"Implementation: {sys.implementation.name}")
print(f"Free Memory: {gc.mem_free()} bytes")

# Test functions for validation
def test_basic_execution():
    return "basic_execution_success"

def test_exception_handling():
    raise ValueError("Test exception for error handling validation")

def test_large_output():
    return "x" * 1000  # Test protocol buffer handling

def test_multiline_code():
    result = []
    for i in range(5):
        result.append(f"Line {i}")
    return result

# Memory and performance test functions
def test_memory_usage():
    import gc
    gc.collect()
    return gc.mem_free()

def test_timing():
    import time
    start = time.ticks_ms()
    # Simulate some work
    for i in range(1000):
        pass
    end = time.ticks_ms()
    return time.ticks_diff(end, start)
```

## Testing Protocols and Validation Procedures

### 1. Device Factory Pattern Validation

#### Test Connection String Parsing
```csharp
// Test file: DeviceFactoryHardwareTests.cs
[Test]
[Category("Hardware")]
public async Task TestSerialConnectionStringParsing()
{
    // Windows format
    var device1 = Device.Create("serial://COM3?baudrate=115200");
    Assert.IsInstanceOf<SerialDevice>(device1);
    
    // Linux format  
    var device2 = Device.Create("serial:///dev/ttyACM0?baudrate=115200");
    Assert.IsInstanceOf<SerialDevice>(device2);
    
    // macOS format
    var device3 = Device.Create("serial:///dev/cu.usbmodem143201?baudrate=115200");
    Assert.IsInstanceOf<SerialDevice>(device3);
}
```

#### Test Device Discovery
```csharp
[Test]
[Category("Hardware")]
public void TestDeviceDiscovery()
{
    var devices = Device.DiscoverSerialDevices();
    Assert.IsTrue(devices.Count > 0, "No MicroPython devices found");
    
    foreach (var port in devices)
    {
        Console.WriteLine($"Found device: {port}");
    }
}
```

### 2. Raw REPL Protocol Communication Validation

#### Protocol State Management Tests
```csharp
[Test]
[Category("Hardware")]
public async Task TestRawReplStateTransitions()
{
    using var device = Device.Create(GetTestDeviceConnectionString());
    await device.ConnectAsync();
    
    // Test entering raw mode
    await device.EnterRawModeAsync();
    Assert.AreEqual(ReplState.Raw, device.CurrentState);
    
    // Test executing code in raw mode
    var result = await device.ExecuteAsync<string>("print('raw_mode_test')");
    Assert.AreEqual("raw_mode_test", result);
    
    // Test exiting raw mode
    await device.ExitRawModeAsync();
    Assert.AreEqual(ReplState.Normal, device.CurrentState);
}
```

#### Flow Control Validation
```csharp
[Test]
[Category("Hardware")]
public async Task TestFlowControlHandling()
{
    using var device = Device.Create(GetTestDeviceConnectionString());
    await device.ConnectAsync();
    
    // Test large code execution requiring flow control
    var largeCode = "result = " + string.Join(" + ", Enumerable.Range(1, 100));
    var result = await device.ExecuteAsync<int>(largeCode + "; print(result)");
    
    Assert.AreEqual(5050, result); // Sum of 1 to 100
}
```

#### Raw-Paste Mode Validation
```csharp
[Test]
[Category("Hardware")]
public async Task TestRawPasteModeProtocol()
{
    using var device = Device.Create(GetTestDeviceConnectionString());
    await device.ConnectAsync();
    
    // Multi-line code requiring raw-paste mode
    var multilineCode = @"
def calculate_fibonacci(n):
    if n <= 1:
        return n
    return calculate_fibonacci(n-1) + calculate_fibonacci(n-2)

result = calculate_fibonacci(10)
print(result)";
    
    var result = await device.ExecuteAsync<int>(multilineCode);
    Assert.AreEqual(55, result); // 10th Fibonacci number
}
```

### 3. Serial Device Communication Implementation Tests

#### Connection Resilience
```csharp
[Test]
[Category("Hardware")]
public async Task TestConnectionResilience()
{
    using var device = Device.Create(GetTestDeviceConnectionString());
    
    // Test initial connection
    await device.ConnectAsync();
    Assert.IsTrue(device.IsConnected);
    
    // Simulate disconnect/reconnect cycle
    await device.DisconnectAsync();
    Assert.IsFalse(device.IsConnected);
    
    await device.ConnectAsync();
    Assert.IsTrue(device.IsConnected);
    
    // Verify device still works after reconnection
    var result = await device.ExecuteAsync<string>("print('reconnect_test')");
    Assert.AreEqual("reconnect_test", result);
}
```

#### Timeout and Cancellation Handling
```csharp
[Test]
[Category("Hardware")]
public async Task TestTimeoutHandling()
{
    using var device = Device.Create(GetTestDeviceConnectionString());
    await device.ConnectAsync();
    
    // Test operation timeout
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
    
    var ex = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
    {
        // Code that takes longer than 2 seconds
        await device.ExecuteAsync("import time; time.sleep(5)", cts.Token);
    });
}
```

### 4. Error Handling and Reconnection Logic Tests

#### Exception Mapping Validation
```csharp
[Test]
[Category("Hardware")]
public async Task TestPythonExceptionMapping()
{
    using var device = Device.Create(GetTestDeviceConnectionString());
    await device.ConnectAsync();
    
    // Test ValueError mapping
    var ex = await Assert.ThrowsAsync<DeviceExecutionException>(async () =>
    {
        await device.ExecuteAsync("raise ValueError('Test error')");
    });
    
    Assert.Contains("ValueError", ex.Message);
    Assert.Contains("Test error", ex.Message);
}
```

#### Automatic Reconnection Logic
```csharp
[Test]
[Category("Hardware")]
public async Task TestAutomaticReconnection()
{
    var deviceConfig = new DeviceConfiguration
    {
        ConnectionString = GetTestDeviceConnectionString(),
        AutoReconnect = true,
        ReconnectAttempts = 3,
        ReconnectDelay = TimeSpan.FromSeconds(1)
    };
    
    using var device = Device.Create(deviceConfig);
    await device.ConnectAsync();
    
    // Simulate connection loss (would need physical disconnect/reconnect in real test)
    // This test validates the retry logic configuration
    Assert.IsTrue(device.IsConfiguredForAutoReconnect);
}
```

## Platform-Specific Considerations

### Windows Serial Port Handling

#### COM Port Enumeration
```csharp
// Windows-specific device discovery
public static class WindowsDeviceDiscovery
{
    public static List<string> GetMicroPythonPorts()
    {
        var ports = new List<string>();
        var searcher = new ManagementObjectSearcher(
            "SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'");
        
        foreach (ManagementObject queryObj in searcher.Get())
        {
            var caption = queryObj["Caption"]?.ToString();
            if (caption != null && (caption.Contains("USB") || caption.Contains("Serial")))
            {
                var match = Regex.Match(caption, @"COM(\d+)");
                if (match.Success)
                {
                    ports.Add($"COM{match.Groups[1].Value}");
                }
            }
        }
        return ports;
    }
}
```

#### Windows Permission Issues
- **Requirement**: Run tests with appropriate user permissions
- **Issue**: COM port access may require administrator privileges
- **Solution**: Add user to "Dialout" equivalent group or run as administrator

### Linux Serial Port Handling

#### Device Permission Configuration
```bash
# Add user to dialout group for serial port access
sudo usermod -a -G dialout $USER
# Logout and login again for changes to take effect

# Alternative: Set specific device permissions
sudo chmod 666 /dev/ttyACM0
sudo chmod 666 /dev/ttyUSB0
```

#### udev Rules for Consistent Device Naming
```bash
# Create /etc/udev/rules.d/99-micropython.rules
SUBSYSTEM=="tty", ATTRS{idVendor}=="2e8a", ATTRS{idProduct}=="0005", SYMLINK+="micropython-pico"
SUBSYSTEM=="tty", ATTRS{idVendor}=="10c4", ATTRS{idProduct}=="ea60", SYMLINK+="micropython-esp32"

# Reload udev rules
sudo udevadm control --reload-rules
sudo udevadm trigger
```

### macOS Serial Port Handling

#### System Extension Requirements
- **Issue**: macOS may block access to USB serial devices
- **Solution**: Grant terminal emulator applications full disk access in System Preferences

#### Device Path Patterns
```csharp
// macOS device path detection
public static class MacOSDeviceDiscovery
{
    public static List<string> GetMicroPythonPorts()
    {
        var ports = Directory.GetFiles("/dev", "cu.*")
            .Where(p => p.Contains("usbmodem") || p.Contains("usbserial"))
            .ToList();
        
        return ports;
    }
}
```

## Common Hardware Issues and Troubleshooting

### Connection Issues

#### Device Not Detected
**Symptoms**: Device doesn't appear in port listings
**Causes**:
- USB cable is power-only (no data lines)
- Driver issues on Windows
- Insufficient permissions on Linux/macOS
- Device in bootloader mode instead of runtime mode

**Solutions**:
```bash
# Linux: Check for device detection
dmesg | tail -20
lsusb | grep -i "micro\|circuit\|pi"

# Windows: Check Device Manager for unknown devices
# macOS: Check System Information > USB
```

#### Permission Denied Errors
**Symptoms**: "Access denied" or "Permission denied" when opening serial port
**Solutions**:
```bash
# Linux: Add user to dialout group
sudo usermod -a -G dialout $(whoami)
# Logout/login required

# Temporary fix
sudo chmod 666 /dev/ttyACM0
```

### Communication Protocol Issues

#### Raw REPL Entry Failures
**Symptoms**: Device doesn't respond to Ctrl-A sequence
**Causes**:
- Device already in raw mode
- Incorrect baud rate
- Device is executing blocking code

**Solutions**:
```csharp
// Recovery sequence
await device.SendBreakAsync();           // Interrupt any running code
await device.SendAsync("\x02");         // Exit raw mode if already in it
await Task.Delay(100);
await device.SendAsync("\x01");         // Enter raw mode
```

#### Flow Control Problems
**Symptoms**: Large code transfers fail or timeout
**Causes**:
- Window size mismanagement
- Missing flow control byte handling
- Buffer overflow on device

**Solutions**:
- Implement proper window size tracking
- Respect device flow control signals
- Add configurable timeouts for different operation types

#### Character Encoding Issues
**Symptoms**: Non-ASCII characters corrupted or cause errors
**Solutions**:
```csharp
// Always use UTF-8 encoding for MicroPython communication
var encoding = new UTF8Encoding(false); // No BOM
var bytes = encoding.GetBytes(pythonCode);
```

### Device-Specific Quirks

#### Raspberry Pi Pico
- **Reset Behavior**: Automatically resets when serial connection opens
- **Boot Delay**: May require 1-2 second delay after connection before communication
- **Memory Constraints**: Limited heap space for large operations

#### ESP32 Boards
- **Boot Messages**: Sends boot information over UART0, may interfere with REPL
- **Reset Requirements**: May need hardware reset (EN pin) for reliable operation
- **Flash Mode**: Can accidentally enter download mode if boot pins are held

#### CircuitPython Differences
- **REPL Prompt**: Shows ">>>" instead of ">>>" after some operations
- **Auto-reload**: May restart when files change on CIRCUITPY drive
- **Module Differences**: Some MicroPython modules unavailable

## Validation Test Scenarios

### Manual Testing Procedures

#### 1. Basic Communication Test
```bash
# Connect to device via terminal
screen /dev/ttyACM0 115200

# Test sequence:
1. Press Ctrl-C (should see REPL prompt)
2. Type: print("Hello World")
3. Press Enter (should see output)
4. Press Ctrl-A (should see "raw REPL; CTRL-B to exit")
5. Type: print("Raw mode test")
6. Press Ctrl-D (should see output and "OK")
7. Press Ctrl-B (should return to normal REPL)
```

#### 2. Belay.NET Integration Test
```csharp
// Create comprehensive integration test
[Test]
[Category("Hardware")]
[Category("Manual")]
public async Task ComprehensiveDeviceValidation()
{
    var connectionString = GetTestDeviceConnectionString(); // Set manually for target device
    
    using var device = Device.Create(connectionString);
    
    // Test 1: Connection
    await device.ConnectAsync();
    Assert.IsTrue(device.IsConnected);
    
    // Test 2: Basic execution
    var result = await device.ExecuteAsync<string>("print('integration_test')");
    Assert.AreEqual("integration_test", result);
    
    // Test 3: Complex data types
    var listResult = await device.ExecuteAsync<List<int>>("print([1, 2, 3, 4, 5])");
    Assert.AreEqual(new List<int> {1, 2, 3, 4, 5}, listResult);
    
    // Test 4: Error handling
    var ex = await Assert.ThrowsAsync<DeviceExecutionException>(
        () => device.ExecuteAsync("raise RuntimeError('Test error')"));
    Assert.Contains("RuntimeError", ex.Message);
    
    // Test 5: Large data transfer
    var largeData = string.Join("", Enumerable.Range(0, 1000).Select(i => i.ToString()));
    var largeResult = await device.ExecuteAsync<string>($"print('{largeData}')");
    Assert.AreEqual(largeData, largeResult);
    
    // Test 6: Multiple rapid operations
    var tasks = Enumerable.Range(0, 10)
        .Select(i => device.ExecuteAsync<int>($"print({i})"))
        .ToArray();
    
    var results = await Task.WhenAll(tasks);
    Assert.AreEqual(Enumerable.Range(0, 10).ToArray(), results);
}
```

#### 3. Performance Benchmark Test
```csharp
[Test]
[Category("Hardware")]
[Category("Performance")]
public async Task PerformanceBenchmarks()
{
    using var device = Device.Create(GetTestDeviceConnectionString());
    await device.ConnectAsync();
    
    // Measure simple execution latency
    var stopwatch = Stopwatch.StartNew();
    for (int i = 0; i < 100; i++)
    {
        await device.ExecuteAsync<int>($"print({i})");
    }
    stopwatch.Stop();
    
    var averageLatency = stopwatch.ElapsedMilliseconds / 100.0;
    Console.WriteLine($"Average execution latency: {averageLatency:F2}ms");
    
    // Measure throughput with large data
    stopwatch.Restart();
    var largeCode = "result = " + string.Join(" + ", Enumerable.Range(1, 1000));
    await device.ExecuteAsync<int>(largeCode + "; print(result)");
    stopwatch.Stop();
    
    Console.WriteLine($"Large operation time: {stopwatch.ElapsedMilliseconds}ms");
}
```

### Automated Hardware Test Suite

Create a test configuration file for different hardware setups:

```json
{
  "hardware_configurations": [
    {
      "name": "RaspberryPi_Pico_MicroPython",
      "connection_string": "serial:///dev/ttyACM0?baudrate=115200",
      "firmware": "MicroPython v1.20+",
      "expected_platform": "rp2",
      "test_categories": ["basic", "performance", "memory"]
    },
    {
      "name": "ESP32_MicroPython", 
      "connection_string": "serial:///dev/ttyUSB0?baudrate=115200",
      "firmware": "MicroPython v1.20+",
      "expected_platform": "esp32",
      "test_categories": ["basic", "performance"]
    }
  ]
}
```

## Test Execution Commands

### Run Hardware Tests
```bash
# Run all hardware tests
dotnet test --filter "Category=Hardware" --logger console

# Run tests for specific device type
dotnet test --filter "Category=Hardware&Category=RaspberryPi" --logger console

# Run performance benchmarks
dotnet test --filter "Category=Performance" --logger console

# Generate detailed test report
dotnet test --filter "Category=Hardware" --logger "trx;LogFileName=hardware_tests.trx"
```

### Continuous Hardware Testing
```bash
# Set up for CI/CD with hardware in the loop
export BELAY_TEST_DEVICE="serial:///dev/ttyACM0?baudrate=115200"
dotnet test --filter "Category=Hardware" --configuration Release
```

## Success Criteria

A successful hardware validation should demonstrate:

1. **Connection Management**: Reliable connection/disconnection cycles
2. **Protocol Compliance**: Proper Raw REPL and Raw-Paste mode implementation
3. **Error Handling**: Correct mapping of device exceptions to .NET exceptions
4. **Performance**: Acceptable latency (<100ms for simple operations)
5. **Reliability**: 100% success rate for basic operations over 100 iterations
6. **Cross-Platform**: Tests pass on Windows, Linux, and macOS
7. **Multi-Device**: Tests pass on both MicroPython and CircuitPython devices
8. **Recovery**: Graceful handling of connection loss and automatic reconnection

After successful hardware validation, the implementation is ready for integration into larger applications and advanced feature development.