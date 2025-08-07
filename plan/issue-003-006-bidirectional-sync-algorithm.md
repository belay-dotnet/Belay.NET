# Issue 003-006: Bidirectional Sync Algorithm Implementation

**Epic**: Epic 003 - File Synchronization System  
**Status**: 📋 PLANNED  
**Priority**: Critical  
**Estimated Effort**: 8 days  
**Assignee**: TBD  
**Dependencies**: Issue 003-001, 003-002, 003-005

## Summary

Implement the core bidirectional synchronization algorithm that intelligently compares local and remote file states to determine the optimal set of operations needed to achieve synchronization. This algorithm serves as the heart of the file synchronization system.

## Acceptance Criteria

### Functional Requirements
- ✅ Compare local and remote file hierarchies efficiently
- ✅ Generate optimal synchronization plans with minimal data transfer
- ✅ Support multiple synchronization modes (two-way, one-way, mirror)
- ✅ Handle file additions, modifications, deletions, and renames
- ✅ Detect and report synchronization conflicts
- ✅ Optimize operations to minimize device storage usage during sync

### Technical Requirements
- ✅ Memory-efficient comparison algorithms for large file sets
- ✅ Configurable synchronization policies and preferences
- ✅ Thread-safe execution for concurrent synchronization requests
- ✅ Integration with existing file system and transfer infrastructure
- ✅ Comprehensive logging and diagnostics for troubleshooting

### Performance Requirements
- ✅ Handle projects with up to 500 files efficiently
- ✅ Complete synchronization planning in <10 seconds for typical projects
- ✅ Minimize unnecessary file transfers using timestamps and checksums
- ✅ Memory usage <50MB for synchronization planning operations

## Technical Specification

### Synchronization Algorithm Design

#### Sync Mode Definitions
```csharp
public enum SyncMode
{
    /// <summary>Bidirectional sync - changes flow both directions</summary>
    TwoWay,
    
    /// <summary>Host to device only - device changes are overwritten</summary>  
    HostToDevice,
    
    /// <summary>Device to host only - host changes are overwritten</summary>
    DeviceToHost,
    
    /// <summary>Mirror mode - device becomes exact copy of host</summary>
    Mirror,
    
    /// <summary>Backup mode - device files copied to host, no overwrites</summary>
    Backup
}

public enum ConflictResolution
{
    /// <summary>Skip conflicted files and report them</summary>
    Skip,
    
    /// <summary>Prefer newer files based on timestamp</summary>
    NewerWins,
    
    /// <summary>Prefer larger files (assuming more complete)</summary>
    LargerWins,
    
    /// <summary>Always prefer host version</summary>
    HostWins,
    
    /// <summary>Always prefer device version</summary>
    DeviceWins,
    
    /// <summary>Create backup copies of conflicted files</summary>
    CreateBackup
}
```

#### Synchronization Options
```csharp
public class SyncOptions
{
    /// <summary>Base directory on host filesystem</summary>
    public string HostPath { get; set; } = string.Empty;
    
    /// <summary>Base directory on device filesystem</summary>
    public string DevicePath { get; set; } = "/";
    
    /// <summary>Synchronization mode</summary>
    public SyncMode Mode { get; set; } = SyncMode.TwoWay;
    
    /// <summary>Conflict resolution strategy</summary>
    public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.Skip;
    
    /// <summary>Include patterns (glob-style)</summary>
    public List<string> IncludePatterns { get; set; } = new() { "*" };
    
    /// <summary>Exclude patterns (glob-style)</summary>
    public List<string> ExcludePatterns { get; set; } = new();
    
    /// <summary>Delete files that don't exist on source side</summary>
    public bool DeleteExtraFiles { get; set; } = false;
    
    /// <summary>Preserve file timestamps when possible</summary>
    public bool PreserveTimestamps { get; set; } = true;
    
    /// <summary>Verify file integrity using checksums</summary>
    public bool VerifyChecksums { get; set; } = true;
    
    /// <summary>Create backup of overwritten files</summary>
    public bool CreateBackupOnOverwrite { get; set; } = false;
    
    /// <summary>Dry run mode - plan only, no actual changes</summary>
    public bool DryRun { get; set; } = false;
}
```

