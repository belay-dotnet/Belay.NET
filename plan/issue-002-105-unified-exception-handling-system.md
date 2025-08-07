# Issue 002-105: Unified Exception Handling System

**Status**: Not Started  
**Priority**: HIGH  
**Estimated Effort**: 4 days  
**Epic**: 002.1 (Architectural Improvements)  
**Dependencies**: None (Foundation requirement)

## Problem Statement

The current architecture has inconsistent exception handling across components, with basic custom exceptions but no unified error mapping or context preservation system. Different layers handle errors differently, making debugging difficult and providing poor error messages to developers. A comprehensive exception handling system is needed to provide consistent error mapping, context preservation, and meaningful error messages across all components.

## Technical Requirements

### Exception Hierarchy

```csharp
// Base Belay exception
public abstract class BelayException : Exception
{
    public string ErrorCode { get; protected set; }
    public Dictionary<string, object> Context { get; } = new();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string ComponentName { get; protected set; }
    
    protected BelayException(string message, string errorCode = null, string componentName = null) 
        : base(message)
    {
        ErrorCode = errorCode ?? GetDefaultErrorCode();
        ComponentName = componentName ?? GetType().Name;
    }
    
    protected BelayException(string message, Exception innerException, string errorCode = null, string componentName = null) 
        : base(message, innerException)
    {
        ErrorCode = errorCode ?? GetDefaultErrorCode();
        ComponentName = componentName ?? GetType().Name;
    }
    
    public BelayException WithContext(string key, object value)
    {
        Context[key] = value;
        return this;
    }
    
    public BelayException WithContext(Dictionary<string, object> context)
    {
        foreach (var kvp in context)
        {
            Context[kvp.Key] = kvp.Value;
        }
        return this;
    }
    
    protected virtual string GetDefaultErrorCode() => "BELAY_UNKNOWN";
}

// Communication exceptions
public class DeviceCommunicationException : BelayException
{
    public string DeviceId { get; }
    public string ConnectionString { get; }
    
    public DeviceCommunicationException(string message, string deviceId = null, string connectionString = null) 
        : base(message, "BELAY_COMM_ERROR", nameof(DeviceCommunicationException))
    {
        DeviceId = deviceId;
        ConnectionString = connectionString;
        
        if (deviceId != null) WithContext("device_id", deviceId);
        if (connectionString != null) WithContext("connection_string", connectionString);
    }
    
    protected override string GetDefaultErrorCode() => "BELAY_COMM_ERROR";
}

public class DeviceConnectionException : DeviceCommunicationException
{
    public DeviceConnectionException(string message, string deviceId = null, string connectionString = null, Exception innerException = null) 
        : base(message, deviceId, connectionString)
    {
        ErrorCode = "BELAY_CONN_ERROR";
        if (innerException != null)
        {
            // Preserve inner exception details in context
            WithContext("inner_exception_type", innerException.GetType().Name);
            WithContext("inner_exception_message", innerException.Message);
        }
    }
}

public class DeviceTimeoutException : DeviceCommunicationException
{
    public TimeSpan Timeout { get; }
    public string Operation { get; }
    
    public DeviceTimeoutException(string operation, TimeSpan timeout, string deviceId = null) 
        : base($"Operation '{operation}' timed out after {timeout.TotalSeconds:F1}s", deviceId)
    {
        ErrorCode = "BELAY_TIMEOUT_ERROR";
        Operation = operation;
        Timeout = timeout;
        
        WithContext("operation", operation);
        WithContext("timeout_seconds", timeout.TotalSeconds);
    }
}

// Execution exceptions
public class DeviceExecutionException : BelayException
{
    public string Code { get; }
    public string DeviceStackTrace { get; }
    public int? LineNumber { get; }
    
    public DeviceExecutionException(string message, string code = null, string deviceStackTrace = null, int? lineNumber = null) 
        : base(message, "BELAY_EXEC_ERROR", nameof(DeviceExecutionException))
    {
        Code = code;
        DeviceStackTrace = deviceStackTrace;
        LineNumber = lineNumber;
        
        if (code != null) WithContext("executed_code", code);
        if (deviceStackTrace != null) WithContext("device_stack_trace", deviceStackTrace);
        if (lineNumber.HasValue) WithContext("line_number", lineNumber.Value);
    }
}

public class DeviceCodeSyntaxException : DeviceExecutionException
{
    public DeviceCodeSyntaxException(string message, string code, int? lineNumber = null) 
        : base(message, code, null, lineNumber)
    {
        ErrorCode = "BELAY_SYNTAX_ERROR";
    }
}

public class DeviceMemoryException : DeviceExecutionException
{
    public long? AvailableMemory { get; }
    public long? RequestedMemory { get; }
    
    public DeviceMemoryException(string message, long? availableMemory = null, long? requestedMemory = null) 
        : base(message)
    {
        ErrorCode = "BELAY_MEMORY_ERROR";
        AvailableMemory = availableMemory;
        RequestedMemory = requestedMemory;
        
        if (availableMemory.HasValue) WithContext("available_memory", availableMemory.Value);
        if (requestedMemory.HasValue) WithContext("requested_memory", requestedMemory.Value);
    }
}

// Session and resource exceptions
public class DeviceSessionException : BelayException
{
    public string SessionId { get; }
    public DeviceSessionState SessionState { get; }
    
    public DeviceSessionException(string message, string sessionId, DeviceSessionState sessionState) 
        : base(message, "BELAY_SESSION_ERROR", nameof(DeviceSessionException))
    {
        SessionId = sessionId;
        SessionState = sessionState;
        
        WithContext("session_id", sessionId);
        WithContext("session_state", sessionState.ToString());
    }
}

public class DeviceResourceException : BelayException
{
    public string ResourceId { get; }
    public string ResourceType { get; }
    
    public DeviceResourceException(string message, string resourceId, string resourceType) 
        : base(message, "BELAY_RESOURCE_ERROR", nameof(DeviceResourceException))
    {
        ResourceId = resourceId;
        ResourceType = resourceType;
        
        WithContext("resource_id", resourceId);
        WithContext("resource_type", resourceType);
    }
}

// Configuration and validation exceptions
public class BelayConfigurationException : BelayException
{
    public string ConfigurationSection { get; }
    
    public BelayConfigurationException(string message, string configurationSection = null) 
        : base(message, "BELAY_CONFIG_ERROR", nameof(BelayConfigurationException))
    {
        ConfigurationSection = configurationSection;
        
        if (configurationSection != null) WithContext("configuration_section", configurationSection);
    }
}

public class BelayValidationException : BelayException
{
    public string ValidationTarget { get; }
    public List<string> ValidationErrors { get; } = new();
    
    public BelayValidationException(string message, string validationTarget, IEnumerable<string> errors = null) 
        : base(message, "BELAY_VALIDATION_ERROR", nameof(BelayValidationException))
    {
        ValidationTarget = validationTarget;
        if (errors != null) ValidationErrors.AddRange(errors);
        
        WithContext("validation_target", validationTarget);
        WithContext("validation_errors", ValidationErrors);
    }
}
```

