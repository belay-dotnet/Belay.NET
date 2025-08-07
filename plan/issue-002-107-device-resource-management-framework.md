# Issue 002-107: Device Resource Management Framework

**Status**: Not Started  
**Priority**: MEDIUM  
**Estimated Effort**: 1 week  
**Epic**: 002.1 (Architectural Improvements)  
**Dependencies**: Issues 002-101, 002-102 (Executor Framework, Session Management)

## Problem Statement

The current architecture lacks comprehensive device resource management for memory monitoring, concurrent operation management, and performance monitoring. Without proper resource management, devices may run out of memory, operations may conflict, and performance may degrade without detection. A resource management framework is needed to monitor device resources, manage concurrent operations, and provide performance monitoring infrastructure.

## Technical Requirements

### Core Resource Management Interfaces

```csharp
// Main resource manager interface
public interface IDeviceResourceManager
{
    Task<DeviceResourceStatus> GetResourceStatusAsync(CancellationToken cancellationToken = default);
    Task<ResourceAllocationResult> AllocateResourceAsync(ResourceAllocationRequest request, CancellationToken cancellationToken = default);
    Task ReleaseResourceAsync(string allocationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceAllocation>> GetActiveAllocationsAsync(CancellationToken cancellationToken = default);
    
    event EventHandler<ResourceStatusChangedEventArgs> ResourceStatusChanged;
    event EventHandler<ResourceThresholdExceededEventArgs> ThresholdExceeded;
}

// Resource monitoring interfaces
public interface IResourceMonitor
{
    Task<ResourceMetrics> GetCurrentMetricsAsync(CancellationToken cancellationToken = default);
    Task<ResourceTrends> GetTrendsAsync(TimeSpan period, CancellationToken cancellationToken = default);
    Task StartMonitoringAsync(TimeSpan interval, CancellationToken cancellationToken = default);
    Task StopMonitoringAsync(CancellationToken cancellationToken = default);
}

// Concurrent operation management
public interface IConcurrentOperationManager
{
    Task<OperationToken> BeginOperationAsync(OperationRequest request, CancellationToken cancellationToken = default);
    Task CompleteOperationAsync(string operationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActiveOperation>> GetActiveOperationsAsync(CancellationToken cancellationToken = default);
    Task<bool> CanExecuteOperationAsync(OperationRequest request, CancellationToken cancellationToken = default);
    
    int MaxConcurrentOperations { get; set; }
    TimeSpan OperationTimeout { get; set; }
}

// Resource data models
public record DeviceResourceStatus(
    MemoryStatus Memory,
    CpuStatus Cpu,
    StorageStatus Storage,
    NetworkStatus Network,
    DateTime LastUpdated,
    TimeSpan UpdateInterval
);

public record MemoryStatus(
    long TotalBytes,
    long UsedBytes,
    long AvailableBytes,
    double UsagePercentage,
    long LargestFreeBlock
);

public record CpuStatus(
    double UsagePercentage,
    int ActiveThreads,
    TimeSpan UptimeSeconds
);

public record StorageStatus(
    long TotalBytes,
    long UsedBytes,
    long AvailableBytes,
    double UsagePercentage
);

public record ResourceAllocationRequest(
    string OperationId,
    ResourceType ResourceType,
    long RequestedAmount,
    TimeSpan EstimatedDuration,
    ResourcePriority Priority
);

public record ResourceAllocationResult(
    string AllocationId,
    bool Success,
    long AllocatedAmount,
    string Reason
);

public enum ResourceType { Memory, Storage, CpuTime, NetworkBandwidth }
public enum ResourcePriority { Low, Normal, High, Critical }
```

### Implementation Classes