#### Synchronization State Model
```csharp
public class FileComparisonResult
{
    public string RelativePath { get; init; } = string.Empty;
    public FileState HostState { get; init; } = FileState.Unknown;
    public FileState DeviceState { get; init; } = FileState.Unknown;
    public SyncAction RequiredAction { get; init; } = SyncAction.None;
    public SyncDirection Direction { get; init; } = SyncDirection.None;
    public ConflictType ConflictType { get; init; } = ConflictType.None;
    public string ConflictReason { get; init; } = string.Empty;
    public long HostSize { get; init; }
    public long DeviceSize { get; init; }
    public DateTime HostModified { get; init; }
    public DateTime DeviceModified { get; init; }
    public string? HostChecksum { get; init; }
    public string? DeviceChecksum { get; init; }
}

public enum FileState
{
    Unknown,
    Missing,
    Present,
    Directory
}

public enum SyncAction
{
    None,
    Copy,
    Update,
    Delete,
    CreateDirectory,
    Skip
}

public enum SyncDirection
{
    None,
    HostToDevice,
    DeviceToHost
}

public enum ConflictType
{
    None,
    BothModified,
    TypeMismatch,    // File vs Directory
    ContentDiffer,   // Same timestamp, different content
    PermissionDenied
}
```

#### Synchronization Plan
```csharp
public class SyncPlan
{
    public SyncOptions Options { get; init; } = new();
    public List<FileComparisonResult> FileOperations { get; init; } = new();
    public List<FileComparisonResult> Conflicts { get; init; } = new();
    public SyncStatistics Statistics { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public TimeSpan PlanningDuration { get; init; }
    
    public IEnumerable<FileComparisonResult> CopyOperations => 
        FileOperations.Where(f => f.RequiredAction == SyncAction.Copy);
    
    public IEnumerable<FileComparisonResult> UpdateOperations => 
        FileOperations.Where(f => f.RequiredAction == SyncAction.Update);
    
    public IEnumerable<FileComparisonResult> DeleteOperations => 
        FileOperations.Where(f => f.RequiredAction == SyncAction.Delete);
        
    public bool HasConflicts => Conflicts.Any();
    public bool RequiresChanges => FileOperations.Any(f => f.RequiredAction != SyncAction.None);
}

public class SyncStatistics
{
    public int TotalFiles { get; init; }
    public int FilesToCopy { get; init; }
    public int FilesToUpdate { get; init; }
    public int FilesToDelete { get; init; }
    public int DirectoriesToCreate { get; init; }
    public int ConflictsFound { get; init; }
    public long TotalBytesToTransfer { get; init; }
    public TimeSpan EstimatedDuration { get; init; }
}
```

### Core Algorithm Implementation

#### Main Synchronization Interface
```csharp
public interface IFileSynchronizer
{
    Task<SyncPlan> PlanSynchronizationAsync(
        SyncOptions options, 
        CancellationToken cancellationToken = default);
    
    Task<SyncResult> ExecuteSynchronizationAsync(
        SyncPlan plan,
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    Task<SyncResult> SynchronizeAsync(
        SyncOptions options,
        IProgress<SyncProgress>? progress = null, 
        CancellationToken cancellationToken = default);
}
```

