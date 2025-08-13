# Issue 001-012: Raw REPL Protocol Fix

**Epic**: Epic 001 - Device Communication Foundation  
**Status**: In Progress  
**Priority**: CRITICAL - Blocking Hardware Integration  
**Estimated Effort**: 2-3 days  
**Created**: 2025-08-13  

## Executive Summary

The raw REPL protocol implementation in the simplified architecture is not functioning correctly. Hardware integration tests show successful connection but fail to execute Python code, receiving MicroPython REPL prompts instead of execution results. This issue blocks all hardware testing and must be fixed immediately.

## Problem Analysis

### Current Implementation Issues

1. **No Raw Mode Verification** (Line 318 in DeviceConnection.cs)
   - Only waits 50ms after sending Ctrl-A
   - Does not verify receipt of "raw REPL; CTRL-B to exit\r\n>" prompt
   - Proceeds blindly assuming raw mode is active

2. **Inadequate Result Parsing** (Lines 350-414)
   - `ReadUntilPromptAsync` looks for wrong markers (">" or ">>>")
   - Should wait for "\x04>" (raw mode completion marker)
   - `ParseRawReplResult` incorrectly strips important protocol markers

3. **Missing Protocol Sequence**
   - No "OK" acknowledgment handling after code execution
   - No proper handling of "\x04\x04>" sequence
   - No distinction between output and result data

4. **No Error Detection**
   - Cannot distinguish between successful execution and errors
   - Traceback parsing not implemented
   - No timeout handling for stuck states

## Technical Solution

### Phase 1: Core Protocol Fix (Immediate)

#### 1.1 Raw Mode Entry Verification
```csharp
private async Task<bool> EnterRawModeAsync(CancellationToken cancellationToken)
{
    // Send Ctrl-A to enter raw mode
    await this.WriteAsync("\x01");
    
    // Wait for raw mode confirmation
    string response = await this.ReadExactAsync("raw REPL; CTRL-B to exit\r\n>", 1000, cancellationToken);
    
    if (response.Contains("raw REPL; CTRL-B to exit"))
    {
        // Clear any remaining prompt characters
        await this.FlushInputAsync();
        return true;
    }
    
    return false;
}
```

#### 1.2 Proper Result Reading
```csharp
private async Task<RawReplResult> ReadExecutionResultAsync(CancellationToken cancellationToken)
{
    var result = new RawReplResult();
    var buffer = new StringBuilder();
    
    // First, expect "OK" acknowledgment
    string ack = await this.ReadUntilAsync("OK", 1000, cancellationToken);
    if (!ack.Contains("OK"))
    {
        throw new DeviceException("Raw REPL did not acknowledge execution");
    }
    
    // Read until we see the completion marker \x04\x04>
    string rawOutput = await this.ReadUntilAsync("\x04\x04>", 5000, cancellationToken);
    
    // Split into output and error sections
    var sections = rawOutput.Split('\x04');
    
    if (sections.Length >= 2)
    {
        result.Output = sections[0].Trim();
        
        // Check for traceback in error section
        if (sections[1].Contains("Traceback"))
        {
            result.IsError = true;
            result.ErrorMessage = sections[1];
        }
    }
    
    // Read the final prompt marker
    await this.ReadUntilAsync("\x04>", 1000, cancellationToken);
    
    return result;
}
```

#### 1.3 Enhanced ExecuteRawReplAsync
```csharp
private async Task<string> ExecuteRawReplAsync(string code, CancellationToken cancellationToken)
{
    const int MAX_RETRIES = 2;
    
    for (int attempt = 0; attempt < MAX_RETRIES; attempt++)
    {
        try
        {
            // Ensure we're in raw mode
            if (!await this.EnterRawModeAsync(cancellationToken))
            {
                // Try to recover by sending interrupt and retrying
                await this.WriteAsync("\x03"); // Ctrl-C
                await Task.Delay(100, cancellationToken);
                continue;
            }
            
            // Send the code
            await this.WriteAsync(code);
            
            // Execute with Ctrl-D
            await this.WriteAsync("\x04");
            
            // Read the result with proper parsing
            var result = await this.ReadExecutionResultAsync(cancellationToken);
            
            // Exit raw mode
            await this.WriteAsync("\x02");
            
            if (result.IsError)
            {
                throw new DeviceException($"Python execution error: {result.ErrorMessage}");
            }
            
            return result.Output;
        }
        catch (TimeoutException) when (attempt < MAX_RETRIES - 1)
        {
            // Reset and retry
            await this.WriteAsync("\x03\x03"); // Double Ctrl-C
            await Task.Delay(200, cancellationToken);
        }
    }
    
    throw new DeviceException("Failed to execute code after retries");
}
```

