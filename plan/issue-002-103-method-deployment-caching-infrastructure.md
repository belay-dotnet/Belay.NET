# Issue 002-103: Method Deployment Caching Infrastructure

**Status**: Not Started  
**Priority**: CRITICAL  
**Estimated Effort**: 1 week  
**Epic**: 002.1 (Architectural Improvements)  
**Dependencies**: Issue 002-102 (Session Management System)

## Problem Statement

The attribute-based programming model requires efficient method deployment to avoid repeatedly sending the same code to devices. The current architecture lacks a caching infrastructure for deployed methods, which will result in poor performance as methods are re-deployed on every invocation. A persistent caching system is needed to optimize method deployment with intelligent invalidation strategies.

## Technical Requirements

### Core Interfaces

```csharp
// Main caching interface
public interface IMethodDeploymentCache
{
    Task<MethodCacheEntry> GetCachedMethodAsync(MethodCacheKey key, CancellationToken cancellationToken = default);
    Task SetCachedMethodAsync(MethodCacheKey key, MethodCacheEntry entry, CancellationToken cancellationToken = default);
    Task InvalidateCacheAsync(MethodCacheKey key, CancellationToken cancellationToken = default);
    Task InvalidateAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MethodCacheKey>> GetCachedKeysAsync(CancellationToken cancellationToken = default);
    
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    Task CompactCacheAsync(CancellationToken cancellationToken = default);
}

// Cache key for method identification
public record MethodCacheKey
{
    public string DeviceId { get; init; }
    public string FirmwareVersion { get; init; }
    public string MethodSignature { get; init; }
    public string CodeHash { get; init; }
    public DateTime CreatedAt { get; init; }
    
    public static MethodCacheKey Create(IDeviceCapabilities capabilities, MethodInfo method, string code)
    {
        var signature = GenerateMethodSignature(method);
        var hash = GenerateCodeHash(code);
        
        return new MethodCacheKey
        {
            DeviceId = capabilities.DeviceType,
            FirmwareVersion = capabilities.FirmwareVersion,
            MethodSignature = signature,
            CodeHash = hash,
            CreatedAt = DateTime.UtcNow
        };
    }
}

// Cache entry with metadata
public record MethodCacheEntry
{
    public string DeployedCode { get; init; }
    public string MethodName { get; init; }
    public DateTime DeployedAt { get; init; }
    public DateTime LastAccessedAt { get; set; }
    public int AccessCount { get; set; }
    public TimeSpan DeploymentTime { get; init; }
    public CacheEntryStatus Status { get; set; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

// Cache configuration
public class MethodCacheConfiguration
{
    public int MaxCacheEntries { get; set; } = 1000;
    public TimeSpan CacheExpirationTime { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan InactiveExpirationTime { get; set; } = TimeSpan.FromHours(6);
    public string CacheDirectory { get; set; } = "cache";
    public bool EnablePersistence { get; set; } = true;
    public bool EnableCompression { get; set; } = true;
}
```

### Implementation Classes

