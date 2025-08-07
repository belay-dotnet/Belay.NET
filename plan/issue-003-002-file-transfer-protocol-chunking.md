# Issue 003-002: File Transfer Protocol with Chunking

**Epic**: Epic 003 - File Synchronization System  
**Status**: 📋 PLANNED  
**Priority**: Critical  
**Estimated Effort**: 6 days  
**Assignee**: TBD  
**Dependencies**: Issue 003-001 (Device File System Implementation)

## Summary

Implement a robust file transfer protocol that handles chunked reading and writing of files to accommodate MicroPython device memory constraints. This issue extends the basic file operations with progress reporting, cancellation support, and adaptive chunking strategies.

## Acceptance Criteria

### Functional Requirements
- ✅ Implement chunked file reading for large files (>64KB)
- ✅ Implement chunked file writing with flow control
- ✅ Support progress reporting for all file transfer operations
- ✅ Handle cancellation gracefully during transfers
- ✅ Automatic retry mechanism for failed chunks
- ✅ Adaptive chunk sizing based on device capabilities

### Technical Requirements
- ✅ Memory-efficient transfers that work with 64KB device RAM
- ✅ Binary data integrity validation using checksums
- ✅ Thread-safe progress reporting
- ✅ Configurable transfer parameters (chunk size, timeout, retries)
- ✅ Integration with existing DeviceFileSystem implementation

### Performance Requirements
- ✅ Support files up to device storage limits (typically 2MB)
- ✅ Transfer speed: minimum 50KB/s for sustained operations
- ✅ Memory overhead: <1MB on host, <32KB on device
- ✅ Progress reporting with <1 second update intervals

## Technical Specification

### Chunked Transfer Protocol Design

#### Transfer Configuration
```csharp
public class FileTransferOptions
{
    /// <summary>Initial chunk size for transfers (adaptive sizing will adjust)</summary>
    public int InitialChunkSize { get; set; } = 4096; // 4KB default
    
    /// <summary>Maximum chunk size allowed</summary>
    public int MaxChunkSize { get; set; } = 32768; // 32KB max
    
    /// <summary>Minimum chunk size for adaptive sizing</summary>
    public int MinChunkSize { get; set; } = 1024; // 1KB min
    
    /// <summary>Timeout per chunk transfer</summary>
    public TimeSpan ChunkTimeout { get; set; } = TimeSpan.FromSeconds(10);
    
    /// <summary>Maximum retry attempts for failed chunks</summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>Enable checksum validation for transfers</summary>
    public bool ValidateChecksums { get; set; } = true;
    
    /// <summary>Progress reporting interval</summary>
    public TimeSpan ProgressInterval { get; set; } = TimeSpan.FromMilliseconds(500);
}
```

#### Progress Reporting
```csharp
public class FileTransferProgress
{
    public string FilePath { get; init; } = string.Empty;
    public long TotalBytes { get; init; }
    public long TransferredBytes { get; init; }
    public double PercentComplete => TotalBytes > 0 ? (double)TransferredBytes / TotalBytes * 100 : 0;
    public TimeSpan Elapsed { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
    public double TransferRateKBps { get; init; }
    public string Operation { get; init; } = string.Empty; // "Reading" or "Writing"
    public int CurrentChunk { get; init; }
    public int TotalChunks { get; init; }
}

public delegate void FileTransferProgressHandler(FileTransferProgress progress);
```

#### Extended File System Interface
```csharp
public interface IDeviceFileSystem
{
    // Existing methods from Issue 003-001...
    
    // Chunked operations with progress reporting
    Task<string> ReadTextChunkedAsync(
        string path, 
        FileTransferOptions? options = null,
        FileTransferProgressHandler? progressCallback = null,
        CancellationToken cancellationToken = default);
    
    Task<byte[]> ReadBytesChunkedAsync(
        string path,
        FileTransferOptions? options = null, 
        FileTransferProgressHandler? progressCallback = null,
        CancellationToken cancellationToken = default);
    
    Task WriteTextChunkedAsync(
        string path, 
        string content,
        FileTransferOptions? options = null,
        FileTransferProgressHandler? progressCallback = null,
        CancellationToken cancellationToken = default);
    
    Task WriteBytesChunkedAsync(
        string path, 
        byte[] content,
        FileTransferOptions? options = null,
        FileTransferProgressHandler? progressCallback = null,
        CancellationToken cancellationToken = default);
    
    // Stream-based operations for very large files
    Task WriteStreamAsync(
        string path,
        Stream sourceStream,
        FileTransferOptions? options = null,
        FileTransferProgressHandler? progressCallback = null,
        CancellationToken cancellationToken = default);
    
    Task ReadToStreamAsync(
        string path,
        Stream destinationStream,
        FileTransferOptions? options = null,
        FileTransferProgressHandler? progressCallback = null,
        CancellationToken cancellationToken = default);
}
```

