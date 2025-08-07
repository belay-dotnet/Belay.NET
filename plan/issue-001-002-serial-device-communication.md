# Issue 001-002: Serial Device Communication Layer

**Epic**: Epic 001 - Device Communication Foundation  
**Status**: Not Started  
**Priority**: Critical  
**Estimated Effort**: 1 week  
**Assignee**: TBD  
**Dependencies**: Issue 001-001 (Raw REPL Protocol)

## Summary

Implement the serial/USB device communication layer that provides reliable connectivity to MicroPython devices over serial ports. This layer wraps System.IO.Ports functionality with MicroPython-specific protocols and robust error handling.

## Technical Requirements

### Core Communication Interface
```csharp
public class SerialDeviceCommunication : IDeviceCommunication
{
    private readonly SerialPort _serialPort;
    private readonly RawReplProtocol _replProtocol;
    private readonly SemaphoreSlim _executionSemaphore;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private volatile bool _disposed;
    
    public SerialDeviceCommunication(string portName, int baudRate = 115200, 
        int timeout = 30000)
    {
        _serialPort = new SerialPort(portName, baudRate)
        {
            Parity = Parity.None,
            DataBits = 8,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = timeout,
            WriteTimeout = timeout,
            NewLine = "\n"
        };
    }
    
    public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    public async Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default)
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    public event EventHandler<DeviceOutputEventArgs> OutputReceived;
    public event EventHandler<DeviceStateChangeEventArgs> StateChanged;
}
```

### Device Discovery and Enumeration
```csharp
public static class SerialDeviceDiscovery
{
    public static async Task<DeviceInfo[]> DiscoverMicroPythonDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        var availablePorts = SerialPort.GetPortNames();
        var devices = new List<DeviceInfo>();
        
        foreach (var port in availablePorts)
        {
            try
            {
                var deviceInfo = await ProbeDeviceAsync(port, cancellationToken);
                if (deviceInfo != null)
                {
                    devices.Add(deviceInfo);
                }
            }
            catch (Exception ex)
            {
                // Log and continue - some ports may not be accessible
            }
        }
        
        return devices.ToArray();
    }
    
    private static async Task<DeviceInfo> ProbeDeviceAsync(string portName, 
        CancellationToken cancellationToken)
    {
        // Attempt to connect and identify MicroPython/CircuitPython device
        // Send identification commands and parse responses
        // Return DeviceInfo with implementation details
    }
}

public class DeviceInfo
{
    public string PortName { get; set; }
    public string Implementation { get; set; } // "micropython" or "circuitpython"
    public Version Version { get; set; }
    public string Platform { get; set; }
    public string[] Capabilities { get; set; }
    public bool SupportsRawPasteMode { get; set; }
}
```

## Connection Management

### Connection State Handling
```csharp
public enum DeviceConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Executing,
    Error,
    Reconnecting
}

public class DeviceStateChangeEventArgs : EventArgs
{
    public DeviceConnectionState OldState { get; }
    public DeviceConnectionState NewState { get; }
    public string Reason { get; }
    public Exception Exception { get; }
}
```

### Robust Connection Logic
```csharp
public async Task ConnectAsync(CancellationToken cancellationToken = default)
{
    SetState(DeviceConnectionState.Connecting, "Initiating connection");
    
    try
    {
        // Open serial port
        _serialPort.Open();
        
        // Wait for device to be ready (may need soft reset)
        await WaitForDeviceReadyAsync(cancellationToken);
        
        // Initialize raw REPL protocol
        await _replProtocol.InitializeAsync(_serialPort.BaseStream, cancellationToken);
        
        // Probe device capabilities
        await ProbeDeviceCapabilitiesAsync(cancellationToken);
        
        SetState(DeviceConnectionState.Connected, "Successfully connected");
    }
    catch (Exception ex)
    {
        SetState(DeviceConnectionState.Error, $"Connection failed: {ex.Message}", ex);
        throw new DeviceConnectionException($"Failed to connect to device on {_serialPort.PortName}", ex);
    }
}

private async Task WaitForDeviceReadyAsync(CancellationToken cancellationToken)
{
    // MicroPython devices may need time to boot or may be in unknown state
    // Send interrupt (Ctrl-C) and soft reset (Ctrl-D) to get to clean state
    // Wait for ">>>" prompt indicating ready state
}
```