```csharp
// Main resource manager implementation
internal class DeviceResourceManager : IDeviceResourceManager, IDisposable
{
    private readonly IDeviceCommunicator _communicator;
    private readonly IResourceMonitor _monitor;
    private readonly IConcurrentOperationManager _operationManager;
    private readonly ILogger<DeviceResourceManager> _logger;
    private readonly Timer _monitoringTimer;
    private readonly ConcurrentDictionary<string, ResourceAllocation> _allocations = new();
    
    private DeviceResourceStatus _lastStatus;
    private readonly ResourceThresholds _thresholds;
    
    public async Task<DeviceResourceStatus> GetResourceStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var metrics = await _monitor.GetCurrentMetricsAsync(cancellationToken);
            var status = ConvertMetricsToStatus(metrics);
            
            // Check thresholds
            await CheckResourceThresholds(status);
            
            _lastStatus = status;
            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device resource status");
            throw new DeviceResourceException("Failed to retrieve resource status", "resource_monitor", "status_query");
        }
    }
    
    public async Task<ResourceAllocationResult> AllocateResourceAsync(ResourceAllocationRequest request, CancellationToken cancellationToken = default)
    {
        var allocationId = Guid.NewGuid().ToString();
        
        try
        {
            // Check if allocation is possible
            var currentStatus = await GetResourceStatusAsync(cancellationToken);
            var canAllocate = await CanAllocateResourceAsync(request, currentStatus);
            
            if (!canAllocate.CanAllocate)
            {
                return new ResourceAllocationResult(allocationId, false, 0, canAllocate.Reason);
            }
            
            // Perform allocation
            var allocation = new ResourceAllocation(
                allocationId,
                request.OperationId,
                request.ResourceType,
                request.RequestedAmount,
                DateTime.UtcNow,
                request.EstimatedDuration,
                request.Priority);
                
            _allocations.TryAdd(allocationId, allocation);
            
            _logger.LogDebug("Allocated {ResourceType} resource: {Amount} for operation {OperationId}", 
                request.ResourceType, request.RequestedAmount, request.OperationId);
                
            return new ResourceAllocationResult(allocationId, true, request.RequestedAmount, "Resource allocated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate resource for operation {OperationId}", request.OperationId);
            throw new DeviceResourceException($"Resource allocation failed: {ex.Message}", allocationId, request.ResourceType.ToString());
        }
    }
    
    private async Task<(bool CanAllocate, string Reason)> CanAllocateResourceAsync(ResourceAllocationRequest request, DeviceResourceStatus status)
    {
        return request.ResourceType switch
        {
            ResourceType.Memory => await CanAllocateMemoryAsync(request.RequestedAmount, status.Memory),
            ResourceType.Storage => await CanAllocateStorageAsync(request.RequestedAmount, status.Storage),
            ResourceType.CpuTime => await CanAllocateCpuTimeAsync(request.RequestedAmount, status.Cpu),
            _ => (false, $"Unsupported resource type: {request.ResourceType}")
        };
    }
    
    private async Task<(bool CanAllocate, string Reason)> CanAllocateMemoryAsync(long requestedBytes, MemoryStatus memoryStatus)
    {
        var availableBytes = memoryStatus.AvailableBytes;
        var reserveBytes = (long)(memoryStatus.TotalBytes * 0.1); // Keep 10% reserve
        var allocatableBytes = availableBytes - reserveBytes;
        
        if (requestedBytes > allocatableBytes)
        {
            return (false, $"Insufficient memory: requested {requestedBytes} bytes, available {allocatableBytes} bytes");
        }
        
        return (true, "Memory allocation possible");
    }
}

// Resource monitor implementation
internal class DeviceResourceMonitor : IResourceMonitor
{
    private readonly IDeviceCommunicator _communicator;
    private readonly ILogger<DeviceResourceMonitor> _logger;
    private readonly Timer _monitoringTimer;
    private readonly CircularBuffer<ResourceMetrics> _metricsHistory;
    private bool _isMonitoring = false;
    
    public async Task<ResourceMetrics> GetCurrentMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Query device for current resource usage
            var memoryInfo = await QueryDeviceMemoryAsync(cancellationToken);
            var cpuInfo = await QueryDeviceCpuAsync(cancellationToken);
            var storageInfo = await QueryDeviceStorageAsync(cancellationToken);
            
            var metrics = new ResourceMetrics
            {
                Memory = memoryInfo,
                Cpu = cpuInfo,
                Storage = storageInfo,
                Timestamp = DateTime.UtcNow
            };
            
            _metricsHistory.Add(metrics);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current resource metrics");
            throw new DeviceResourceException("Failed to retrieve resource metrics", "resource_monitor", "metrics_query");
        }
    }
    
    private async Task<MemoryMetrics> QueryDeviceMemoryAsync(CancellationToken cancellationToken)
    {
        // Query MicroPython memory information
        var code = @"
import gc
import micropython

# Get memory info
gc.collect()
free_mem = gc.mem_free()
allocated_mem = gc.mem_alloc()
total_mem = free_mem + allocated_mem

# Get largest free block
largest_free = 0
try:
    # This is platform-specific and may not work on all devices
    micropython.mem_info(1)
    largest_free = free_mem  # Simplified - actual implementation would parse mem_info output
except:
    largest_free = free_mem

return {
    'total': total_mem,
    'used': allocated_mem,
    'available': free_mem,
    'largest_free': largest_free
}
";
        
        var result = await _communicator.ExecuteAsync<Dictionary<string, long>>(code, cancellationToken);
        
        return new MemoryMetrics
        {
            TotalBytes = result["total"],
            UsedBytes = result["used"],
            AvailableBytes = result["available"],
            LargestFreeBlock = result["largest_free"]
        };
    }
    
    private async Task<CpuMetrics> QueryDeviceCpuAsync(CancellationToken cancellationToken)
    {
        // Query CPU information (limited on MicroPython)
        var code = @"
import time
import _thread

# Get basic CPU info
active_threads = _thread.get_active_thread_count() if hasattr(_thread, 'get_active_thread_count') else 1
uptime = time.ticks_ms() / 1000.0

return {
    'active_threads': active_threads,
    'uptime_seconds': uptime,
    'usage_percentage': 0.0  # Not easily measurable on MicroPython
}
";
        
        var result = await _communicator.ExecuteAsync<Dictionary<string, object>>(code, cancellationToken);
        
        return new CpuMetrics
        {
            ActiveThreads = Convert.ToInt32(result["active_threads"]),
            UptimeSeconds = TimeSpan.FromSeconds(Convert.ToDouble(result["uptime_seconds"])),
            UsagePercentage = Convert.ToDouble(result["usage_percentage"])
        };
    }
}

// Concurrent operation manager
internal class ConcurrentOperationManager : IConcurrentOperationManager
{
    private readonly ConcurrentDictionary<string, ActiveOperation> _activeOperations = new();
    private readonly ILogger<ConcurrentOperationManager> _logger;
    private readonly Timer _timeoutTimer;
    
    public int MaxConcurrentOperations { get; set; } = 4;
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromMinutes(5);
    
    public async Task<OperationToken> BeginOperationAsync(OperationRequest request, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString();
        
        // Check if we can execute the operation
        var canExecute = await CanExecuteOperationAsync(request, cancellationToken);
        if (!canExecute)
        {
            throw new DeviceResourceException(
                $"Cannot execute operation: maximum concurrent operations ({MaxConcurrentOperations}) reached",
                operationId,
                "operation_limit");
        }
        
        var operation = new ActiveOperation(
            operationId,
            request.Name,
            request.Priority,
            DateTime.UtcNow,
            request.EstimatedDuration);
            
        _activeOperations.TryAdd(operationId, operation);
        
        _logger.LogDebug("Started operation {OperationId}: {OperationName}", operationId, request.Name);
        
        return new OperationToken(operationId, operation);
    }
    
    public async Task<bool> CanExecuteOperationAsync(OperationRequest request, CancellationToken cancellationToken = default)
    {
        var activeCount = _activeOperations.Count;
        
        if (activeCount >= MaxConcurrentOperations)
        {
            // Check if any high priority slots are available
            if (request.Priority >= ResourcePriority.High)
            {
                var lowPriorityCount = _activeOperations.Values.Count(op => op.Priority < ResourcePriority.High);
                return lowPriorityCount > 0; // Can preempt low priority operations
            }
            
            return false;
        }
        
        return true;
    }
}

// Supporting data models
public record ResourceMetrics
{
    public MemoryMetrics Memory { get; init; }
    public CpuMetrics Cpu { get; init; }
    public StorageMetrics Storage { get; init; }
    public DateTime Timestamp { get; init; }
}

public record ResourceAllocation(
    string AllocationId,
    string OperationId,
    ResourceType ResourceType,
    long AllocatedAmount,
    DateTime AllocatedAt,
    TimeSpan EstimatedDuration,
    ResourcePriority Priority
);

public record ActiveOperation(
    string OperationId,
    string Name,
    ResourcePriority Priority,
    DateTime StartedAt,
    TimeSpan EstimatedDuration
);

public record OperationToken(string OperationId, ActiveOperation Operation);

public record OperationRequest(
    string Name,
    ResourcePriority Priority,
    TimeSpan EstimatedDuration
);
```

