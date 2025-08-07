# Issue 003-009: Backup and Restore System

**Epic**: Epic 003 - File Synchronization System  
**Status**: 📋 PLANNED  
**Priority**: High  
**Estimated Effort**: 6 days  
**Assignee**: TBD  
**Dependencies**: Issue 003-001, 003-002, 003-006

## Summary

Implement a comprehensive backup and restore system that enables complete device filesystem backup, incremental backups, and selective restoration. This system provides data protection and recovery capabilities essential for development and production environments.

## Acceptance Criteria

### Functional Requirements
- ✅ Create complete device filesystem backups
- ✅ Support incremental backups to minimize transfer time
- ✅ Selective file/directory restoration from backups
- ✅ Backup metadata management and versioning
- ✅ Cross-platform backup format compatibility
- ✅ Progress reporting for backup and restore operations

### Technical Requirements
- ✅ Efficient backup format with compression support
- ✅ Integrity verification using checksums
- ✅ Atomic backup operations with rollback capability
- ✅ Configurable retention policies and cleanup
- ✅ Integration with existing file system infrastructure

### Performance Requirements
- ✅ Complete device backup in <2 minutes for typical projects
- ✅ Incremental backup in <30 seconds for minimal changes
- ✅ Restore operations complete in <1 minute for typical projects
- ✅ Backup storage efficiency: <20% overhead vs raw files

## Technical Specification

### Backup System Architecture

#### Backup Configuration
```csharp
public class BackupOptions
{
    /// <summary>Local directory where backups are stored</summary>
    public string BackupDirectory { get; set; } = string.Empty;
    
    /// <summary>Device path to backup (default: entire filesystem)</summary>
    public string DevicePath { get; set; } = "/";
    
    /// <summary>Backup type to create</summary>
    public BackupType Type { get; set; } = BackupType.Full;
    
    /// <summary>Include patterns for selective backup</summary>
    public List<string> IncludePatterns { get; set; } = new() { "*" };
    
    /// <summary>Exclude patterns for selective backup</summary>
    public List<string> ExcludePatterns { get; set; } = new();
    
    /// <summary>Compress backup data to reduce size</summary>
    public bool EnableCompression { get; set; } = true;
    
    /// <summary>Verify backup integrity using checksums</summary>
    public bool VerifyIntegrity { get; set; } = true;
    
    /// <summary>Maximum number of backups to retain</summary>
    public int MaxBackupCount { get; set; } = 10;
    
    /// <summary>Description for this backup</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>Additional metadata to store with backup</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public enum BackupType
{
    /// <summary>Complete backup of all files</summary>
    Full,
    
    /// <summary>Backup only changed files since last backup</summary>
    Incremental,
    
    /// <summary>Backup changed files since last full backup</summary>
    Differential
}
```

#### Restore Configuration
```csharp
public class RestoreOptions
{
    /// <summary>Path to backup file or directory</summary>
    public string BackupPath { get; set; } = string.Empty;
    
    /// <summary>Target device path for restoration</summary>
    public string DevicePath { get; set; } = "/";
    
    /// <summary>Files/directories to restore (empty = all)</summary>
    public List<string> SelectiveRestore { get; set; } = new();
    
    /// <summary>Overwrite existing files during restore</summary>
    public bool OverwriteExisting { get; set; } = true;
    
    /// <summary>Create backup of files being overwritten</summary>
    public bool BackupExisting { get; set; } = false;
    
    /// <summary>Verify restored files using checksums</summary>
    public bool VerifyIntegrity { get; set; } = true;
    
    /// <summary>Restore file timestamps when possible</summary>
    public bool PreserveTimestamps { get; set; } = true;
    
    /// <summary>Continue restore even if some files fail</summary>
    public bool ContinueOnError { get; set; } = true;
}
```

#### Backup Manifest Structure
```csharp
public class BackupManifest
{
    /// <summary>Unique backup identifier</summary>
    public string BackupId { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>Backup creation timestamp</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>Device information at backup time</summary>
    public DeviceInfo DeviceInfo { get; init; } = new();
    
    /// <summary>Backup configuration used</summary>
    public BackupOptions Options { get; init; } = new();
    
    /// <summary>Files included in backup</summary>
    public List<BackupFileEntry> Files { get; init; } = new();
    
    /// <summary>Backup statistics and metadata</summary>
    public BackupStatistics Statistics { get; init; } = new();
    
    /// <summary>Reference to parent backup for incremental backups</summary>
    public string? ParentBackupId { get; init; }
    
    /// <summary>Backup format version</summary>
    public int FormatVersion { get; init; } = 1;
    
    /// <summary>Overall backup integrity checksum</summary>
    public string IntegrityChecksum { get; init; } = string.Empty;
}

public class BackupFileEntry
{
    public string RelativePath { get; init; } = string.Empty;
    public long OriginalSize { get; init; }
    public long CompressedSize { get; init; }
    public DateTime LastModified { get; init; }
    public string Checksum { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public string CompressionMethod { get; init; } = "none";
    public long Offset { get; init; }  // Position in backup file
}

public class BackupStatistics
{
    public int FileCount { get; init; }
    public int DirectoryCount { get; init; }
    public long TotalOriginalSize { get; init; }
    public long TotalCompressedSize { get; init; }
    public double CompressionRatio { get; init; }
    public TimeSpan BackupDuration { get; init; }
    public string BackupPath { get; init; } = string.Empty;
}
```