#### File Comparison Engine
```csharp
internal class FileComparisonEngine
{
    private readonly IDeviceFileSystem deviceFileSystem;
    private readonly ILogger<FileComparisonEngine> logger;
    
    public async Task<List<FileComparisonResult>> CompareFilesAsync(
        string hostPath,
        string devicePath,
        SyncOptions options,
        CancellationToken cancellationToken)
    {
        var results = new List<FileComparisonResult>();
        
        // Get file listings from both sides
        var hostFiles = await GetHostFilesAsync(hostPath, options);
        var deviceFiles = await deviceFileSystem.ListFilesAsync(devicePath, recursive: true, cancellationToken);
        
        // Create unified file set for comparison
        var allPaths = hostFiles.Keys
            .Union(deviceFiles.Select(f => GetRelativePath(f.Path, devicePath)))
            .Distinct();
        
        foreach (var relativePath in allPaths)
        {
            if (!ShouldIncludeFile(relativePath, options))
                continue;
                
            var comparison = await CompareFileAsync(
                relativePath, hostPath, devicePath, hostFiles, deviceFiles, options, cancellationToken);
            
            results.Add(comparison);
        }
        
        return results;
    }
    
    private async Task<FileComparisonResult> CompareFileAsync(
        string relativePath,
        string hostBasePath,
        string deviceBasePath,
        Dictionary<string, FileInfo> hostFiles,
        List<DeviceFileInfo> deviceFiles,
        SyncOptions options,
        CancellationToken cancellationToken)
    {
        var hostFile = hostFiles.TryGetValue(relativePath, out var hf) ? hf : null;
        var deviceFile = deviceFiles.FirstOrDefault(f => 
            GetRelativePath(f.Path, deviceBasePath) == relativePath);
        
        var result = new FileComparisonResult
        {
            RelativePath = relativePath,
            HostState = hostFile != null ? FileState.Present : FileState.Missing,
            DeviceState = deviceFile != null ? FileState.Present : FileState.Missing,
            HostSize = hostFile?.Length ?? 0,
            DeviceSize = deviceFile?.Size ?? 0,
            HostModified = hostFile?.LastWriteTime ?? DateTime.MinValue,
            DeviceModified = deviceFile?.LastModified ?? DateTime.MinValue
        };
        
        // Determine required action based on file states and sync mode
        result = result with 
        { 
            RequiredAction = DetermineRequiredAction(result, options),
            Direction = DetermineSyncDirection(result, options),
            ConflictType = DetectConflict(result, options),
            ConflictReason = GetConflictReason(result, options)
        };
        
        // Calculate checksums if needed for conflict resolution
        if (options.VerifyChecksums && ShouldCalculateChecksums(result))
        {
            result = await AddChecksumsAsync(result, hostBasePath, deviceBasePath, cancellationToken);
        }
        
        return result;
    }
}
```

## Implementation Tasks

### Phase 1: Core Comparison Engine (Days 1-3)
1. **File Listing and Enumeration**
   - Implement host filesystem enumeration with pattern matching
   - Create unified file comparison data structures
   - Add file filtering and exclusion logic

2. **File State Comparison**
   - Implement file state detection (missing, present, directory)
   - Add timestamp and size comparison logic
   - Create file change detection algorithms

3. **Basic Sync Action Determination**
   - Implement sync action logic for different modes
   - Add conflict detection for basic scenarios
   - Create sync direction determination

### Phase 2: Advanced Comparison Logic (Days 3-5)
1. **Checksum Integration**
   - Integrate with checksum validation from Issue 003-004
   - Add intelligent checksum calculation (only when needed)
   - Implement content-based change detection

2. **Conflict Detection and Classification**
   - Implement comprehensive conflict detection
   - Add conflict type classification and reasoning
   - Create conflict resolution strategy application

3. **Sync Mode Implementation**
   - Implement all synchronization modes (two-way, one-way, mirror, backup)
   - Add mode-specific action determination logic
   - Create safety checks for destructive operations

### Phase 3: Optimization and Planning (Days 5-6)
1. **Sync Plan Generation**
   - Implement comprehensive sync plan creation
   - Add operation ordering and dependency management
   - Create statistics calculation and estimation

2. **Performance Optimization**
   - Optimize file comparison algorithms for large file sets
   - Add incremental comparison capabilities
   - Implement efficient memory usage patterns

3. **Plan Validation and Safety**
   - Add plan validation for consistency and safety
   - Implement pre-flight checks (storage space, permissions)
   - Create rollback capability planning

### Phase 4: Execution Engine (Days 6-7)
1. **Plan Execution Framework**
   - Implement sync plan execution engine
   - Add operation ordering and dependency resolution
   - Create atomic operation support with rollback

2. **Progress Reporting Integration**
   - Integrate with progress reporting from Issue 003-008
   - Add detailed operation progress tracking
   - Implement cancellation support during execution

3. **Error Handling and Recovery**
   - Add comprehensive error handling during execution
   - Implement partial failure recovery strategies
   - Create execution result reporting and analysis

### Phase 5: Integration and Testing (Days 7-8)
1. **API Integration**
   - Integrate synchronizer with Device class
   - Add convenience methods and overloads
   - Ensure consistent error handling and logging

2. **Comprehensive Testing**
   - Unit tests for all comparison and execution logic
   - Integration tests with various sync scenarios
   - Performance tests with large file sets

3. **Documentation and Examples**
   - API documentation for synchronization operations
   - Usage examples for different sync modes
   - Troubleshooting guide for common issues

## Testing Strategy