### MicroPython Protocol Implementation

#### Chunked Writing Strategy
```python
# Device-side chunked write implementation
def write_file_chunked(path, total_size):
    """Initialize chunked file writing"""
    try:
        f = open(path, 'wb')
        return {'success': True, 'handle': id(f)}
    except Exception as e:
        return {'success': False, 'error': str(e)}

def write_chunk(handle_id, chunk_data, chunk_index):
    """Write a single chunk to the file"""
    try:
        # Find file handle by ID (implementation detail)
        f = get_file_handle(handle_id)
        f.write(chunk_data)
        f.flush()  # Ensure data is written
        return {'success': True, 'bytes_written': len(chunk_data)}
    except Exception as e:
        return {'success': False, 'error': str(e)}

def finalize_write(handle_id, expected_checksum=None):
    """Finalize the chunked write operation"""
    try:
        f = get_file_handle(handle_id)
        f.close()
        
        if expected_checksum:
            # Verify file integrity
            actual_checksum = calculate_file_checksum(f.name)
            if actual_checksum != expected_checksum:
                return {'success': False, 'error': 'Checksum mismatch'}
        
        return {'success': True}
    except Exception as e:
        return {'success': False, 'error': str(e)}
```

#### Adaptive Chunk Sizing Algorithm
```csharp
internal class AdaptiveChunkSizer
{
    private int currentChunkSize;
    private readonly FileTransferOptions options;
    private readonly Queue<(TimeSpan duration, bool success)> performanceHistory;
    
    public int GetNextChunkSize(TimeSpan lastTransferTime, bool lastTransferSuccess)
    {
        performanceHistory.Enqueue((lastTransferTime, lastTransferSuccess));
        
        // Keep only recent history (last 10 operations)
        while (performanceHistory.Count > 10)
            performanceHistory.Dequeue();
        
        if (!lastTransferSuccess)
        {
            // Decrease chunk size on failure
            currentChunkSize = Math.Max(options.MinChunkSize, 
                                      (int)(currentChunkSize * 0.75));
        }
        else if (ShouldIncreaseChunkSize())
        {
            // Increase chunk size if performance is good
            currentChunkSize = Math.Min(options.MaxChunkSize, 
                                      (int)(currentChunkSize * 1.25));
        }
        
        return currentChunkSize;
    }
    
    private bool ShouldIncreaseChunkSize()
    {
        // Increase size if recent transfers are consistently fast
        var recentSuccesses = performanceHistory
            .Where(h => h.success && h.duration < TimeSpan.FromSeconds(2))
            .Count();
        
        return recentSuccesses >= 3 && currentChunkSize < options.MaxChunkSize;
    }
}
```

## Implementation Tasks

### Phase 1: Core Chunking Infrastructure (Days 1-2)
1. **Transfer Configuration System**
   - Implement `FileTransferOptions` with sensible defaults
   - Create configuration validation logic
   - Add device capability detection

2. **Progress Reporting Framework**
   - Implement `FileTransferProgress` class
   - Create thread-safe progress notification system
   - Add progress calculation utilities

3. **Chunk Management**
   - Implement chunking algorithms for read and write operations
   - Create chunk size calculation and validation
   - Add chunk retry and error handling logic

### Phase 2: Chunked Reading Implementation (Days 2-3)
1. **Device-Side Read Protocol**
   - Implement MicroPython chunked reading functions
   - Add file position management and seeking
   - Create error handling for partial reads

2. **Host-Side Read Implementation**
   - Implement `ReadBytesChunkedAsync` with progress reporting
   - Add `ReadTextChunkedAsync` with encoding handling
   - Implement chunk assembly and validation

3. **Stream-Based Reading**
   - Implement `ReadToStreamAsync` for large file streaming
   - Add memory-efficient stream handling
   - Support for partial file reading and resumption

### Phase 3: Chunked Writing Implementation (Days 3-4)
1. **Device-Side Write Protocol**
   - Implement MicroPython chunked writing functions  
   - Add file handle management for concurrent operations
   - Create atomic write operations with rollback

2. **Host-Side Write Implementation**
   - Implement `WriteBytesChunkedAsync` with progress reporting
   - Add `WriteTextChunkedAsync` with encoding handling
   - Implement chunk transmission and verification

3. **Stream-Based Writing**
   - Implement `WriteStreamAsync` for large file streaming
   - Add memory-efficient stream handling
   - Support for resumable uploads

### Phase 4: Advanced Features (Days 4-5)
1. **Adaptive Chunk Sizing**
   - Implement `AdaptiveChunkSizer` class
   - Add performance monitoring and adjustment
   - Create device capability profiling

2. **Checksum Validation**
   - Implement file integrity checking using MD5/SHA256
   - Add checksum calculation on both host and device
   - Create corruption detection and recovery

