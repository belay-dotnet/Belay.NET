# Raspberry Pi Pico Hardware Validation Guide - Week 3

**Date**: August 9, 2025  
**Device**: Raspberry Pi Pico with MicroPython  
**Belay.NET Version**: v0.1.0 Foundation + Week 2 File Transfer  
**Objective**: Validate hardware compatibility and compare with subprocess implementation

## Prerequisites

- Raspberry Pi Pico (or Pico W)
- USB-A to USB-C cable with data lines
- Computer with .NET 8+ SDK installed
- Belay.NET codebase (current repository state)

## Step 1: Hardware Setup

### 1.1 Download MicroPython Firmware

```bash
# Download latest stable MicroPython for Raspberry Pi Pico
wget https://micropython.org/resources/firmware/rpi-pico-20241025-v1.24.0.uf2
```

### 1.2 Flash Firmware to Pico

1. **Enter BOOTSEL mode:**
   - Hold the BOOTSEL button on your Pico
   - Connect USB cable while holding BOOTSEL
   - Release BOOTSEL button
   - Pico appears as "RPI-RP2" mass storage device

2. **Flash firmware:**
   ```bash
   # Linux
   cp rpi-pico-20241025-v1.24.0.uf2 /media/$USER/RPI-RP2/
   
   # Windows - drag and drop .uf2 file to RPI-RP2 drive
   
   # macOS
   cp rpi-pico-20241025-v1.24.0.uf2 /Volumes/RPI-RP2/
   ```

3. **Verify installation:**
   - Pico automatically reboots after firmware copy
   - Device appears as serial port (next step)

### 1.3 Identify Serial Connection

**Linux:**
```bash
# Pico appears as /dev/ttyACM0 (or /dev/ttyACM1 if other devices present)
ls -la /dev/ttyACM*
dmesg | tail -10  # Check connection messages
```

**Windows:**
```powershell
# Check Device Manager or use PowerShell
Get-WmiObject Win32_SerialPort | Select-Object DeviceID,Description
# Pico appears as COMx (e.g., COM3, COM4, etc.)
```

**macOS:**
```bash
# Pico appears as /dev/cu.usbmodem* 
ls -la /dev/cu.usbmodem*
# Example: /dev/cu.usbmodem143201
```

### 1.4 Configure Permissions (Linux only)

```bash
# Add user to dialout group for permanent access
sudo usermod -a -G dialout $USER
# Log out and back in for changes to take effect

# Or temporary permission:
sudo chmod 666 /dev/ttyACM0
```

## Step 2: Basic Connection Verification

### 2.1 Terminal Test

```bash
# Linux/macOS
screen /dev/ttyACM0 115200

# Windows - use PuTTY with COMx at 115200 baud
```

**Test sequence:**
1. Press **Ctrl-C** → should see `>>>` REPL prompt
2. Type: `print("Hello Pico")` → should see output
3. Press **Ctrl-A** → should see "raw REPL; CTRL-B to exit"
4. Type: `print("Raw mode test")` + **Ctrl-D** → should see output
5. Press **Ctrl-B** → return to normal REPL
6. Exit: **Ctrl-A, K, Y** (screen) or close PuTTY

## Step 3: Build and Run Belay.NET Tests

### 3.1 Build Examples

```bash
cd /home/corona/belay.net

# Build all examples
dotnet build examples/TaskAttributeMinimalTest/TaskAttributeMinimalTest.csproj
dotnet build examples/BasicTaskExample/BasicTaskExample.csproj  
dotnet build examples/PicoHardwareTest/PicoHardwareTest.csproj
dotnet build examples/ProtocolComparison/ProtocolComparison.csproj
```

### 3.2 Run Infrastructure Test

```bash
# Test Task attribute infrastructure (no hardware connection needed)
dotnet run --project examples/TaskAttributeMinimalTest/TaskAttributeMinimalTest.csproj

# Expected output:
# ✓ Task attribute found: True
# ✓ TaskExecutor accessed
# 🎉 Task attribute infrastructure is working!
```

### 3.3 Run Basic Task Example

```bash
# Replace with your actual device path
# Linux:
dotnet run --project examples/BasicTaskExample/BasicTaskExample.csproj serial:/dev/ttyACM0

# Windows:
dotnet run --project examples/BasicTaskExample/BasicTaskExample.csproj serial:COM3

# macOS:
dotnet run --project examples/BasicTaskExample/BasicTaskExample.csproj serial:/dev/cu.usbmodem143201
```

**Expected behavior:**
- Connection established successfully
- System information displayed (MicroPython version, platform "rp2")
- Task examples execute (calculations, sensor readings, cached operations)
- All examples complete without errors

### 3.4 Run Pico-Specific Hardware Test

```bash
# Linux example (adjust path for your system):
dotnet run --project examples/PicoHardwareTest/PicoHardwareTest.csproj serial:/dev/ttyACM0
```

**Expected behavior:**
- Device information shows MicroPython version and "rp2" platform
- Built-in LED blinks 3 times (visible on Pico board)
- Internal temperature reading displayed (~27°C at room temperature)
- All task attribute functionality validated
- Protocol and error handling tests pass

### 3.5 Run Protocol Comparison

```bash
# Compare subprocess vs hardware behavior
dotnet run --project examples/ProtocolComparison/ProtocolComparison.csproj \
  ../../micropython/ports/unix/build-standard/micropython \
  serial:/dev/ttyACM0
```

