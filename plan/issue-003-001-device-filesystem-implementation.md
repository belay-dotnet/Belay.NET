# Issue 003-001: Device File System Implementation

**Epic**: Epic 003 - File Synchronization System  
**Status**: 📋 PLANNED  
**Priority**: Critical  
**Estimated Effort**: 5 days  
**Assignee**: TBD  
**Dependencies**: Epic 001 (✅ COMPLETED)

## Summary

Implement the core `IDeviceFileSystem` interface and `DeviceFileSystem` class that provides fundamental file and directory operations on MicroPython devices. This component serves as the foundation for all file synchronization and management capabilities.

## Acceptance Criteria

### Functional Requirements
- ✅ Implement complete `IDeviceFileSystem` interface
- ✅ Support all basic file operations (read, write, delete)
- ✅ Support directory operations (create, list, remove)
- ✅ Integrate with existing `Device` communication layer
- ✅ Handle both text and binary files correctly
- ✅ Provide async operations with cancellation support

### Technical Requirements
- ✅ Thread-safe implementation for concurrent access
- ✅ Proper error handling with meaningful exceptions
- ✅ Memory-efficient operations suitable for embedded devices
- ✅ Cross-platform path handling (Windows, Linux, macOS)
- ✅ Unit test coverage >95%

### Performance Requirements
- ✅ File read/write operations complete within reasonable time limits
- ✅ Directory listing operations handle up to 100 files efficiently
- ✅ Memory usage stays within 5MB for typical operations

## Technical Specification

### Interface Design
```csharp
namespace Belay.Sync;

public interface IDeviceFileSystem
{
    // File operations
    Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default);
    Task<byte[]> ReadBytesAsync(string path, CancellationToken cancellationToken = default);
    Task WriteTextAsync(string path, string content, CancellationToken cancellationToken = default);
    Task WriteBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string path, CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default);
    Task<DeviceFileInfo> GetFileInfoAsync(string path, CancellationToken cancellationToken = default);
    
    // Directory operations
    Task<IReadOnlyList<DeviceFileInfo>> ListFilesAsync(string path = "/", bool recursive = false, CancellationToken cancellationToken = default);
    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteDirectoryAsync(string path, bool recursive = false, CancellationToken cancellationToken = default);
    Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default);
    
    // Storage information
    Task<DeviceStorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default);
}

public class DeviceFileInfo
{
    public string Path { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public long Size { get; init; }
    public DateTime LastModified { get; init; }
    public bool IsDirectory { get; init; }
    public DeviceFileAttributes Attributes { get; init; }
}

public class DeviceStorageInfo
{
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }
    public long UsedBytes => TotalBytes - FreeBytes;
    public double UsagePercentage => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
}

[Flags]
public enum DeviceFileAttributes
{
    None = 0,
    Hidden = 1,
    System = 2,
    ReadOnly = 4
}
```

### Implementation Strategy

#### MicroPython Integration
The implementation will use MicroPython's built-in filesystem operations:

```python
# File operations
with open(path, 'r') as f: content = f.read()  # Read text
with open(path, 'rb') as f: content = f.read()  # Read binary  
with open(path, 'w') as f: f.write(content)  # Write text
with open(path, 'wb') as f: f.write(content)  # Write binary

# Directory operations
import os
os.listdir(path)  # List directory contents
os.mkdir(path)    # Create directory
os.rmdir(path)    # Remove empty directory
os.remove(path)   # Remove file

# File information
os.stat(path)     # Get file statistics
```

#### Error Handling Strategy
Map MicroPython exceptions to meaningful .NET exceptions:

```csharp
// Custom exceptions for device file operations
public class DeviceFileNotFoundException : DeviceException
public class DeviceDirectoryNotFoundException : DeviceException  
public class DeviceStorageFullException : DeviceException
public class DevicePermissionException : DeviceException
public class DeviceFileSystemException : DeviceException
```

#### Path Normalization
Handle cross-platform path differences:

```csharp
// Internal path utilities
internal static class DevicePathUtilities
{
    public static string NormalizePath(string path) => 
        path.Replace('\\', '/').TrimStart('/');
        
    public static string EnsureAbsolutePath(string path) =>
        path.StartsWith('/') ? path : '/' + path;
        
    public static bool IsValidDevicePath(string path) => 
        // Validate path for device filesystem constraints
}
```

## Implementation Tasks

### Phase 1: Core Infrastructure (Days 1-2)
1. **Create Project Structure**
   - Set up Belay.Sync project
   - Add necessary package references
   - Configure project properties and dependencies

2. **Define Interfaces and Models** 
   - Implement `IDeviceFileSystem` interface
   - Create `DeviceFileInfo` and `DeviceStorageInfo` classes
   - Define exception hierarchy for file operations

3. **Path Utilities**
   - Implement cross-platform path normalization
   - Create path validation utilities
   - Handle special characters and Unicode filenames

### Phase 2: File Operations (Days 2-3)
1. **Basic File Operations**
   - Implement `ReadTextAsync` and `ReadBytesAsync`
   - Implement `WriteTextAsync` and `WriteBytesAsync`
   - Add `FileExistsAsync` and `GetFileInfoAsync`

2. **File Deletion and Management**
   - Implement `DeleteFileAsync` with proper error handling
   - Add file metadata retrieval (size, timestamp)
   - Handle readonly and system files appropriately

3. **Error Handling Integration**
   - Map MicroPython exceptions to .NET exceptions
   - Provide meaningful error messages with context
   - Implement retry logic for transient failures