## Integration Points

### Executor Framework Integration
- Resource allocation before method execution (Issue 002-101)
- Resource monitoring during background thread operations
- Automatic resource cleanup after executor operations

### Session Management Integration
- Resource tracking per session (Issue 002-102)
- Session-level resource quotas and limits
- Resource cleanup on session termination

### Cross-Component Integration
- Resource monitoring for composite operations (Issue 002-106)
- Progress reporting for resource-intensive operations
- Resource allocation coordination across components

## Implementation Strategy

### Phase 1: Core Resource Monitoring (Days 1-2)
1. Implement resource monitoring interfaces and basic device queries
2. Create memory, CPU, and storage monitoring
3. Add resource metrics collection and history tracking
4. Integrate with existing device communication

### Phase 2: Resource Allocation Management (Days 3-4)
1. Implement resource allocation and release mechanisms
2. Add allocation validation and conflict detection
3. Create resource quota and limit enforcement
4. Add threshold monitoring and alerting

### Phase 3: Concurrent Operation Management (Days 5-6)
1. Implement concurrent operation tracking and limits
2. Add operation prioritization and preemption
3. Create operation timeout and cleanup mechanisms
4. Add performance monitoring and optimization

### Phase 4: Integration and Testing (Day 7)
1. Integration with existing components
2. Comprehensive unit and integration testing
3. Performance benchmarking and optimization
4. Documentation and usage examples