### Error Mapping and Context Preservation

```csharp
// Error mapping service
public interface IErrorMapper
{
    BelayException MapException(Exception exception, string context = null);
    BelayException MapDeviceError(string deviceOutput, string executedCode = null);
    T EnrichException<T>(T exception, Dictionary<string, object> context) where T : BelayException;
}

internal class ErrorMapper : IErrorMapper
{
    private readonly ILogger<ErrorMapper> _logger;
    private readonly Dictionary<string, Func<string, string, BelayException>> _deviceErrorPatterns;
    
    public ErrorMapper(ILogger<ErrorMapper> logger)
    {
        _logger = logger;
        _deviceErrorPatterns = InitializeDeviceErrorPatterns();
    }
    
    public BelayException MapException(Exception exception, string context = null)
    {
        return exception switch
        {
            BelayException belayEx => EnrichWithContext(belayEx, context),
            TimeoutException timeoutEx => new DeviceTimeoutException("Operation timed out", TimeSpan.Zero, context),
            UnauthorizedAccessException authEx => new DeviceConnectionException($"Access denied: {authEx.Message}", context),
            InvalidOperationException invalidEx => new BelayConfigurationException($"Invalid operation: {invalidEx.Message}"),
            ArgumentException argEx => new BelayValidationException($"Invalid argument: {argEx.Message}", argEx.ParamName),
            _ => new BelayException($"Unexpected error: {exception.Message}", exception, "BELAY_UNEXPECTED_ERROR")
        };
    }
    
    public BelayException MapDeviceError(string deviceOutput, string executedCode = null)
    {
        if (string.IsNullOrEmpty(deviceOutput))
            return new DeviceExecutionException("Device returned empty error response", executedCode);
        
        // Parse common MicroPython error patterns
        foreach (var pattern in _deviceErrorPatterns)
        {
            if (deviceOutput.Contains(pattern.Key))
            {
                var lineNumber = ExtractLineNumber(deviceOutput);
                var exception = pattern.Value(deviceOutput, executedCode);
                
                if (exception is DeviceExecutionException execEx && lineNumber.HasValue)
                {
                    return new DeviceExecutionException(execEx.Message, execEx.Code, deviceOutput, lineNumber);
                }
                
                return exception;
            }
        }
        
        // Default mapping for unknown device errors
        return new DeviceExecutionException($"Device error: {deviceOutput}", executedCode, deviceOutput);
    }
    
    private Dictionary<string, Func<string, string, BelayException>> InitializeDeviceErrorPatterns()
    {
        return new Dictionary<string, Func<string, string, BelayException>>(StringComparer.OrdinalIgnoreCase)
        {
            ["SyntaxError"] = (output, code) => new DeviceCodeSyntaxException($"Syntax error in device code: {ExtractErrorMessage(output)}", code),
            ["MemoryError"] = (output, code) => new DeviceMemoryException($"Device out of memory: {ExtractErrorMessage(output)}"),
            ["OSError"] = (output, code) => new DeviceExecutionException($"Device OS error: {ExtractErrorMessage(output)}", code, output),
            ["ImportError"] = (output, code) => new DeviceExecutionException($"Module import failed: {ExtractErrorMessage(output)}", code, output),
            ["AttributeError"] = (output, code) => new DeviceExecutionException($"Attribute error: {ExtractErrorMessage(output)}", code, output),
            ["NameError"] = (output, code) => new DeviceExecutionException($"Name error: {ExtractErrorMessage(output)}", code, output),
            ["ValueError"] = (output, code) => new DeviceExecutionException($"Value error: {ExtractErrorMessage(output)}", code, output),
            ["TypeError"] = (output, code) => new DeviceExecutionException($"Type error: {ExtractErrorMessage(output)}", code, output)
        };
    }
    
    private BelayException EnrichWithContext(BelayException exception, string context)
    {
        if (!string.IsNullOrEmpty(context))
        {
            exception.WithContext("operation_context", context);
        }
        return exception;
    }
    
    private string ExtractErrorMessage(string deviceOutput)
    {
        // Parse MicroPython error format to extract clean error message
        var lines = deviceOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.LastOrDefault()?.Trim() ?? deviceOutput;
    }
    
    private int? ExtractLineNumber(string deviceOutput)
    {
        // Extract line number from MicroPython traceback
        var match = System.Text.RegularExpressions.Regex.Match(deviceOutput, @"line (\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }
}

// Exception enrichment service
public interface IExceptionEnricher
{
    T Enrich<T>(T exception, string component = null, Dictionary<string, object> additionalContext = null) where T : Exception;
    T EnrichWithDeviceContext<T>(T exception, IDeviceCapabilities capabilities, string sessionId = null) where T : Exception;
}

internal class ExceptionEnricher : IExceptionEnricher
{
    private readonly ILogger<ExceptionEnricher> _logger;
    
    public T Enrich<T>(T exception, string component = null, Dictionary<string, object> additionalContext = null) where T : Exception
    {
        if (exception is BelayException belayEx)
        {
            if (!string.IsNullOrEmpty(component))
                belayEx.WithContext("component", component);
                
            if (additionalContext != null)
                belayEx.WithContext(additionalContext);
        }
        
        _logger.LogError(exception, "Exception enriched in component {Component}", component ?? "Unknown");
        return exception;
    }
    
    public T EnrichWithDeviceContext<T>(T exception, IDeviceCapabilities capabilities, string sessionId = null) where T : Exception
    {
        if (exception is BelayException belayEx)
        {
            belayEx.WithContext("device_type", capabilities.DeviceType)
                   .WithContext("firmware_version", capabilities.FirmwareVersion)
                   .WithContext("supported_features", capabilities.SupportedFeatures.ToString());
                   
            if (!string.IsNullOrEmpty(sessionId))
                belayEx.WithContext("session_id", sessionId);
        }
        
        return exception;
    }
}
```