```csharp
// Main cache implementation
internal class MethodDeploymentCache : IMethodDeploymentCache, IDisposable
{
    private readonly MethodCacheConfiguration _configuration;
    private readonly ILogger<MethodDeploymentCache> _logger;
    private readonly ConcurrentDictionary<MethodCacheKey, MethodCacheEntry> _memoryCache = new();
    private readonly IPersistentCacheStorage _persistentStorage;
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    
    public async Task<MethodCacheEntry> GetCachedMethodAsync(MethodCacheKey key, CancellationToken cancellationToken = default)
    {
        // Try memory cache first
        if (_memoryCache.TryGetValue(key, out var entry))
        {
            entry.LastAccessedAt = DateTime.UtcNow;
            entry.AccessCount++;
            return entry;
        }
        
        // Try persistent storage
        if (_configuration.EnablePersistence)
        {
            entry = await _persistentStorage.LoadAsync(key, cancellationToken);
            if (entry != null && !IsExpired(entry))
            {
                // Promote to memory cache
                _memoryCache.TryAdd(key, entry);
                entry.LastAccessedAt = DateTime.UtcNow;
                entry.AccessCount++;
                return entry;
            }
        }
        
        return null;
    }
    
    public async Task SetCachedMethodAsync(MethodCacheKey key, MethodCacheEntry entry, CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Add to memory cache
            _memoryCache.AddOrUpdate(key, entry, (k, existing) => entry);
            
            // Persist if enabled
            if (_configuration.EnablePersistence)
            {
                await _persistentStorage.SaveAsync(key, entry, cancellationToken);
            }
            
            // Enforce cache size limits
            await EnforceCacheLimitsAsync(cancellationToken);
            
            _logger.LogDebug("Cached method deployment: {MethodSignature} for device {DeviceId}", 
                key.MethodSignature, key.DeviceId);
        }
        finally
        {
            _cacheLock.Release();
        }
    }
    
    private async Task EnforceCacheLimitsAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.Count <= _configuration.MaxCacheEntries) return;
        
        // Remove least recently used entries
        var entriesToRemove = _memoryCache
            .OrderBy(kvp => kvp.Value.LastAccessedAt)
            .Take(_memoryCache.Count - _configuration.MaxCacheEntries)
            .ToList();
            
        foreach (var (key, _) in entriesToRemove)
        {
            _memoryCache.TryRemove(key, out _);
        }
    }
    
    private bool IsExpired(MethodCacheEntry entry)
    {
        var age = DateTime.UtcNow - entry.DeployedAt;
        var inactiveTime = DateTime.UtcNow - entry.LastAccessedAt;
        
        return age > _configuration.CacheExpirationTime || 
               inactiveTime > _configuration.InactiveExpirationTime;
    }
}

// Persistent storage interface and implementation
public interface IPersistentCacheStorage
{
    Task<MethodCacheEntry> LoadAsync(MethodCacheKey key, CancellationToken cancellationToken = default);
    Task SaveAsync(MethodCacheKey key, MethodCacheEntry entry, CancellationToken cancellationToken = default);
    Task DeleteAsync(MethodCacheKey key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MethodCacheKey>> GetAllKeysAsync(CancellationToken cancellationToken = default);
    Task CompactAsync(CancellationToken cancellationToken = default);
}

internal class FileSystemCacheStorage : IPersistentCacheStorage
{
    private readonly string _cacheDirectory;
    private readonly bool _enableCompression;
    private readonly ILogger<FileSystemCacheStorage> _logger;
    
    public async Task<MethodCacheEntry> LoadAsync(MethodCacheKey key, CancellationToken cancellationToken = default)
    {
        var filePath = GetCacheFilePath(key);
        
        if (!File.Exists(filePath)) return null;
        
        try
        {
            var jsonData = await File.ReadAllTextAsync(filePath, cancellationToken);
            
            if (_enableCompression)
            {
                jsonData = await DecompressAsync(jsonData, cancellationToken);
            }
            
            return JsonSerializer.Deserialize<MethodCacheEntry>(jsonData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load cached method from {FilePath}", filePath);
            return null;
        }
    }
    
    public async Task SaveAsync(MethodCacheKey key, MethodCacheEntry entry, CancellationToken cancellationToken = default)
    {
        var filePath = GetCacheFilePath(key);
        var directory = Path.GetDirectoryName(filePath);
        
        Directory.CreateDirectory(directory);
        
        try
        {
            var jsonData = JsonSerializer.Serialize(entry, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            
            if (_enableCompression)
            {
                jsonData = await CompressAsync(jsonData, cancellationToken);
            }
            
            await File.WriteAllTextAsync(filePath, jsonData, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save cached method to {FilePath}", filePath);
            throw;
        }
    }
    
    private string GetCacheFilePath(MethodCacheKey key)
    {
        var fileName = $"{key.DeviceId}_{key.FirmwareVersion}_{key.CodeHash}.cache";
        return Path.Combine(_cacheDirectory, "methods", fileName);
    }
}
```

### Integration with Executor Framework

```csharp
// Enhanced TaskExecutor with caching
internal class TaskExecutor : BaseExecutor, ITaskExecutor
{
    private readonly IMethodDeploymentCache _cache;
    
    public async Task<T> InvokeMethodAsync<T>(MethodInfo method, object[] parameters, CancellationToken cancellationToken = default)
    {
        var capabilities = _sessionManager.Capabilities;
        var code = GenerateMethodCode(method, parameters);
        var cacheKey = MethodCacheKey.Create(capabilities, method, code);
        
        // Try to get from cache
        var cachedEntry = await _cache.GetCachedMethodAsync(cacheKey, cancellationToken);
        
        if (cachedEntry != null)
        {
            _logger.LogDebug("Using cached method deployment: {MethodSignature}", cacheKey.MethodSignature);
            return await ExecuteCachedMethodAsync<T>(cachedEntry, parameters, cancellationToken);
        }
        
        // Deploy and cache the method
        var deploymentStart = DateTime.UtcNow;
        var deployedCode = await DeployMethodAsync(method, code, cancellationToken);
        var deploymentTime = DateTime.UtcNow - deploymentStart;
        
        // Create cache entry
        var cacheEntry = new MethodCacheEntry
        {
            DeployedCode = deployedCode,
            MethodName = method.Name,
            DeployedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            AccessCount = 1,
            DeploymentTime = deploymentTime,
            Status = CacheEntryStatus.Active
        };
        
        // Save to cache
        await _cache.SetCachedMethodAsync(cacheKey, cacheEntry, cancellationToken);
        
        // Execute the method
        return await ExecuteDeployedMethodAsync<T>(deployedCode, parameters, cancellationToken);
    }
}
```