## Definition of Done

### Functional Requirements
- [ ] Device resource monitoring operational
- [ ] Resource allocation and release working
- [ ] Concurrent operation management functional
- [ ] Threshold monitoring and alerting working
- [ ] Integration with existing components complete

### Technical Requirements
- [ ] All resource monitoring APIs working
- [ ] Resource allocation validation and enforcement
- [ ] Operation concurrency limits enforced
- [ ] Performance monitoring established
- [ ] Memory management optimized

### Quality Requirements
- [ ] Comprehensive unit test coverage >85%
- [ ] Integration tests with real device resources
- [ ] Performance benchmarks established
- [ ] Resource cleanup verified
- [ ] Cross-platform compatibility maintained

## Dependencies

### Prerequisite Issues
- Issue 002-101: Executor Framework Implementation (resource integration)
- Issue 002-102: Device Session Management System (session resource tracking)

### Dependent Issues
- All Epic 002 issues benefit from resource management
- Future performance optimization initiatives

## Risk Assessment

### High Risk Items
- **Device Compatibility**: Resource monitoring may not work on all MicroPython devices
  - *Mitigation*: Graceful fallbacks, device capability detection
- **Performance Impact**: Resource monitoring overhead may impact device performance
  - *Mitigation*: Configurable monitoring intervals, lightweight queries

### Medium Risk Items
- **Resource Accuracy**: Resource measurements may not be accurate on all platforms
  - *Mitigation*: Platform-specific optimizations, validation testing
- **Concurrency Complexity**: Complex concurrent operation management
  - *Mitigation*: Comprehensive testing, clear operation boundaries

## Testing Requirements

### Unit Testing
- Resource monitoring functionality
- Resource allocation validation
- Concurrent operation management
- Threshold detection and alerting
- Integration with communication layer

### Integration Testing
- End-to-end resource management scenarios
- Resource monitoring with real devices
- Concurrent operation limits and preemption
- Resource cleanup during failures
- Performance impact assessment

### Performance Testing
- Resource monitoring overhead measurement
- Resource allocation performance
- Concurrent operation handling
- Memory usage profiling

## Acceptance Criteria

1. **Resource Monitoring**: Accurate monitoring of device memory, CPU, and storage
2. **Resource Allocation**: Working resource allocation and release mechanisms
3. **Concurrency Management**: Concurrent operation limits and prioritization working
4. **Threshold Monitoring**: Automatic threshold detection and alerting
5. **Performance**: <5% overhead for resource monitoring operations
6. **Integration**: Seamless integration with existing components
7. **Reliability**: Robust resource cleanup and error handling

This issue provides comprehensive device resource management infrastructure to monitor device resources, manage concurrent operations, and ensure optimal performance across all Belay.NET operations.