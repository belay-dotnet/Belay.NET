# Issue 001-001: Raw REPL Protocol Implementation

**Epic**: Epic 001 - Device Communication Foundation  
**Status**: Foundation Complete - Needs Optimization  
**Priority**: Critical  
**Estimated Effort**: 1.5 weeks  
**Assignee**: TBD  

## Summary

Implement the MicroPython Raw REPL protocol with support for both raw mode and raw-paste mode. This is the core communication protocol that enables reliable programmatic interaction with MicroPython devices.

## Technical Requirements

### Raw Mode Protocol
Implement basic raw REPL mode following this sequence:
1. Send Ctrl-A (0x01) to enter raw REPL mode
2. Wait for "raw REPL; CTRL-B to exit\r\n>" response
3. Send Python code followed by Ctrl-D (0x04)
4. Wait for "OK" acknowledgment  
5. Read execution output until ">" prompt
6. Parse results and handle errors

### Raw-Paste Mode Protocol  
Implement advanced raw-paste mode with flow control:
1. Enter raw REPL mode (Ctrl-A)
2. Send initialization sequence: `\x05A\x01`
3. Read device response to confirm support
4. Read window-size-increment (16-bit little-endian)
5. Send code with flow control management:
   - Track remaining window size
   - Handle flow control bytes: `\x01` (increase window), `\x04` (end data)
   - Send data in chunks respecting window size
6. Send `\x04` to indicate end of data transmission
7. Read device output and execution results

### State Management
```csharp
public enum RawReplState
{
    Normal,      // Standard interactive REPL
    Raw,         // Raw mode for programmatic use  
    RawPaste     // Raw-paste mode with flow control
}

public class RawReplStateMachine
{
    public RawReplState CurrentState { get; private set; }
    
    public async Task EnterRawModeAsync(CancellationToken cancellationToken = default);
    public async Task EnterRawPasteModeAsync(CancellationToken cancellationToken = default);  
    public async Task ExitRawModeAsync(CancellationToken cancellationToken = default);
    public async Task SendCodeAsync(string code, bool useFlowControl, CancellationToken cancellationToken = default);
}
```

## Implementation Details

### Core Protocol Class
```csharp
public class RawReplProtocol
{
    private readonly Stream _stream;
    private readonly ILogger<RawReplProtocol> _logger;
    private readonly SemaphoreSlim _protocolSemaphore;
    private RawReplState _currentState;
    
    // Control characters
    private const byte CTRL_A = 0x01; // Enter raw REPL
    private const byte CTRL_B = 0x02; // Exit raw REPL  
    private const byte CTRL_D = 0x04; // Execute/End data
    private const byte CTRL_E = 0x05; // Raw-paste mode
    
    public async Task<RawReplResponse> ExecuteCodeAsync(string code, 
        bool useRawPasteMode = true, CancellationToken cancellationToken = default)
    {
        await _protocolSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (useRawPasteMode && await SupportsRawPasteModeAsync(cancellationToken))
            {
                return await ExecuteWithRawPasteModeAsync(code, cancellationToken);
            }
            else
            {
                return await ExecuteWithRawModeAsync(code, cancellationToken);
            }
        }
        finally
        {
            _protocolSemaphore.Release();
        }
    }
    
    private async Task<bool> SupportsRawPasteModeAsync(CancellationToken cancellationToken)
    private async Task<RawReplResponse> ExecuteWithRawModeAsync(string code, CancellationToken cancellationToken)
    private async Task<RawReplResponse> ExecuteWithRawPasteModeAsync(string code, CancellationToken cancellationToken)
}
```

### Response Handling
```csharp
public class RawReplResponse
{
    public bool IsSuccess { get; set; }
    public string Output { get; set; }
    public string ErrorOutput { get; set; }  
    public string Result { get; set; }
    public Exception Exception { get; set; }
}

public static class ResponseParser
{
    public static RawReplResponse ParseResponse(string rawOutput)
    {
        // Parse device output to separate:
        // - Standard output (print statements)
        // - Error output (exceptions, tracebacks)  
        // - Result values (expression results)
        // - Belay-specific response markers (_BELAYR prefix)
    }
}
```

## Flow Control Implementation

### Window Size Management
```csharp
public class FlowControlManager
{
    private int _remainingWindowSize;
    private int _windowSizeIncrement;
    
    public async Task InitializeAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Read initial window size increment (16-bit little-endian)
        var buffer = new byte[2];
        await stream.ReadExactlyAsync(buffer, cancellationToken);
        _windowSizeIncrement = BitConverter.ToUInt16(buffer, 0);
        _remainingWindowSize = _windowSizeIncrement;
    }
    
    public async Task SendDataWithFlowControlAsync(Stream stream, byte[] data, 
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            if (_remainingWindowSize == 0)
            {
                await WaitForFlowControlAsync(stream, cancellationToken);
            }
            
            int chunkSize = Math.Min(_remainingWindowSize, data.Length - offset);
            await stream.WriteAsync(data, offset, chunkSize, cancellationToken);
            
            offset += chunkSize;
            _remainingWindowSize -= chunkSize;
        }
    }
    
    private async Task WaitForFlowControlAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Read and handle flow control bytes
        // 0x01: Increase window size
        // 0x04: End data reception  
    }
}
```

