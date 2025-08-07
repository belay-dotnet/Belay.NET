# Issue 002-106: Cross-Component Integration Layer

**Status**: Not Started  
**Priority**: HIGH  
**Estimated Effort**: 1 week  
**Epic**: 002.1 (Architectural Improvements)  
**Dependencies**: Issues 002-101, 002-102, 002-105 (Executor Framework, Session Management, Exception Handling)

## Problem Statement

The current architecture lacks a unified integration layer to coordinate between file system, device communication, executor framework, and session management components. Components operate independently without shared progress reporting, cancellation coordination, or unified session context. A cross-component integration layer is needed to provide unified operation coordination, shared progress reporting, and consistent session context management.

## Technical Requirements

### Core Integration Interfaces

```csharp
// Main integration coordinator
public interface IOperationCoordinator
{
    Task<TResult> ExecuteCoordinatedOperationAsync<TResult>(
        ICoordinatedOperation<TResult> operation, 
        CancellationToken cancellationToken = default);
        
    Task ExecuteCoordinatedOperationAsync(
        ICoordinatedOperation operation, 
        CancellationToken cancellationToken = default);
        
    Task<TResult> ExecuteWithProgressAsync<TResult>(
        IProgressReportingOperation<TResult> operation,
        IProgress<OperationProgress> progress = null,
        CancellationToken cancellationToken = default);
}

// Coordinated operation interface
public interface ICoordinatedOperation<TResult>
{
    string OperationId { get; }
    string OperationName { get; }
    TimeSpan EstimatedDuration { get; }
    IReadOnlyList<string> RequiredCapabilities { get; }
    
    Task<TResult> ExecuteAsync(IOperationContext context, CancellationToken cancellationToken);
    Task<OperationPreflightResult> PreflightCheckAsync(IOperationContext context);
    Task CleanupAsync(IOperationContext context, Exception exception = null);
}

public interface ICoordinatedOperation : ICoordinatedOperation<object>
{
    new Task ExecuteAsync(IOperationContext context, CancellationToken cancellationToken);
}

// Operation context with cross-component access
public interface IOperationContext
{
    string SessionId { get; }
    IDeviceSession DeviceSession { get; }
    IDeviceCommunicator Communicator { get; }
    ITaskExecutor TaskExecutor { get; }
    IFileSystemContext FileSystemContext { get; }
    IProgressReporter ProgressReporter { get; }
    IOperationLogger Logger { get; }
    
    Task<T> GetSharedStateAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetSharedStateAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    Task NotifyComponentAsync(string component, string message, object data = null);
}

// Progress reporting for complex operations
public interface IProgressReportingOperation<TResult> : ICoordinatedOperation<TResult>
{
    IReadOnlyList<ProgressStage> ProgressStages { get; }
}

public interface IProgressReporter
{
    Task ReportProgressAsync(string stage, double percentage, string message = null, CancellationToken cancellationToken = default);
    Task ReportStageCompletedAsync(string stage, CancellationToken cancellationToken = default);
    Task ReportOperationCompletedAsync(CancellationToken cancellationToken = default);
}

public record ProgressStage(string Name, string Description, double EstimatedWeight);
public record OperationProgress(string Stage, double Percentage, string Message, DateTime Timestamp);
```

### Implementation Classes