3. **Error Recovery**
   - Implement chunk retry logic with exponential backoff
   - Add partial transfer resumption capabilities
   - Create corruption recovery strategies

### Phase 5: Integration and Testing (Days 5-6)
1. **Integration with DeviceFileSystem**
   - Update existing file operations to use chunking for large files
   - Add automatic fallback to chunked operations
   - Maintain backward compatibility

2. **Comprehensive Testing**
   - Unit tests for all chunking algorithms
   - Integration tests with various file sizes
   - Performance tests and benchmarking

3. **Documentation and Examples**
   - API documentation for chunked operations
   - Usage examples for different scenarios
   - Performance tuning guidelines

## Testing Strategy

### Unit Tests
```csharp
[TestFixture]
[Category("FileTransfer")]
public class ChunkedFileTransferTests
{
    [Test]
    public async Task ReadBytesChunked_LargeFile_ReturnsCompleteContent()
    {
        // Test reading a 1MB file in chunks
    }
    
    [Test]
    public async Task WriteBytesChunked_WithProgress_ReportsProgress()
    {
        // Test progress reporting during chunked write
    }
    
    [Test]
    public async Task AdaptiveChunkSizing_SlowDevice_ReducesChunkSize()
    {
        // Test adaptive sizing with simulated slow device
    }
    
    [Test]
    public async Task ChunkedTransfer_WithCancellation_CancelsGracefully()
    {
        // Test cancellation during chunked operations
    }
    
    [Test]
    public async Task ChecksumValidation_CorruptedFile_DetectsError()
    {
        // Test checksum validation with simulated corruption
    }
}
```

### Integration Tests (Hardware Required)
```csharp
[TestFixture]
[Category("Hardware")]
[Category("FileTransfer")]
public class ChunkedTransferIntegrationTests
{
    [Test]
    public async Task TransferLargeFile_1MB_CompletesSuccessfully()
    [Test]
    public async Task ConcurrentChunkedTransfers_MultipleFiles_HandledCorrectly()
    [Test]
    public async Task DeviceMemoryConstraint_LimitedRAM_AdaptsChunkSize()
    [Test]
    public async Task NetworkInterruption_USBDisconnect_RecoversGracefully()
}
```

### Performance Tests
```csharp
[TestFixture]
[Category("Performance")]
[Category("FileTransfer")]
public class FileTransferPerformanceTests
{
    [Test]
    public async Task ChunkedTransfer_ThroughputTest_MeetsMinimumSpeed()
    [Test]
    public async Task MemoryUsage_LargeFileTransfer_StaysWithinLimits()
    [Test]
    public async Task AdaptiveChunking_PerformanceOptimization_ImprovesSpeed()
}
```

## Risk Assessment

### High Risk Items
- **Device Memory Exhaustion**: Large chunks may cause device crashes
  - *Mitigation*: Conservative initial chunk sizes, adaptive sizing with memory monitoring
- **Transfer Reliability**: Network/USB interruptions during long transfers
  - *Mitigation*: Comprehensive retry logic, resumable transfers, integrity checking

### Medium Risk Items  
- **Performance Optimization**: Balancing chunk size vs transfer speed
  - *Mitigation*: Adaptive algorithms, extensive performance testing, configurable parameters
- **Checksum Overhead**: Integrity checking may slow transfers significantly
  - *Mitigation*: Optional validation, efficient algorithms, streaming checksums

## Dependencies

### Technical Dependencies
- Issue 003-001: Device File System Implementation (foundation)
- Cryptographic libraries for checksum calculations
- System.IO.Hashing for efficient hash calculations

### Hardware Dependencies
- Test devices with various RAM configurations (32KB, 128KB, 512KB)
- Different storage types (internal flash, SD card)

## Acceptance Testing

### Manual Testing Checklist
- [ ] Transfer 1MB file successfully with progress reporting
- [ ] Cancel long-running transfer and verify cleanup
- [ ] Test with very limited device memory (64KB RAM)
- [ ] Verify checksum validation catches corruption
- [ ] Test adaptive chunk sizing with slow/fast devices
- [ ] Confirm memory usage stays within limits

### Automated Testing Goals
- [ ] >95% unit test coverage for chunking logic
- [ ] All integration tests pass on target hardware
- [ ] Performance tests meet speed and memory requirements
- [ ] Stress tests with various file sizes and device configurations

## Definition of Done

- [ ] All chunked transfer operations implemented and working
- [ ] Progress reporting system complete with examples
- [ ] Adaptive chunk sizing algorithm validated
- [ ] Checksum validation system operational
- [ ] Comprehensive error handling and recovery
- [ ] Integration tests passing on real hardware
- [ ] Performance benchmarks meet requirements  
- [ ] Documentation complete with usage examples
- [ ] Code review completed and approved

This issue establishes the robust file transfer foundation needed for efficient synchronization operations while respecting the memory and performance constraints of MicroPython devices.