### Auto-Reconnection Support
```csharp
public class ReconnectionPolicy
{
    public bool EnableAutoReconnect { get; set; } = true;
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(1);
    public int MaxReconnectAttempts { get; set; } = 5;
    public bool ExponentialBackoff { get; set; } = true;
}

private async Task HandleConnectionLostAsync(Exception ex)
{
    if (!_reconnectionPolicy.EnableAutoReconnect)
    {
        SetState(DeviceConnectionState.Error, "Connection lost", ex);
        return;
    }
    
    SetState(DeviceConnectionState.Reconnecting, "Attempting to reconnect");
    
    for (int attempt = 1; attempt <= _reconnectionPolicy.MaxReconnectAttempts; attempt++)
    {
        try
        {
            await Task.Delay(CalculateReconnectDelay(attempt), _cancellationTokenSource.Token);
            await ConnectAsync(_cancellationTokenSource.Token);
            
            // Replay any recorded commands for state reconstruction
            await ReplayCommandHistoryAsync(_cancellationTokenSource.Token);
            
            return; // Success
        }
        catch (Exception reconnectEx)
        {
            _logger.LogWarning(reconnectEx, "Reconnection attempt {Attempt} failed", attempt);
            
            if (attempt == _reconnectionPolicy.MaxReconnectAttempts)
            {
                SetState(DeviceConnectionState.Error, "All reconnection attempts failed", reconnectEx);
                throw new DeviceConnectionException("Device reconnection failed after maximum attempts", reconnectEx);
            }
        }
    }
}
```

## Code Execution Implementation

### Basic Execution Flow
```csharp
public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default)
{
    await _executionSemaphore.WaitAsync(cancellationToken);
    
    try
    {
        SetState(DeviceConnectionState.Executing, "Executing code");
        
        // Record command for potential replay
        RecordCommand(code);
        
        // Execute via raw REPL protocol
        var response = await _replProtocol.ExecuteCodeAsync(code, useRawPasteMode: true, cancellationToken);
        
        if (!response.IsSuccess)
        {
            throw new DeviceExecutionException("Code execution failed", response.Exception)
            {
                DeviceOutput = response.ErrorOutput,
                ExecutedCode = code
            };
        }
        
        // Forward any output to event handlers
        if (!string.IsNullOrEmpty(response.Output))
        {
            OutputReceived?.Invoke(this, new DeviceOutputEventArgs(response.Output));
        }
        
        SetState(DeviceConnectionState.Connected, "Execution completed");
        return response.Result;
    }
    catch (Exception ex) when (IsConnectionError(ex))
    {
        _ = Task.Run(() => HandleConnectionLostAsync(ex));
        throw;
    }
    finally
    {
        _executionSemaphore.Release();
    }
}
```

### Typed Execution Support
```csharp
public async Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default)
{
    var result = await ExecuteAsync(code, cancellationToken);
    
    try
    {
        return JsonSerializer.Deserialize<T>(result);
    }
    catch (JsonException)
    {
        // Fallback to simple type conversion for basic types
        return (T)Convert.ChangeType(result, typeof(T));
    }
}
```

## File Operations

### File Transfer Support
```csharp
public async Task PutFileAsync(string localPath, string remotePath, 
    CancellationToken cancellationToken = default)
{
    var fileContent = await File.ReadAllBytesAsync(localPath, cancellationToken);
    
    // Use MicroPython filesystem operations
    var base64Content = Convert.ToBase64String(fileContent);
    var code = $@"
import binascii
with open('{remotePath}', 'wb') as f:
    f.write(binascii.a2b_base64('{base64Content}'))
";
    
    await ExecuteAsync(code, cancellationToken);
}

public async Task<byte[]> GetFileAsync(string remotePath, 
    CancellationToken cancellationToken = default)
{
    var code = $@"
import binascii
with open('{remotePath}', 'rb') as f:
    print(binascii.b2a_base64(f.read()).decode())
";
    
    var result = await ExecuteAsync(code, cancellationToken);
    return Convert.FromBase64String(result.Trim());
}
```

## Testing Requirements