### Backup Storage Format

#### Backup Archive Structure
```
backup_20250807_143022.blb (Belay Backup file)
├── manifest.json          # Backup manifest and metadata
├── files/                 # File data (compressed if enabled)
│   ├── 001_main.py.gz
│   ├── 002_config.json.gz
│   └── ...
└── checksums.txt          # File integrity checksums
```

#### Incremental Backup Chain
```
full_backup_001.blb        # Full backup baseline
├── incremental_002.blb    # Changes since full backup
├── incremental_003.blb    # Changes since incremental_002
└── incremental_004.blb    # Changes since incremental_003
```

### Core Implementation

#### Backup Interface
```csharp
public interface IDeviceBackup
{
    /// <summary>Create a new backup of the device</summary>
    Task<BackupManifest> CreateBackupAsync(
        BackupOptions options,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>Restore files from a backup</summary>
    Task<RestoreResult> RestoreFromBackupAsync(
        RestoreOptions options,
        IProgress<RestoreProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>List available backups in a directory</summary>
    Task<List<BackupManifest>> ListBackupsAsync(
        string backupDirectory,
        CancellationToken cancellationToken = default);
    
    /// <summary>Validate backup integrity</summary>
    Task<BackupValidationResult> ValidateBackupAsync(
        string backupPath,
        CancellationToken cancellationToken = default);
    
    /// <summary>Clean up old backups according to retention policy</summary>
    Task<BackupCleanupResult> CleanupBackupsAsync(
        string backupDirectory,
        BackupOptions retentionPolicy,
        CancellationToken cancellationToken = default);
    
    /// <summary>Get detailed information about a backup</summary>
    Task<BackupInfo> GetBackupInfoAsync(
        string backupPath,
        CancellationToken cancellationToken = default);
}
```

#### Progress Reporting
```csharp
public class BackupProgress
{
    public string Operation { get; init; } = string.Empty; // "Scanning", "Backing up", "Compressing"
    public string CurrentFile { get; init; } = string.Empty;
    public int ProcessedFiles { get; init; }
    public int TotalFiles { get; init; }
    public long ProcessedBytes { get; init; }
    public long TotalBytes { get; init; }
    public double PercentComplete => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
    public TimeSpan Elapsed { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
    public double ThroughputMBps { get; init; }
}

public class RestoreProgress
{
    public string Operation { get; init; } = string.Empty; // "Extracting", "Restoring", "Verifying"
    public string CurrentFile { get; init; } = string.Empty;
    public int RestoredFiles { get; init; }
    public int TotalFiles { get; init; }
    public long RestoredBytes { get; init; }
    public long TotalBytes { get; init; }
    public double PercentComplete => TotalFiles > 0 ? (double)RestoredFiles / TotalFiles * 100 : 0;
    public TimeSpan Elapsed { get; init; }
    public TimeSpan? EstimatedTimeRemaining { get; init; }
}
```

## Implementation Tasks

### Phase 1: Backup Infrastructure (Days 1-2)
1. **Backup Format Design**
   - Design backup archive format with manifest structure
   - Implement backup file I/O operations
   - Create compression integration using System.IO.Compression

2. **Manifest System**
   - Implement BackupManifest serialization/deserialization
   - Create backup metadata management
   - Add integrity checksum calculation for manifests

3. **File Enumeration and Selection**
   - Implement device file enumeration for backup
   - Add pattern matching for include/exclude filters
   - Create file change detection for incremental backups

### Phase 2: Full Backup Implementation (Days 2-3)
1. **Full Backup Creation**
   - Implement complete device filesystem backup
   - Add file compression and integrity verification
   - Create progress reporting during backup operations

2. **Backup Validation**
   - Implement backup integrity verification
   - Add checksum validation for backed up files
   - Create backup corruption detection and reporting

3. **Storage Management**
   - Implement backup retention policies and cleanup
   - Add disk space management and warnings
   - Create backup history tracking and management

### Phase 3: Incremental Backup System (Days 3-4)
1. **Change Detection**
   - Implement file change detection using timestamps and checksums
   - Create incremental backup chain management
   - Add differential backup support

2. **Incremental Backup Creation**
   - Implement incremental backup generation
   - Add backup chain validation and consistency checking
   - Create merge strategies for incremental chains

3. **Optimization**
   - Optimize incremental backup performance
   - Add smart change detection algorithms
   - Implement backup deduplication where beneficial

### Phase 4: Restore System (Days 4-5)
1. **Full Restore Implementation**
   - Implement complete backup restoration
   - Add file extraction with decompression
   - Create progress reporting during restore operations