### Phase 2: Robust Communication Helpers

#### 2.1 ReadUntilAsync with Timeout
```csharp
private async Task<string> ReadUntilAsync(string marker, int timeoutMs, CancellationToken cancellationToken)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    cts.CancelAfter(timeoutMs);
    
    var buffer = new StringBuilder();
    var charBuffer = new byte[256];
    
    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            int bytesRead = await this.ReadBytesAsync(charBuffer, cts.Token);
            if (bytesRead > 0)
            {
                string chunk = Encoding.UTF8.GetString(charBuffer, 0, bytesRead);
                buffer.Append(chunk);
                
                if (buffer.ToString().Contains(marker))
                {
                    return buffer.ToString();
                }
            }
            
            await Task.Delay(1, cts.Token);
        }
    }
    catch (OperationCanceledException)
    {
        throw new TimeoutException($"Timeout waiting for '{marker}' after {timeoutMs}ms");
    }
    
    return buffer.ToString();
}
```

#### 2.2 Unified Read Implementation
```csharp
private async Task<int> ReadBytesAsync(byte[] buffer, CancellationToken cancellationToken)
{
    switch (this.Type)
    {
        case ConnectionType.Serial:
            if (this.serialPort!.BytesToRead > 0)
            {
                return this.serialPort.Read(buffer, 0, Math.Min(buffer.Length, this.serialPort.BytesToRead));
            }
            return 0;
            
        case ConnectionType.Subprocess:
            return await this.processOutput!.BaseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            
        default:
            throw new InvalidOperationException($"Unknown connection type: {this.Type}");
    }
}
```

### Phase 3: Result Data Structure

```csharp
internal class RawReplResult
{
    public string Output { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsError { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}
```

## Implementation Steps

1. **Immediate Fix** (Day 1)
   - [ ] Implement EnterRawModeAsync with prompt verification
   - [ ] Fix ReadExecutionResultAsync to handle proper markers
   - [ ] Update ExecuteRawReplAsync with retry logic
   - [ ] Add timeout handling throughout

2. **Testing & Validation** (Day 1-2)
   - [ ] Test with hardware device at `/dev/usb/tty-Board_in_FS_mode-e6614c311b7e6f35`
   - [ ] Verify with subprocess MicroPython instance
   - [ ] Test error conditions and recovery
   - [ ] Validate timeout handling

3. **Polish & Documentation** (Day 2-3)
   - [ ] Add comprehensive logging for debugging
   - [ ] Update XML documentation
   - [ ] Create integration tests for new implementation
   - [ ] Update ICD-001 compliance notes

## Testing Plan

### Unit Tests
```csharp
[Test]
public async Task EnterRawMode_WithCorrectPrompt_ReturnsTrue()
[Test]
public async Task ExecuteCode_WithValidPython_ReturnsOutput()
[Test]
public async Task ExecuteCode_WithSyntaxError_ThrowsDeviceException()
[Test]
public async Task ExecuteCode_WithTimeout_RetriesAndRecovers()
```

### Hardware Integration Tests
1. Connect to Raspberry Pi Pico at specified port
2. Execute simple Python: `print("Hello from Pico")`
3. Execute multi-line code with variables
4. Test error handling with invalid syntax
5. Test timeout recovery with infinite loop
6. Verify session state after errors

## Success Criteria

- [ ] Raw REPL protocol correctly enters and exits raw mode
- [ ] Python code executes successfully on hardware device
- [ ] Output is correctly parsed and returned
- [ ] Errors are properly detected and reported
- [ ] Timeouts are handled gracefully with recovery
- [ ] Integration tests pass on both serial and subprocess connections

## Risk Mitigation

1. **Risk**: Breaking existing subprocess tests
   - **Mitigation**: Test thoroughly with both connection types
   
2. **Risk**: Incompatibility with different MicroPython versions
   - **Mitigation**: Test with multiple firmware versions
   
3. **Risk**: Performance regression from added verification
   - **Mitigation**: Keep timeouts minimal, add caching where possible

## Dependencies

- Hardware device available for testing
- Access to MicroPython subprocess for local testing
- Updated integration test framework

## References

- ICD-001: Raw REPL Protocol Specification
- Issue 001-001: Original Raw REPL Protocol Implementation
- Commit 349a370: Aggressive architectural simplification

## Notes

This fix maintains the simplified architecture approach while ensuring protocol compliance. The solution avoids reintroducing complex abstractions and keeps the implementation direct and understandable per the simplification goals.