### Unit Tests
```csharp
[TestFixture]
public class SerialDeviceCommunicationTests
{
    private Mock<SerialPort> _mockSerialPort;
    private SerialDeviceCommunication _communication;
    
    [Test]
    public async Task ConnectAsync_WithValidPort_ShouldConnectSuccessfully()
    {
        // Test successful connection scenario
    }
    
    [Test]
    public async Task ExecuteAsync_WithSimpleCode_ShouldReturnResult()
    {
        // Test basic code execution
    }
    
    [Test]  
    public async Task HandleConnectionLost_WithAutoReconnect_ShouldReconnect()
    {
        // Test reconnection logic
    }
    
    [Test]
    public async Task PutFileAsync_WithBinaryFile_ShouldTransferCorrectly()
    {
        // Test file transfer functionality
    }
}
```

### Integration Tests
```csharp
[TestFixture]
[Category("Integration")]
public class SerialDeviceCommunicationIntegrationTests
{
    [Test]
    [TestCase("COM3")]   // Windows
    [TestCase("/dev/ttyUSB0")] // Linux  
    public async Task RealDevice_BasicExecution_ShouldWork(string portName)
    {
        // Test with real hardware devices
    }
    
    [Test]
    public async Task DeviceDiscovery_ShouldFindMicroPythonDevices()
    {
        // Test device discovery functionality
    }
}
```

## Error Handling

### Exception Hierarchy
```csharp
public class DeviceConnectionException : Exception
{
    public string PortName { get; }
    
    public DeviceConnectionException(string message, string portName = null) : base(message)
    {
        PortName = portName;
    }
}

public class DeviceExecutionException : Exception
{
    public string ExecutedCode { get; set; }
    public string DeviceOutput { get; set; }
    public string DeviceTraceback { get; set; }
}

public class DeviceTimeoutException : DeviceConnectionException
{
    public TimeSpan Timeout { get; }
}
```

## Performance Considerations

### Optimization Targets
- Connection establishment: <2 seconds
- Simple code execution: <50ms overhead
- File transfer: >1KB/second for small files
- Memory usage: <5MB per device connection

### Performance Monitoring
```csharp
public class DevicePerformanceMetrics
{
    public TimeSpan AverageExecutionTime { get; set; }
    public long TotalExecutions { get; set; }
    public long FailedExecutions { get; set; }
    public DateTime LastExecutionTime { get; set; }
    public long BytesTransferred { get; set; }
}
```

## Implementation Plan

### Phase 1: Core Communication (2 days)
- Implement SerialDeviceCommunication class
- Basic connection and disconnection
- Simple code execution wrapper
- Unit test framework setup

### Phase 2: Robustness Features (2 days)
- Connection state management
- Auto-reconnection logic
- Error handling and recovery
- Command history and replay

### Phase 3: Advanced Features (2 days)
- Device discovery and enumeration
- File transfer operations
- Performance monitoring
- Cross-platform testing

### Phase 4: Integration & Polish (1 day)
- Integration tests with real devices
- Performance optimization
- Documentation completion
- Code review and cleanup

## Acceptance Criteria

### Functional Requirements
- [ ] Successfully connect to MicroPython devices via serial/USB
- [ ] Execute code with proper error handling and result parsing
- [ ] Support device discovery and auto-detection
- [ ] Handle connection failures with automatic reconnection
- [ ] Transfer files reliably between host and device

### Performance Requirements
- [ ] Connection establishment in <2 seconds
- [ ] Code execution overhead <50ms
- [ ] Support concurrent operations safely
- [ ] Graceful handling of slow or unresponsive devices

### Reliability Requirements
- [ ] >95% success rate for basic operations
- [ ] Automatic recovery from connection issues
- [ ] Proper resource cleanup and disposal
- [ ] Cross-platform compatibility (Windows/Linux/macOS)

## Definition of Done

- [ ] All acceptance criteria met and validated
- [ ] Comprehensive unit test coverage (>90%)
- [ ] Integration tests passing with real hardware
- [ ] Performance benchmarks meet requirements
- [ ] Cross-platform compatibility verified
- [ ] Code review approved and security vetted
- [ ] Documentation complete and accurate
- [ ] Memory leak testing passed

This implementation provides the robust serial communication foundation required for reliable MicroPython device interaction.