## Error Handling

### Protocol Exceptions
```csharp
public class RawReplProtocolException : Exception
{
    public RawReplState ExpectedState { get; }
    public RawReplState ActualState { get; }
    
    public RawReplProtocolException(string message, RawReplState expected, RawReplState actual) 
        : base(message)
}

public class FlowControlException : RawReplProtocolException
{
    public int WindowSize { get; }
    public byte ReceivedByte { get; }
}
```

### Device Error Mapping
```csharp
public static class DeviceErrorParser  
{
    public static DeviceExecutionException ParseTraceback(string traceback)
    {
        // Parse MicroPython tracebacks to extract:
        // - Exception type and message
        // - Line numbers and file information
        // - Call stack details
        // Map to appropriate .NET exception types
    }
}
```

## Testing Requirements

### Unit Tests
- [ ] Raw mode state transitions
- [ ] Raw-paste mode flow control scenarios
- [ ] Protocol error conditions and recovery
- [ ] Response parsing for various output formats
- [ ] Concurrent access protection

### Integration Tests
- [ ] Physical device raw REPL communication
- [ ] Subprocess (unix port) raw REPL testing
- [ ] Large code transmission via raw-paste mode
- [ ] Error condition handling with real devices
- [ ] Performance testing for protocol overhead

### Test Cases
```csharp
[Test]
public async Task EnterRawMode_ShouldTransitionState()
{
    // Test basic raw mode entry
}

[Test] 
public async Task RawPasteMode_ShouldHandleFlowControl()
{
    // Test flow control with simulated window size limits
}

[Test]
public async Task ExecuteCode_WithSyntaxError_ShouldThrowAppropriateException()
{
    // Test error handling and exception mapping
}

[Test]
public async Task ConcurrentExecution_ShouldSerializeAccess()
{
    // Test thread safety and semaphore protection
}
```

## Acceptance Criteria

### Functional Requirements
- [ ] Successfully enter and exit raw REPL mode
- [ ] Execute Python code and receive results in raw mode
- [ ] Support raw-paste mode with proper flow control
- [ ] Handle device errors and map to appropriate exceptions
- [ ] Maintain thread safety for concurrent access

### Performance Requirements  
- [ ] <10ms protocol overhead for simple code execution
- [ ] Support code transmission up to 64KB via raw-paste mode
- [ ] Handle flow control without blocking for >100ms
- [ ] Graceful handling of slow device responses

### Reliability Requirements
- [ ] >99% success rate for basic protocol operations
- [ ] Automatic recovery from protocol state issues
- [ ] Proper resource cleanup on connection failures
- [ ] Timeout handling for all protocol phases

## Implementation Status

### Phase 1: Basic Raw Mode (✅ Complete)
- ✅ Implement RawReplStateMachine with basic state transitions
- ✅ Create basic raw mode execution flow
- ✅ Add simple response parsing
- ✅ Unit tests for state management

### Phase 2: Raw-Paste Mode (✅ Complete)  
- ✅ Implement flow control manager
- ✅ Add raw-paste mode protocol support
- ✅ Create window size management
- 📋 Integration tests with subprocess communication (blocked on Issue 001-003)

### Phase 3: Error Handling (🔄 Partial)
- ✅ Implement basic error parsing
- ✅ Add exception hierarchy and mapping
- ⏳ Create timeout and recovery mechanisms (needs improvement)
- ⏳ Error condition testing (needs real device testing)

### Phase 4: Polish & Optimization (⏳ Needed)
- ⏳ Performance optimization (needs benchmarking)
- ✅ Code review and cleanup  
- ⏳ Documentation completion (partially done)
- ⏳ Final integration testing (blocked on serial communication)

## Dependencies

### Internal Dependencies
- Basic stream communication infrastructure
- Logging framework integration
- Exception hierarchy foundation

### External Dependencies  
- MicroPython unix port for testing
- Physical test device (Raspberry Pi Pico)
- Cross-platform stream testing

## Definition of Done

- [ ] All acceptance criteria met and tested
- [ ] Comprehensive unit test coverage (>95%)
- [ ] Integration tests passing on multiple platforms
- [ ] Performance benchmarks meet requirements
- [ ] Code review approved
- [ ] Documentation complete and accurate
- [ ] No security vulnerabilities identified
- [ ] Memory leak testing completed

## Related Issues

- **Issue 001-002**: Serial Device Communication Layer (dependency)
- **Issue 001-003**: Subprocess Communication for Testing (dependency)
- **Issue 001-007**: Response Parsing and Type Conversion (builds on this)

This issue provides the foundational protocol implementation that all device communication depends upon.