```csharp
// Main operation coordinator implementation
internal class OperationCoordinator : IOperationCoordinator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDeviceSessionManager _sessionManager;
    private readonly IGlobalExceptionHandler _exceptionHandler;
    private readonly ILogger<OperationCoordinator> _logger;
    private readonly ConcurrentDictionary<string, OperationContext> _activeOperations = new();
    
    public async Task<TResult> ExecuteCoordinatedOperationAsync<TResult>(
        ICoordinatedOperation<TResult> operation, 
        CancellationToken cancellationToken = default)
    {
        var operationId = operation.OperationId;
        var context = await CreateOperationContextAsync(operationId, cancellationToken);
        
        try
        {
            // Preflight checks
            var preflightResult = await operation.PreflightCheckAsync(context);
            if (!preflightResult.CanProceed)
            {
                throw new BelayValidationException(
                    $"Preflight check failed: {preflightResult.Reason}", 
                    operationId, 
                    preflightResult.ValidationErrors);
            }
            
            // Execute operation with error handling
            _logger.LogInformation("Starting coordinated operation {OperationId}: {OperationName}", 
                operationId, operation.OperationName);
                
            var result = await _exceptionHandler.ExecuteWithErrorHandlingAsync(
                () => operation.ExecuteAsync(context, cancellationToken),
                $"Operation:{operation.OperationName}");
                
            _logger.LogInformation("Completed coordinated operation {OperationId}", operationId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Coordinated operation {OperationId} failed", operationId);
            
            // Cleanup on error
            try
            {
                await operation.CleanupAsync(context, ex);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Cleanup failed for operation {OperationId}", operationId);
            }
            
            throw;
        }
        finally
        {
            await DisposeOperationContextAsync(operationId);
        }
    }
    
    public async Task<TResult> ExecuteWithProgressAsync<TResult>(
        IProgressReportingOperation<TResult> operation,
        IProgress<OperationProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        var operationId = operation.OperationId;
        var context = await CreateOperationContextAsync(operationId, cancellationToken);
        
        // Setup progress reporting
        if (progress != null)
        {
            context.ProgressReporter = new ProgressReporter(progress, operation.ProgressStages);
        }
        
        return await ExecuteCoordinatedOperationAsync(operation, cancellationToken);
    }
    
    private async Task<OperationContext> CreateOperationContextAsync(string operationId, CancellationToken cancellationToken)
    {
        var session = await _sessionManager.CreateSessionAsync(cancellationToken);
        var context = new OperationContext(
            operationId,
            session,
            _serviceProvider.GetRequiredService<IDeviceCommunicator>(),
            _serviceProvider.GetRequiredService<ITaskExecutor>(),
            _logger,
            _serviceProvider);
            
        _activeOperations.TryAdd(operationId, context);
        return context;
    }
}

// Operation context implementation
internal class OperationContext : IOperationContext, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, object> _sharedState = new();
    
    public string SessionId { get; }
    public IDeviceSession DeviceSession { get; }
    public IDeviceCommunicator Communicator { get; }
    public ITaskExecutor TaskExecutor { get; }
    public IFileSystemContext FileSystemContext { get; }
    public IProgressReporter ProgressReporter { get; set; }
    public IOperationLogger Logger { get; }
    
    public async Task<T> GetSharedStateAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        return _sharedState.TryGetValue(key, out var value) ? (T)value : default(T);
    }
    
    public async Task SetSharedStateAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        _sharedState.AddOrUpdate(key, value, (k, v) => value);
    }
    
    public async Task NotifyComponentAsync(string component, string message, object data = null)
    {
        Logger.LogInformation("Component notification from {Component}: {Message}", component, message);
        
        // Could be extended to support component-specific message routing
        if (data != null)
        {
            await SetSharedStateAsync($"notification_{component}_{DateTime.UtcNow.Ticks}", data);
        }
    }
}

// Progress reporter implementation
internal class ProgressReporter : IProgressReporter
{
    private readonly IProgress<OperationProgress> _progress;
    private readonly IReadOnlyList<ProgressStage> _stages;
    private readonly Dictionary<string, bool> _completedStages = new();
    private double _totalWeight;
    private double _completedWeight;
    
    public ProgressReporter(IProgress<OperationProgress> progress, IReadOnlyList<ProgressStage> stages)
    {
        _progress = progress;
        _stages = stages;
        _totalWeight = stages.Sum(s => s.EstimatedWeight);
    }
    
    public async Task ReportProgressAsync(string stage, double percentage, string message = null, CancellationToken cancellationToken = default)
    {
        var stageInfo = _stages.FirstOrDefault(s => s.Name == stage);
        if (stageInfo == null) return;
        
        var stageWeight = stageInfo.EstimatedWeight * (percentage / 100.0);
        var overallPercentage = (_completedWeight + stageWeight) / _totalWeight * 100.0;
        
        _progress?.Report(new OperationProgress(stage, overallPercentage, message ?? stageInfo.Description, DateTime.UtcNow));
    }
    
    public async Task ReportStageCompletedAsync(string stage, CancellationToken cancellationToken = default)
    {
        if (_completedStages.ContainsKey(stage)) return;
        
        var stageInfo = _stages.FirstOrDefault(s => s.Name == stage);
        if (stageInfo != null)
        {
            _completedStages[stage] = true;
            _completedWeight += stageInfo.EstimatedWeight;
            
            var overallPercentage = _completedWeight / _totalWeight * 100.0;
            _progress?.Report(new OperationProgress(stage, 100.0, $"Completed: {stageInfo.Description}", DateTime.UtcNow));
        }
    }
    
    public async Task ReportOperationCompletedAsync(CancellationToken cancellationToken = default)
    {
        _progress?.Report(new OperationProgress("Complete", 100.0, "Operation completed successfully", DateTime.UtcNow));
    }
}
```