## Integration Points

### Session Management Integration
- Cache operations must coordinate with device session state (Issue 002-102)
- Cache invalidation on device disconnection or firmware changes
- Session-aware cache cleanup and resource management

### Executor Framework Integration
- TaskExecutor uses caching for method deployment optimization (Issue 002-101)
- Cache keys include method signatures and device capabilities
- Automatic cache invalidation on method changes

### Dependency Injection Integration
- Cache configuration through dependency injection (Issue 002-104)
- Pluggable storage implementations
- Service lifetime management for cache components

## Implementation Strategy

### Phase 1: Core Caching Infrastructure (Days 1-2)
1. Implement core interfaces and cache key generation
2. Create in-memory cache with basic operations
3. Add cache statistics and monitoring
4. Integrate with basic method deployment flow

### Phase 2: Persistent Storage (Days 3-4)
1. Implement filesystem-based persistent storage
2. Add cache entry serialization and compression
3. Create cache compaction and cleanup mechanisms
4. Add configuration management

### Phase 3: Integration and Optimization (Days 5-6)
1. Integrate with executor framework and session management
2. Add intelligent cache invalidation strategies
3. Implement performance optimizations
4. Add comprehensive monitoring and logging

### Phase 4: Testing and Documentation (Day 7)
1. Create comprehensive unit and integration tests
2. Performance benchmarking and profiling
3. Documentation and usage examples
4. Cross-platform compatibility verification

## Definition of Done

### Functional Requirements
- [ ] Method deployment caching working with cache hits and misses
- [ ] Persistent cache storage operational
- [ ] Cache invalidation strategies implemented
- [ ] Integration with executor framework complete
- [ ] Cache statistics and monitoring available

### Technical Requirements
- [ ] Cache key generation and collision handling working
- [ ] Persistent storage with compression working
- [ ] Cache cleanup and compaction operational
- [ ] Thread-safe cache operations verified
- [ ] Performance benchmarks established

### Quality Requirements
- [ ] Comprehensive unit test coverage >90%
- [ ] Integration tests with real method deployments
- [ ] Performance optimization verified
- [ ] Memory usage profiling completed
- [ ] Cross-platform compatibility maintained

## Dependencies

### Prerequisite Issues
- Issue 002-102: Device Session Management System (required for device capabilities)

### Dependent Issues
- Issue 002-101: Executor Framework Implementation (tight integration)
- Issue 002-104: Dependency Injection Infrastructure (configuration)
- All main Epic 002 issues benefit from caching

## Risk Assessment

### High Risk Items
- **Cache Consistency**: Ensuring cache consistency across application restarts
  - *Mitigation*: Robust persistence layer, cache validation on load
- **Performance Impact**: Cache overhead may impact method execution
  - *Mitigation*: Performance benchmarks, optimization focus

### Medium Risk Items
- **Storage Requirements**: Cache storage may grow large over time
  - *Mitigation*: Cache size limits, compaction strategies, configurable retention
- **Cache Invalidation**: Incorrect cache invalidation may cause stale deployments
  - *Mitigation*: Conservative invalidation strategies, thorough testing

## Testing Requirements

### Unit Testing
- Cache key generation and uniqueness
- Cache entry lifecycle management
- Persistent storage operations
- Cache invalidation logic
- Statistics and monitoring

### Integration Testing
- End-to-end method deployment with caching
- Cache behavior across application restarts
- Integration with executor framework
- Performance impact measurement

### Performance Testing
- Cache hit/miss performance comparison
- Storage and retrieval speed benchmarks
- Memory usage profiling
- Concurrent access performance

## Acceptance Criteria

1. **Cache Functionality**: Cache correctly stores and retrieves method deployments
2. **Performance**: Cache hits provide >80% performance improvement over deployment
3. **Persistence**: Cache survives application restarts with data integrity
4. **Integration**: Seamless integration with executor framework
5. **Cleanup**: Cache properly manages size and removes stale entries
6. **Monitoring**: Comprehensive cache statistics and health monitoring
7. **Configuration**: Configurable cache behavior and storage options

This issue provides the caching infrastructure necessary for efficient method deployment in the attribute-based programming model, significantly improving performance for repeated method calls.