### Global Exception Handling

```csharp
// Global exception handler for consistent error handling
public interface IGlobalExceptionHandler
{
    Task<TResult> ExecuteWithErrorHandlingAsync<TResult>(Func<Task<TResult>> operation, string context = null);
    Task ExecuteWithErrorHandlingAsync(Func<Task> operation, string context = null);
    void ConfigureExceptionHandling(Action<ExceptionHandlingConfiguration> configure);
}

internal class GlobalExceptionHandler : IGlobalExceptionHandler
{
    private readonly IErrorMapper _errorMapper;
    private readonly IExceptionEnricher _enricher;
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private ExceptionHandlingConfiguration _configuration = new();
    
    public async Task<TResult> ExecuteWithErrorHandlingAsync<TResult>(Func<Task<TResult>> operation, string context = null)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            var mappedException = _errorMapper.MapException(ex, context);
            var enrichedException = _enricher.Enrich(mappedException, context);
            
            _logger.LogError(enrichedException, "Operation failed in context {Context}", context ?? "Unknown");
            
            if (_configuration.RethrowExceptions)
                throw enrichedException;
                
            return default(TResult);
        }
    }
    
    public void ConfigureExceptionHandling(Action<ExceptionHandlingConfiguration> configure)
    {
        configure(_configuration);
    }
}

public class ExceptionHandlingConfiguration
{
    public bool RethrowExceptions { get; set; } = true;
    public bool LogExceptions { get; set; } = true;
    public bool IncludeStackTraces { get; set; } = true;
    public LogLevel ExceptionLogLevel { get; set; } = LogLevel.Error;
}
```