### Composite Operations

```csharp
// Base class for composite operations spanning multiple components
public abstract class CompositeOperation<TResult> : IProgressReportingOperation<TResult>
{
    public string OperationId { get; } = Guid.NewGuid().ToString();
    public abstract string OperationName { get; }
    public abstract TimeSpan EstimatedDuration { get; }
    public abstract IReadOnlyList<string> RequiredCapabilities { get; }
    public abstract IReadOnlyList<ProgressStage> ProgressStages { get; }
    
    public async Task<TResult> ExecuteAsync(IOperationContext context, CancellationToken cancellationToken)
    {
        var result = default(TResult);
        
        foreach (var stage in ProgressStages)
        {
            await context.ProgressReporter?.ReportProgressAsync(stage.Name, 0, $"Starting: {stage.Description}", cancellationToken);
            
            result = await ExecuteStageAsync(stage.Name, result, context, cancellationToken);
            
            await context.ProgressReporter?.ReportStageCompletedAsync(stage.Name, cancellationToken);
        }
        
        await context.ProgressReporter?.ReportOperationCompletedAsync(cancellationToken);
        return result;
    }
    
    protected abstract Task<TResult> ExecuteStageAsync(string stageName, TResult previousResult, IOperationContext context, CancellationToken cancellationToken);
    
    public virtual async Task<OperationPreflightResult> PreflightCheckAsync(IOperationContext context)
    {
        var errors = new List<string>();
        
        // Check device capabilities
        foreach (var capability in RequiredCapabilities)
        {
            if (!context.DeviceSession.Context.DeviceInfo.SupportedFeatures.Contains(capability))
            {
                errors.Add($"Required capability '{capability}' not supported by device");
            }
        }
        
        var canProceed = errors.Count == 0;
        var reason = canProceed ? "All preflight checks passed" : $"Preflight checks failed: {string.Join(", ", errors)}";
        
        return new OperationPreflightResult(canProceed, reason, errors);
    }
    
    public virtual async Task CleanupAsync(IOperationContext context, Exception exception = null)
    {
        // Default cleanup - can be overridden by derived classes
        if (exception != null)
        {
            context.Logger.LogError(exception, "Operation {OperationName} failed, performing cleanup", OperationName);
        }
    }
}

// Example composite operation
public class DeployAndExecuteMethodOperation : CompositeOperation<object>
{
    private readonly MethodInfo _method;
    private readonly object[] _parameters;
    
    public override string OperationName => $"Deploy and Execute: {_method.Name}";
    public override TimeSpan EstimatedDuration => TimeSpan.FromSeconds(5);
    public override IReadOnlyList<string> RequiredCapabilities => new[] { "code_execution", "method_deployment" };
    
    public override IReadOnlyList<ProgressStage> ProgressStages => new[]
    {
        new ProgressStage("validate", "Validate method and parameters", 0.1),
        new ProgressStage("deploy", "Deploy method to device", 0.6),
        new ProgressStage("execute", "Execute method on device", 0.3)
    };
    
    protected override async Task<object> ExecuteStageAsync(string stageName, object previousResult, IOperationContext context, CancellationToken cancellationToken)
    {
        return stageName switch
        {
            "validate" => await ValidateMethodAsync(context, cancellationToken),
            "deploy" => await DeployMethodAsync(context, cancellationToken),
            "execute" => await ExecuteMethodAsync(context, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown stage: {stageName}")
        };
    }
    
    private async Task<bool> ValidateMethodAsync(IOperationContext context, CancellationToken cancellationToken)
    {
        await context.ProgressReporter?.ReportProgressAsync("validate", 50, "Validating method signature", cancellationToken);
        // Method validation logic
        await context.ProgressReporter?.ReportProgressAsync("validate", 100, "Method validation complete", cancellationToken);
        return true;
    }
    
    private async Task<string> DeployMethodAsync(IOperationContext context, CancellationToken cancellationToken)
    {
        await context.ProgressReporter?.ReportProgressAsync("deploy", 25, "Generating method code", cancellationToken);
        // Code generation logic
        
        await context.ProgressReporter?.ReportProgressAsync("deploy", 75, "Deploying to device", cancellationToken);
        // Deployment logic using TaskExecutor
        
        await context.ProgressReporter?.ReportProgressAsync("deploy", 100, "Method deployed successfully", cancellationToken);
        return "method_deployed";
    }
    
    private async Task<object> ExecuteMethodAsync(IOperationContext context, CancellationToken cancellationToken)
    {
        await context.ProgressReporter?.ReportProgressAsync("execute", 50, "Executing method on device", cancellationToken);
        // Execution logic using TaskExecutor
        
        await context.ProgressReporter?.ReportProgressAsync("execute", 100, "Method execution complete", cancellationToken);
        return new { result = "success" };
    }
}

public record OperationPreflightResult(bool CanProceed, string Reason, IReadOnlyList<string> ValidationErrors);
```

