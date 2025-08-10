# ESP32 Hardware Validation Guide - Week 3

**Date**: August 10, 2025  
**Device**: ESP32 with MicroPython  
**Belay.NET Version**: v0.1.0 Foundation + Week 2 File Transfer  
**Objective**: Validate ESP32 hardware compatibility alongside Raspberry Pi Pico infrastructure

## Prerequisites

- ESP32 development board (ESP32, ESP32-S2, ESP32-S3, or ESP32-C3)
- USB-A to Micro-USB or USB-C cable (depending on board)
- Computer with .NET 8+ SDK installed
- Belay.NET codebase (current repository state)

## Step 1: Hardware Setup

### 1.1 Download MicroPython Firmware

```bash
# ESP32 (original)
wget https://micropython.org/resources/firmware/esp32-20241025-v1.24.0.bin

# ESP32-S2
wget https://micropython.org/resources/firmware/esp32s2-20241025-v1.24.0.bin

# ESP32-S3
wget https://micropython.org/resources/firmware/esp32s3-20241025-v1.24.0.bin

# ESP32-C3
wget https://micropython.org/resources/firmware/esp32c3-20241025-v1.24.0.bin
```

### 1.2 Install esptool (if not already installed)

```bash
# Install esptool for firmware flashing
pip install esptool

# Verify installation
esptool.py version
```

### 1.3 Flash Firmware to ESP32

1. **Connect ESP32:**
   - Connect ESP32 to computer via USB cable
   - Most ESP32 boards enter download mode automatically
   - Some boards may require holding BOOT button while connecting

2. **Identify serial port:**
   ```bash
   # Linux
   ls -la /dev/ttyUSB*  # or /dev/ttyACM*
   dmesg | tail -10     # Check connection messages
   
   # Windows
   # Check Device Manager for Silicon Labs CP210x or FTDI devices
   
   # macOS
   ls -la /dev/cu.usbserial-*  # or /dev/cu.SLAB_USBtoUART
   ```

3. **Flash firmware:**
   ```bash
   # Erase flash first (recommended)
   esptool.py --chip esp32 --port /dev/ttyUSB0 erase_flash
   
   # Flash MicroPython firmware
   esptool.py --chip esp32 --port /dev/ttyUSB0 --baud 460800 write_flash -z 0x1000 esp32-20241025-v1.24.0.bin
   
   # Windows example:
   esptool.py --chip esp32 --port COM3 --baud 460800 write_flash -z 0x1000 esp32-20241025-v1.24.0.bin
   ```

4. **Verify installation:**
   - ESP32 reboots after flashing
   - Device appears as serial port

### 1.4 Configure Permissions (Linux only)

```bash
# Add user to dialout group for permanent access
sudo usermod -a -G dialout $USER
# Log out and back in for changes to take effect

# Or temporary permission:
sudo chmod 666 /dev/ttyUSB0
```

## Step 2: Basic Connection Verification

### 2.1 Terminal Test

```bash
# Linux/macOS
screen /dev/ttyUSB0 115200

# Windows - use PuTTY with COMx at 115200 baud
```

**Test sequence:**
1. Press **Ctrl-C** → should see `>>>` REPL prompt
2. Type: `print("Hello ESP32")` → should see output
3. Type: `import esp32; print(hex(esp32.chip_id()))` → should see chip ID
4. Press **Ctrl-A** → should see "raw REPL; CTRL-B to exit"
5. Type: `print("Raw mode test")` + **Ctrl-D** → should see output
6. Press **Ctrl-B** → return to normal REPL
7. Exit: **Ctrl-A, K, Y** (screen) or close PuTTY

## Step 3: Build and Run Belay.NET Tests

### 3.1 Build ESP32 Example

```bash
cd /home/corona/belay.net

# Build ESP32 hardware test
dotnet build examples/ESP32HardwareTest/ESP32HardwareTest.csproj

# Also build existing examples for comparison
dotnet build examples/PicoHardwareTest/PicoHardwareTest.csproj
dotnet build examples/ProtocolComparison/ProtocolComparison.csproj
```

### 3.2 Run ESP32-Specific Hardware Test

```bash
# Replace with your actual device path
# Linux:
dotnet run --project examples/ESP32HardwareTest/ESP32HardwareTest.csproj serial:/dev/ttyUSB0

# Windows:
dotnet run --project examples/ESP32HardwareTest/ESP32HardwareTest.csproj serial:COM3

# macOS:
dotnet run --project examples/ESP32HardwareTest/ESP32HardwareTest.csproj serial:/dev/cu.usbserial-0001
```

**Expected behavior:**
- Connection established successfully
- System information shows MicroPython version and "esp32" platform
- Built-in LED blinks 3 times (usually GPIO 2)
- ADC reading displayed (0-4095 range)
- Hall sensor reading (if supported by board)
- WiFi capability detection
- File transfer operations succeed
- All task attribute functionality validated

### 3.3 Run Protocol Comparison (ESP32 vs Pico vs Subprocess)