## Integration Points

### Executor Framework Integration
- All executors use unified exception handling (Issue 002-101)
- Context preservation across executor boundaries
- Device error mapping from execution results

### Session Management Integration
- Session context included in exception enrichment (Issue 002-102)
- Session state preservation during error handling
- Resource cleanup exception handling

### Communication Layer Integration
- Communication errors mapped to appropriate exception types
- Device response parsing and error extraction
- Connection state context in exceptions

## Implementation Strategy

### Phase 1: Exception Hierarchy (Days 1-2)
1. Create comprehensive exception hierarchy
2. Implement base BelayException with context support
3. Create specialized exception types for different scenarios
4. Add exception serialization for logging

### Phase 2: Error Mapping (Day 3)
1. Implement error mapper for device and system errors
2. Create device error pattern recognition
3. Add exception enrichment services
4. Integrate with existing communication layer

### Phase 3: Global Handling (Day 4)
1. Implement global exception handler
2. Add configuration for exception handling behavior
3. Create exception handling middleware for DI integration
4. Add comprehensive logging and monitoring

## Definition of Done

### Functional Requirements
- [ ] Comprehensive exception hierarchy implemented
- [ ] Device error mapping working for common error patterns
- [ ] Exception enrichment with context preservation
- [ ] Global exception handling infrastructure operational
- [ ] Integration with existing components complete

### Technical Requirements
- [ ] All exception types properly implemented and tested
- [ ] Error mapping patterns validated with real device errors
- [ ] Context preservation working across all layers
- [ ] Exception serialization and logging working
- [ ] Performance impact minimized

### Quality Requirements
- [ ] Comprehensive unit test coverage >90%
- [ ] Integration tests with real device error scenarios
- [ ] Exception handling performance benchmarks
- [ ] Clear error message validation
- [ ] Documentation with error handling examples

## Dependencies

### Prerequisite Issues
- None (This is a foundational architectural component)

### Dependent Issues
- Issue 002-101: Executor Framework Implementation (error handling integration)
- Issue 002-102: Device Session Management System (session context in errors)
- All Epic 002 issues benefit from unified exception handling

## Risk Assessment

### High Risk Items
- **Error Context Loss**: Risk of losing important error context during mapping
  - *Mitigation*: Comprehensive context preservation, extensive testing
- **Performance Impact**: Exception handling overhead on critical paths
  - *Mitigation*: Performance benchmarks, optimized error mapping

### Medium Risk Items
- **Device Error Pattern Coverage**: May not cover all device error patterns
  - *Mitigation*: Extensible error mapping, continuous pattern updates
- **Exception Serialization**: Complex exception context may not serialize properly
  - *Mitigation*: Custom serialization, serialization testing

## Testing Requirements

### Unit Testing
- Exception hierarchy and context preservation
- Error mapping pattern recognition
- Exception enrichment logic
- Global exception handler behavior

### Integration Testing
- End-to-end error handling scenarios
- Device error mapping with real devices
- Context preservation across components
- Exception logging and serialization

### Performance Testing
- Exception handling overhead measurement
- Error mapping performance benchmarks
- Memory usage for exception context

## Acceptance Criteria

1. **Exception Hierarchy**: Comprehensive exception types for all error scenarios
2. **Error Mapping**: Accurate mapping of device errors to appropriate exceptions
3. **Context Preservation**: Error context preserved across all system layers
4. **Global Handling**: Consistent error handling throughout the application
5. **Performance**: <5ms overhead for exception handling operations
6. **Integration**: Seamless integration with all existing components
7. **Developer Experience**: Clear, actionable error messages with proper context

This issue establishes the unified exception handling system that provides consistent error management and debugging support across all components of the Belay.NET architecture.