## Integration Points

### Executor Framework Integration
- Operations use executors through unified context (Issue 002-101)
- Progress reporting for method deployment and execution
- Error handling coordination across executors

### Session Management Integration
- Operation context includes session information (Issue 002-102)
- Session state shared across operation components
- Session cleanup coordination with operations

### Exception Handling Integration
- Unified error handling for all coordinated operations (Issue 002-105)
- Context preservation across operation boundaries
- Cleanup coordination on operation failures

## Implementation Strategy

### Phase 1: Core Integration Infrastructure (Days 1-2)
1. Implement operation coordinator and context interfaces
2. Create base operation classes and progress reporting
3. Add preflight check and cleanup infrastructure
4. Integrate with existing session management

### Phase 2: Composite Operations (Days 3-4)
1. Implement composite operation base classes
2. Create example operations spanning multiple components
3. Add progress reporting and stage management
4. Integrate with executor framework

### Phase 3: Advanced Features (Days 5-6)
1. Add shared state management across operations
2. Implement component notification system
3. Create operation lifecycle management
4. Add performance monitoring and optimization

### Phase 4: Integration and Testing (Day 7)
1. Integration with all existing components
2. Comprehensive unit and integration testing
3. Performance benchmarking
4. Documentation and usage examples

## Definition of Done

### Functional Requirements
- [ ] Operation coordination infrastructure working
- [ ] Progress reporting system operational
- [ ] Composite operations spanning multiple components functional
- [ ] Shared state management working
- [ ] Integration with existing components complete

### Technical Requirements
- [ ] All interfaces properly implemented and tested
- [ ] Operation lifecycle management working
- [ ] Progress reporting accurate and responsive
- [ ] Error handling integrated throughout
- [ ] Performance impact minimized

### Quality Requirements
- [ ] Comprehensive unit test coverage >90%
- [ ] Integration tests with real composite operations
- [ ] Performance benchmarks established
- [ ] Thread safety verified
- [ ] Cross-component integration validated

## Dependencies

### Prerequisite Issues
- Issue 002-101: Executor Framework Implementation (operation context)
- Issue 002-102: Device Session Management System (session integration)
- Issue 002-105: Unified Exception Handling System (error handling)

### Dependent Issues
- All main Epic 002 issues benefit from coordinated operations
- Future Epic 003 file system operations will use this framework

## Risk Assessment

### High Risk Items
- **Operation Complexity**: Complex operation coordination may introduce bugs
  - *Mitigation*: Comprehensive testing, clear operation boundaries
- **Performance Impact**: Coordination overhead may impact performance
  - *Mitigation*: Performance benchmarks, optimization focus

### Medium Risk Items
- **Progress Reporting Accuracy**: Progress reporting may not accurately reflect actual progress
  - *Mitigation*: Careful progress calculation, real-world testing
- **Resource Management**: Complex resource coordination may cause leaks
  - *Mitigation*: Robust cleanup patterns, resource tracking

## Testing Requirements

### Unit Testing
- Operation coordinator functionality
- Progress reporting accuracy
- Composite operation execution
- Shared state management
- Error handling and cleanup

### Integration Testing
- End-to-end composite operations
- Cross-component coordination
- Progress reporting with real operations
- Error handling across components
- Session integration validation

### Performance Testing
- Operation coordination overhead
- Progress reporting performance
- Shared state management performance
- Memory usage profiling

## Acceptance Criteria

1. **Coordination**: Operations can coordinate across multiple components
2. **Progress Reporting**: Accurate progress reporting for complex operations
3. **Error Handling**: Consistent error handling across operation boundaries
4. **State Management**: Shared state management working between components
5. **Performance**: <10ms overhead for operation coordination
6. **Integration**: Seamless integration with all existing components
7. **Usability**: Clear API for creating composite operations

This issue establishes the cross-component integration layer that enables unified operation coordination, progress reporting, and session context management across all components of the Belay.NET architecture.