### Phase 3: Directory Operations (Days 3-4)
1. **Directory Listing**
   - Implement `ListFilesAsync` with recursive support
   - Handle large directories efficiently
   - Support filtering and sorting options

2. **Directory Management**
   - Implement `CreateDirectoryAsync` with parent creation
   - Add `DeleteDirectoryAsync` with recursive support  
   - Implement `DirectoryExistsAsync`

3. **Storage Information**
   - Implement `GetStorageInfoAsync` for device storage stats
   - Calculate free space and usage percentages
   - Handle devices without storage statistics

### Phase 4: Integration and Testing (Days 4-5)
1. **Device Integration**
   - Integrate with existing Device communication layer
   - Add FileSystem property to Device class
   - Ensure consistent error handling and logging

2. **Unit Test Implementation**
   - Create comprehensive unit test suite
   - Mock device communication for testing
   - Test error scenarios and edge cases

3. **Performance Testing**
   - Benchmark file operations with various sizes
   - Test memory usage during operations
   - Validate performance targets

## Testing Strategy

### Unit Tests
```csharp
[TestFixture]
[Category("FileSystem")]
public class DeviceFileSystemTests
{
    private Mock<IDeviceCommunication> mockCommunication;
    private DeviceFileSystem fileSystem;
    
    [Test]
    public async Task ReadTextAsync_ExistingFile_ReturnsContent()
    {
        // Arrange: Mock device response with file content
        // Act: Read text file from device
        // Assert: Content matches expected result
    }
    
    [Test]
    public async Task WriteTextAsync_NewFile_CreatesFile()
    {
        // Arrange: Mock successful file creation
        // Act: Write text content to device
        // Assert: Verify correct MicroPython commands sent
    }
    
    [Test]
    public async Task ListFilesAsync_DirectoryWithFiles_ReturnsFileList()
    {
        // Arrange: Mock directory listing response
        // Act: List files in directory
        // Assert: Correct file information returned
    }
    
    [Test]
    public async Task DeleteFileAsync_NonExistentFile_ThrowsNotFoundException()
    {
        // Arrange: Mock file not found error
        // Act & Assert: Verify correct exception thrown
    }
    
    [Test]
    public async Task GetStorageInfoAsync_ValidDevice_ReturnsStorageStats()
    {
        // Arrange: Mock storage information response
        // Act: Get storage information
        // Assert: Correct storage statistics returned
    }
}
```

### Integration Tests (Requires Hardware)
```csharp
[TestFixture]
[Category("Hardware")]
[Category("FileSystem")]
public class DeviceFileSystemIntegrationTests
{
    [Test]
    public async Task ReadWriteRoundTrip_TextFile_PreservesContent()
    [Test]
    public async Task ReadWriteRoundTrip_BinaryFile_PreservesData()
    [Test]
    public async Task DirectoryOperations_CreateListDelete_WorksCorrectly()
    [Test]
    public async Task LargeFileOperations_1MB_CompletesSuccessfully()
}
```

### Performance Tests
```csharp
[TestFixture]
[Category("Performance")]
[Category("FileSystem")]
public class FileSystemPerformanceTests
{
    [Test]
    public async Task FileOperations_SmallFiles_MeetPerformanceTargets()
    [Test]
    public async Task DirectoryListing_100Files_CompletesQuickly()
    [Test]
    public async Task MemoryUsage_FileOperations_StaysWithinLimits()
}
```

## Risk Assessment

### High Risk Items
- **MicroPython Compatibility**: Different MicroPython versions may have varying filesystem APIs
  - *Mitigation*: Test against multiple MicroPython versions, implement version detection
- **Device Memory Constraints**: Large files may cause memory issues on devices
  - *Mitigation*: Implement chunked operations in subsequent issues

### Medium Risk Items
- **Path Handling Complexity**: Cross-platform path normalization edge cases
  - *Mitigation*: Comprehensive testing on all platforms, robust path utilities
- **Error Message Mapping**: Complex device error scenarios
  - *Mitigation*: Start with basic mapping, enhance based on real-world usage

## Dependencies

### Technical Dependencies
- Device communication layer from Epic 001
- Microsoft.Extensions.Logging for operation logging
- System.IO abstractions for cross-platform file operations

### Internal Dependencies
- None (this is the foundation issue for file system operations)

## Acceptance Testing

### Manual Testing Checklist
- [ ] Create text file on device and verify content
- [ ] Create binary file on device and verify data integrity
- [ ] Create nested directory structure
- [ ] List files in various directories
- [ ] Delete files and directories
- [ ] Handle storage full scenario gracefully
- [ ] Test with special characters in filenames
- [ ] Verify cross-platform path handling

### Automated Testing Goals
- [ ] >95% unit test coverage achieved
- [ ] All integration tests pass on hardware
- [ ] Performance tests meet established targets
- [ ] Cross-platform tests pass on Windows, Linux, and macOS

## Definition of Done

- [ ] All acceptance criteria implemented and validated
- [ ] Complete unit test suite with >95% coverage
- [ ] Integration testing completed on target hardware
- [ ] Performance benchmarks meet requirements
- [ ] Cross-platform compatibility verified
- [ ] Code review completed and approved
- [ ] API documentation updated
- [ ] Integration with Device class completed
- [ ] Error handling comprehensive and user-friendly

This issue establishes the fundamental file system operations that serve as the foundation for all file synchronization, backup, and package management features in Belay.NET.