### Unit Tests
```csharp
[TestFixture]
[Category("Synchronization")]
public class SyncAlgorithmTests
{
    [Test]
    public async Task CompareFiles_NewFileOnHost_PlansCopyToDevice()
    [Test]
    public async Task CompareFiles_ModifiedOnBothSides_DetectsConflict()
    [Test]
    public async Task SyncMode_Mirror_PlansDeleteExtraDeviceFiles()
    [Test]
    public async Task ConflictResolution_NewerWins_SelectsNewerFile()
    [Test]
    public async Task PlanExecution_WithCancellation_StopsGracefully()
    
    [TestCase(SyncMode.TwoWay)]
    [TestCase(SyncMode.HostToDevice)]
    [TestCase(SyncMode.DeviceToHost)]
    [TestCase(SyncMode.Mirror)]
    public async Task SyncMode_VariousScenarios_ProducesCorrectPlan(SyncMode mode)
}
```

### Integration Tests (Hardware Required)
```csharp
[TestFixture]
[Category("Hardware")]
[Category("Synchronization")]
public class SyncIntegrationTests
{
    [Test]
    public async Task FullSyncCycle_TwoWay_SynchronizesCorrectly()
    [Test]
    public async Task ConflictScenario_RealFiles_HandlesCorrectly()
    [Test]
    public async Task LargeProject_500Files_CompletesEfficiently()
    [Test]
    public async Task PartialFailure_NetworkIssue_RecoversGracefully()
}
```

### Performance Tests
```csharp
[TestFixture]
[Category("Performance")]
[Category("Synchronization")]
public class SyncPerformanceTests
{
    [Test]
    public async Task PlanGeneration_LargeFileSet_CompletesQuickly()
    [Test]
    public async Task MemoryUsage_SyncPlanning_StaysWithinLimits()
    [Test]
    public async Task IncrementalSync_MinimalChanges_OptimizesTransfers()
}
```

## Risk Assessment

### High Risk Items
- **Algorithm Complexity**: Bidirectional comparison with conflict resolution is inherently complex
  - *Mitigation*: Extensive testing with various scenarios, comprehensive logging, staged rollout
- **Data Loss Risk**: Incorrect sync operations could overwrite or delete important files
  - *Mitigation*: Dry run mode, backup options, comprehensive validation, atomic operations

### Medium Risk Items
- **Performance with Large File Sets**: Algorithm may not scale well to hundreds of files
  - *Mitigation*: Performance testing, optimization for large sets, incremental comparison
- **Cross-Platform Path Issues**: Path handling differences may cause sync failures
  - *Mitigation*: Robust path normalization, extensive cross-platform testing

## Dependencies

### Technical Dependencies
- Issue 003-001: Device File System Implementation (file operations)
- Issue 003-002: File Transfer Protocol with Chunking (efficient transfers)
- Issue 003-005: Synchronization State Management (state tracking)
- System.IO.Enumeration for efficient file enumeration
- Pattern matching libraries for include/exclude patterns

### Hardware Dependencies
- Test devices with various file system configurations
- Cross-platform development environments for testing

## Acceptance Testing

### Manual Testing Checklist
- [ ] Two-way sync with new files on both sides
- [ ] Conflict detection and resolution with modified files
- [ ] Mirror sync that properly deletes extra files
- [ ] Backup mode that preserves all existing files
- [ ] Pattern matching with include/exclude rules
- [ ] Large project sync (100+ files)
- [ ] Sync cancellation and resumption

### Automated Testing Goals
- [ ] >95% unit test coverage for sync algorithm logic
- [ ] All integration tests pass with real hardware
- [ ] Performance tests meet requirements for large file sets
- [ ] Cross-platform compatibility verified

## Definition of Done

- [ ] All synchronization modes implemented and tested
- [ ] Conflict detection and resolution working correctly
- [ ] Comprehensive sync planning with statistics and validation
- [ ] Plan execution with progress reporting and cancellation
- [ ] Integration with existing file system infrastructure
- [ ] Performance requirements met for target file set sizes
- [ ] Cross-platform compatibility verified
- [ ] Comprehensive test coverage with edge cases
- [ ] API documentation complete with examples
- [ ] Code review completed and approved

This issue implements the core intelligence of the file synchronization system, providing the foundation for reliable and efficient bidirectional file synchronization between host and MicroPython devices.