```bash
# Compare all three implementations
dotnet run --project examples/ProtocolComparison/ProtocolComparison.csproj \
  ../../micropython/ports/unix/build-standard/micropython \
  serial:/dev/ttyUSB0  # ESP32

# Then compare with Pico (if available)
dotnet run --project examples/ProtocolComparison/ProtocolComparison.csproj \
  ../../micropython/ports/unix/build-standard/micropython \
  serial:/dev/ttyACM0  # Pico
```

**Expected results:**
- All test cases pass on subprocess, ESP32, and Pico
- Outputs identical across all platforms
- ESP32 may have different performance characteristics than Pico
- Protocol compatibility: FULL across all platforms

### 3.4 Test File Transfer Integration

```bash
# Test file transfer specifically on ESP32
dotnet run --project file_transfer_test/FileTransferExample.csproj
# (Modify connection string in Program.cs to point to ESP32)
```

## Step 4: ESP32-Specific Validation

### 4.1 WiFi Capability Testing

The ESP32HardwareTest includes WiFi capability detection. For more advanced WiFi testing:

```python
# Manual REPL test for WiFi scanning
import network
wlan = network.WLAN(network.STA_IF)
wlan.active(True)
networks = wlan.scan()
print(f"Found {len(networks)} networks")
wlan.active(False)
```

### 4.2 Flash Memory Testing

ESP32 typically has more flash memory than Pico - test larger file operations:

```bash
# Test with larger files in the ESP32HardwareTest
# Binary file test uses 1KB - ESP32 can handle much larger
```

### 4.3 ADC and Sensor Testing

ESP32 has multiple ADC channels and sensors:

```python
# Manual REPL test for ADC
from machine import Pin, ADC
adc = ADC(Pin(36))  # GPIO 36
adc.atten(ADC.ATTN_11DB)  # 3.3V range
reading = adc.read()  # 0-4095
print(f"ADC reading: {reading}")
```

### 4.4 Chip Information Validation

```python
# Manual REPL test for chip info
import esp32
print(f"Chip ID: {hex(esp32.chip_id())}")
print(f"CPU Freq: {esp32.cpu_freq()} Hz")
print(f"Flash size: {esp32.flash_size()} bytes")
```

## Step 5: Validation Criteria

### 5.1 Success Metrics

A successful ESP32 validation should demonstrate:

✅ **Connection Management**
- Reliable connection/disconnection cycles
- No timeout errors during connection
- Stable serial communication at 115200 baud

✅ **Protocol Compatibility**  
- Raw REPL protocol works identically to Pico and subprocess
- All command sequences execute successfully
- Error handling consistent across platforms

✅ **Task Attribute Functionality**
- All Task attribute examples execute correctly
- Parameter marshaling works (integers, strings, complex types)
- Caching functionality operational
- Exclusive task execution supported

✅ **ESP32-Specific Features**
- Built-in LED controllable (typically GPIO 2)
- ADC readings functional (0-4095 range)
- Hall sensor accessible (if supported)
- WiFi capability detected
- Chip information retrievable

✅ **File Transfer Integration**
- Text and binary file transfers succeed
- Directory operations functional
- File size limitations appropriate for ESP32 flash memory
- Integration with Task attributes working

✅ **Performance Characteristics**
- Simple operations complete in <1s
- Complex operations complete in <3s
- File transfers perform adequately for ESP32 memory constraints
- No significant memory leaks during extended operation

### 5.2 ESP32 vs Pico Comparison

| Feature | ESP32 | Raspberry Pi Pico |
|---------|-------|-------------------|
| Built-in LED | GPIO 2 | GPIO 25 |
| Platform ID | "esp32" | "rp2" |
| WiFi | ✅ Built-in | ⚠️ Pico W only |
| Bluetooth | ✅ Built-in | ⚠️ Pico W only |
| ADC Resolution | 12-bit (0-4095) | 16-bit (0-65535) |
| Flash Memory | 4MB+ typical | 2MB |
| CPU Frequency | 240MHz dual-core | 133MHz dual-core |
| Sensors | Hall sensor | Internal temperature |
| Boot Mode | Automatic/BOOT button | BOOTSEL button |

### 5.3 Common Issues and Solutions

**Issue: Device not detected**
```bash
# Check USB cable supports data (not power-only)
# Verify correct driver (Silicon Labs CP210x, FTDI, etc.)
# Try different USB port
# Check Device Manager (Windows) or dmesg (Linux)
```

**Issue: Flash operation failed**
```bash
# Hold BOOT button during connection (some boards)
# Try slower baud rate: --baud 115200
# Erase flash first: esptool.py erase_flash
# Check power supply (some USB ports insufficient)
```

**Issue: Permission denied (Linux)**
```bash
# Add user to dialout group
sudo usermod -a -G dialout $USER
# Or temporary fix:
sudo chmod 666 /dev/ttyUSB0
```