**Expected results:**
- All test cases pass on both subprocess and hardware
- Outputs are identical between subprocess and hardware
- Hardware may be slightly slower than subprocess (acceptable)
- Protocol compatibility: FULL

## Step 4: Validation Criteria

### 4.1 Success Metrics

A successful validation should demonstrate:

✅ **Connection Management**
- Reliable connection/disconnection cycles
- No timeout errors during connection

✅ **Protocol Compatibility**  
- Raw REPL protocol works identically to subprocess
- All command sequences execute successfully
- Error handling matches subprocess behavior

✅ **Task Attribute Functionality**
- All Task attribute examples execute correctly
- Parameter marshaling works (integers, strings, complex types)
- Caching functionality operational
- Exclusive task execution supported

✅ **Hardware-Specific Features**
- Pico built-in LED controllable
- Internal temperature sensor accessible
- Platform identification correct ("rp2")
- Memory management functional

✅ **Performance Acceptable**
- Simple operations complete in <500ms
- Complex operations complete in <2s
- No significant memory leaks during extended operation

### 4.2 Common Issues and Solutions

**Issue: Device not detected**
```bash
# Check USB cable has data lines (not power-only)
# Verify driver installation (Windows)
# Check permissions (Linux: dialout group)
```

**Issue: Permission denied**
```bash
# Linux: Add user to dialout group
sudo usermod -a -G dialout $USER
# Or temporary fix:
sudo chmod 666 /dev/ttyACM0
```

**Issue: Connection timeouts**
```bash
# Pico may need time to initialize after connection
# Try adding delay or soft reset in connection logic
```

**Issue: Raw REPL not responding**
```bash
# Send interrupt sequence first:
# Ctrl-C (interrupt) → Ctrl-B (exit raw) → Ctrl-A (enter raw)
```

## Step 5: Expected Outcomes

### 5.1 Technical Validation

After successful completion, you should have:

1. **Confirmed Hardware Compatibility**: Raspberry Pi Pico works identically to subprocess implementation
2. **Protocol Validation**: Raw REPL protocol implementation is correct for real hardware  
3. **Performance Baseline**: Established timing expectations for hardware operations
4. **Error Handling Verification**: Device exceptions properly mapped to C# exceptions
5. **Task Infrastructure Validation**: All Task attribute features work on real hardware

### 5.2 Development Impact

This validation enables:

- **Week 4-5 Development**: Confident hardware-based development and testing
- **File Transfer Testing**: Validation of chunked file transfer on real memory constraints
- **Advanced Features**: Hardware-specific capabilities (GPIO, sensors, etc.)
- **CI/CD Integration**: Hardware-in-the-loop testing for future development

### 5.3 Issue Resolution

This validation should determine:

- **Subprocess vs Hardware**: Whether intermittent Raw REPL failures are subprocess-specific
- **Protocol Robustness**: Real-world protocol behavior under various conditions  
- **Memory Constraints**: How hardware memory limitations affect operations
- **Connection Reliability**: Hardware connection stability compared to subprocess

## Step 6: Documentation and Next Steps

### 6.1 Record Results

Create a validation report similar to `HARDWARE_VALIDATION_RESULTS.md` but specific to Raspberry Pi Pico:

```markdown
# Raspberry Pi Pico Validation Results

**Date**: [Current Date]
**Device**: Raspberry Pi Pico with MicroPython v1.24.0
**Platform**: [Your OS]
**Connection**: [Your device path]

## Test Results Summary
- ✅ Basic connectivity: PASS
- ✅ Task attribute functionality: PASS  
- ✅ Protocol compatibility: PASS
- ✅ Hardware-specific features: PASS
- ✅ Performance acceptable: PASS

[Detailed results...]
```

### 6.2 Next Development Phase

With successful Pico validation:

1. **Advanced File Transfer**: Test chunked file transfers with real memory constraints
2. **Multiple Device Support**: Extend to other MicroPython boards
3. **WebREPL Integration**: Wireless device communication (Pico W)
4. **Hardware-Specific APIs**: GPIO, I2C, SPI interfaces via Task attributes
5. **Production Hardening**: Error recovery, reconnection logic, monitoring

### 6.3 CI/CD Integration

Configure continuous integration to use hardware:

```bash
# Example CI environment variable
export BELAY_HARDWARE_DEVICE="serial:/dev/ttyACM0"
dotnet test --filter "Category=Hardware" --configuration Release
```

## Troubleshooting Quick Reference

| Issue | Symptoms | Solution |
|-------|----------|----------|
| Device not found | No /dev/ttyACM* or COMx | Check USB cable, drivers, BOOTSEL mode |
| Permission denied | Access denied opening port | Add to dialout group (Linux) or run as admin |
| Connection timeout | Hangs during connect | Try different baud rate, check cable |
| Raw REPL failure | No response to Ctrl-A | Send Ctrl-C first, check device state |
| Build errors | .NET compilation fails | Verify .NET 8 SDK, restore packages |
| Import errors | MicroPython module errors | Check firmware version, reflash if needed |

## Success Confirmation

A fully successful validation will show:

```
🎉 Raspberry Pi Pico validation completed successfully!
✅ All tests passed - hardware is ready for development
```

At this point, your Belay.NET implementation is validated for production hardware and ready for advanced feature development in Week 4-5.