2. **Selective Restore**
   - Implement selective file/directory restoration
   - Add restore path mapping and transformation
   - Create conflict resolution during restoration

3. **Restore Validation**
   - Implement restore integrity verification
   - Add checksum validation for restored files
   - Create restoration result reporting and analysis

### Phase 5: Advanced Features and Integration (Days 5-6)
1. **Backup Management Tools**
   - Implement backup listing and information display
   - Add backup comparison and diff capabilities
   - Create backup export/import functionality

2. **Integration with Device Class**
   - Integrate backup system with existing Device API
   - Add convenience methods and shortcuts
   - Ensure consistent error handling and logging

3. **Testing and Documentation**
   - Comprehensive unit and integration testing
   - Performance testing with various backup sizes
   - API documentation and usage examples

## Testing Strategy

### Unit Tests
```csharp
[TestFixture]
[Category("Backup")]
public class BackupSystemTests
{
    [Test]
    public async Task CreateFullBackup_TypicalProject_CompletesSuccessfully()
    [Test]
    public async Task CreateIncrementalBackup_ChangedFiles_BacksUpOnlyChanges()
    [Test]
    public async Task RestoreFromBackup_CompleteRestore_RestoresAllFiles()
    [Test]
    public async Task RestoreSelective_SpecificFiles_RestoresOnlyRequested()
    [Test]
    public async Task BackupValidation_CorruptedBackup_DetectsCorruption()
    [Test]
    public async Task RetentionPolicy_OldBackups_CleansUpProperly()
}
```

### Integration Tests (Hardware Required)
```csharp
[TestFixture]
[Category("Hardware")]
[Category("Backup")]
public class BackupIntegrationTests
{
    [Test]
    public async Task BackupRestoreCycle_RealDevice_PreservesAllData()
    [Test]
    public async Task IncrementalBackupChain_MultipleBackups_RestoresCorrectly()
    [Test]
    public async Task LargeProjectBackup_500Files_CompletesEfficiently()
    [Test]
    public async Task BackupDuringDeviceUse_ConcurrentAccess_HandlesGracefully()
}
```

### Performance Tests
```csharp
[TestFixture]
[Category("Performance")]
[Category("Backup")]
public class BackupPerformanceTests
{
    [Test]
    public async Task FullBackupSpeed_TypicalProject_MeetsTimeTarget()
    [Test]
    public async Task IncrementalBackupSpeed_MinimalChanges_CompletesQuickly()
    [Test]
    public async Task CompressionEfficiency_TextFiles_AchievesGoodRatio()
    [Test]
    public async Task RestoreSpeed_TypicalBackup_MeetsTimeTarget()
}
```

## Risk Assessment

### High Risk Items
- **Backup Corruption**: Corrupted backups could result in complete data loss
  - *Mitigation*: Multiple integrity checks, validation before restore, backup versioning
- **Incremental Chain Complexity**: Complex incremental backup chains may become unreliable
  - *Mitigation*: Chain validation, automatic full backup triggers, repair capabilities

### Medium Risk Items
- **Storage Space Management**: Backups may consume significant disk space
  - *Mitigation*: Compression, retention policies, disk space monitoring
- **Cross-Platform Compatibility**: Backup format must work across different operating systems
  - *Mitigation*: Standard formats, path normalization, extensive cross-platform testing

## Dependencies

### Technical Dependencies
- Issue 003-001: Device File System Implementation (file operations)
- Issue 003-002: File Transfer Protocol with Chunking (efficient transfers)
- Issue 003-006: Bidirectional Sync Algorithm (change detection)
- System.IO.Compression for backup compression
- JSON serialization for manifest handling

### Hardware Dependencies
- Test devices with various storage configurations
- Test backup/restore with different file types and sizes

## Acceptance Testing

### Manual Testing Checklist
- [ ] Create full backup of device with 50+ files
- [ ] Create incremental backup after making changes
- [ ] Restore complete backup to clean device
- [ ] Restore selective files from backup
- [ ] Validate backup integrity after corruption simulation
- [ ] Test retention policy with multiple old backups
- [ ] Cross-platform backup compatibility test

### Automated Testing Goals
- [ ] >90% unit test coverage for backup/restore logic
- [ ] All integration tests pass with real hardware
- [ ] Performance tests meet time and efficiency targets
- [ ] Cross-platform compatibility verified

## Definition of Done

- [ ] Full backup and restore functionality implemented and tested
- [ ] Incremental backup system working with chain management
- [ ] Comprehensive progress reporting and cancellation support
- [ ] Backup validation and integrity checking operational
- [ ] Retention policies and cleanup functionality complete
- [ ] Integration with Device class seamless and documented
- [ ] Performance requirements met for typical use cases
- [ ] Cross-platform compatibility verified
- [ ] Comprehensive test coverage including edge cases
- [ ] API documentation complete with usage examples
- [ ] Code review completed and approved

This issue provides essential data protection and recovery capabilities, enabling developers and operators to safely work with MicroPython devices while having confidence in data backup and restoration capabilities.