**Issue: WiFi scan fails**
```bash
# Normal - WiFi testing is capability detection only
# Some ESP32 variants may have WiFi disabled in firmware
# Check board specifications and firmware variant
```

**Issue: Hall sensor not available**
```bash
# Normal - not all ESP32 variants include hall sensor
# ESP32-S2, ESP32-C3 typically don't have hall sensor
# ESP32 original usually includes it
```

## Step 6: Expected Outcomes

### 6.1 Technical Validation

After successful completion, you should have:

1. **Confirmed ESP32 Compatibility**: ESP32 works identically to Pico and subprocess implementations
2. **Protocol Validation**: Raw REPL protocol implementation correct for ESP32 hardware  
3. **Performance Baseline**: ESP32 timing characteristics established
4. **WiFi Capability**: ESP32 WiFi functionality accessible via Belay.NET
5. **File Transfer Validation**: Large file operations work within ESP32 constraints
6. **Multi-Platform Support**: Belay.NET works across ESP32, Pico, and subprocess

### 6.2 Development Impact

This validation enables:

- **Multi-Platform Development**: Applications targeting both ESP32 and Pico
- **WiFi-Enabled Applications**: ESP32 wireless capabilities via Task attributes
- **Larger File Operations**: ESP32 flash memory capabilities for file synchronization
- **IoT Applications**: ESP32 connectivity features for production deployments
- **Platform Comparison**: Performance and capability differences between platforms

### 6.3 Week 3 Integration

ESP32 validation complements Pico validation by:

- **Broader Hardware Support**: Multiple MicroPython platforms validated
- **Different Capability Sets**: WiFi (ESP32) vs Temperature sensor (Pico)
- **Memory Characteristics**: Different flash/RAM constraints tested
- **Connection Methods**: USB serial validation across different chip vendors
- **Performance Comparison**: Different CPU architectures and speeds

## Step 7: Documentation and Next Steps

### 7.1 Record Results

Create an ESP32-specific validation report:

```markdown
# ESP32 Validation Results

**Date**: [Current Date]
**Device**: ESP32 with MicroPython v1.24.0
**Board Type**: [ESP32/ESP32-S2/ESP32-S3/ESP32-C3]
**Platform**: [Your OS]
**Connection**: [Your device path]

## Test Results Summary
- ✅ Basic connectivity: PASS
- ✅ Task attribute functionality: PASS  
- ✅ Protocol compatibility: PASS
- ✅ ESP32-specific features: PASS
- ✅ File transfer integration: PASS
- ✅ Performance acceptable: PASS

## ESP32-Specific Results
- Built-in LED: ✅ GPIO 2 working
- ADC readings: ✅ 0-4095 range
- Hall sensor: [✅/⚠️] [Available/Not available]
- WiFi capability: ✅ Detected
- Chip ID: [Chip ID]
- Flash size: [Size] bytes

[Detailed results...]
```

### 7.2 Multi-Platform Testing

With both Pico and ESP32 validated:

1. **Unified Examples**: Create examples that work on both platforms
2. **Platform Abstraction**: Abstract platform-specific features
3. **Performance Comparison**: Document timing differences
4. **Capability Matrix**: Feature availability across platforms
5. **Connection Handling**: Robust connection string parsing

### 7.3 Advanced Features

ESP32 validation enables:

1. **WiFi Task Attributes**: Network connectivity via Task attributes
2. **Larger File Synchronization**: Multi-MB file transfers
3. **IoT Integration**: MQTT, HTTP clients via MicroPython
4. **Sensor Networks**: Multiple ESP32 devices coordinated
5. **Wireless Programming**: Over-the-air updates via WiFi

## Troubleshooting Quick Reference

| Issue | Symptoms | Solution |
|-------|----------|----------|
| Device not found | No /dev/ttyUSB* or COMx | Check USB cable, drivers, Device Manager |
| Flash failed | esptool.py errors | Hold BOOT button, try slower baud, erase first |
| Permission denied | Access denied opening port | Add to dialout group (Linux) or run as admin |
| Connection timeout | Hangs during connect | Try different baud rate, check board type |
| WiFi errors | WiFi scan fails | Normal for capability testing, check board specs |
| Hall sensor error | hall_sensor() fails | Normal for some ESP32 variants |
| Build errors | .NET compilation fails | Verify .NET 8 SDK, restore packages |
| Import errors | MicroPython module errors | Check firmware version, reflash if needed |

## Success Confirmation

A fully successful ESP32 validation will show:

```
🎉 ESP32 validation completed successfully!
✅ All tests passed - ESP32 hardware is ready for development

=== ESP32 vs Pico Comparison Notes ===
- Built-in LED on GPIO 2 (vs GPIO 25 on Pico)
- WiFi capability available (vs Bluetooth on Pico W)
- Hall sensor available (vs internal temperature on Pico)
- More flash memory for file operations
- Similar Task attribute performance
```

At this point, your Belay.NET implementation supports both major MicroPython hardware platforms and is ready for multi-platform development in Week 4-5.