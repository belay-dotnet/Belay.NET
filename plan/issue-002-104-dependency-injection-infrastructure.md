# Issue 002-104: Dependency Injection Infrastructure

**Status**: Not Started  
**Priority**: CRITICAL  
**Estimated Effort**: 1 week  
**Epic**: 002.1 (Architectural Improvements)  
**Dependencies**: None (Foundation requirement)

## Problem Statement

The current architecture lacks proper dependency injection infrastructure, making the codebase difficult to test, configure, and extend. Components are tightly coupled with hardcoded dependencies, preventing proper unit testing with mocks and making configuration management complex. A comprehensive dependency injection system is needed to support the enterprise-ready architecture goals.

## Technical Requirements

### Core Infrastructure

```csharp
// Main service registration extensions
public static class BelayServiceCollectionExtensions
{
    public static IServiceCollection AddBelayCore(this IServiceCollection services, Action<BelayConfiguration> configure = null)
    {
        var configuration = new BelayConfiguration();
        configure?.Invoke(configuration);
        
        services.AddSingleton(configuration);
        
        // Core services
        services.AddSingleton<IDeviceFactory, DeviceFactory>();
        services.AddSingleton<ICommunicatorFactory, CommunicatorFactory>();
        services.AddScoped<IDeviceSessionManager, DeviceSessionManager>();
        services.AddScoped<IMethodDeploymentCache, MethodDeploymentCache>();
        
        // Executor services
        services.AddScoped<ITaskExecutor, TaskExecutor>();
        services.AddScoped<ISetupExecutor, SetupExecutor>();
        services.AddScoped<IThreadExecutor, ThreadExecutor>();
        services.AddScoped<ITeardownExecutor, TeardownExecutor>();
        
        // Storage services
        services.AddSingleton<IPersistentCacheStorage, FileSystemCacheStorage>();
        
        // Configuration services
        services.Configure<SerialCommunicationConfiguration>(configuration.Serial);
        services.Configure<SubprocessCommunicationConfiguration>(configuration.Subprocess);
        services.Configure<MethodCacheConfiguration>(configuration.Cache);
        
        return services;
    }
    
    public static IServiceCollection AddBelayLogging(this IServiceCollection services, Action<BelayLoggingConfiguration> configure = null)
    {
        var config = new BelayLoggingConfiguration();
        configure?.Invoke(config);
        
        services.AddLogging(builder =>
        {
            if (config.EnableConsoleLogging)
                builder.AddConsole();
                
            if (config.EnableFileLogging)
                builder.AddFile(config.LogFilePath);
                
            builder.SetMinimumLevel(config.MinimumLogLevel);
            builder.AddFilter("Belay", config.BelayLogLevel);
        });
        
        return services;
    }
    
    public static IServiceCollection AddBelayHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DeviceHealthCheck>("device_connectivity")
            .AddCheck<CacheHealthCheck>("method_cache")
            .AddCheck<SessionHealthCheck>("session_management");
            
        return services;
    }
}

// Configuration classes
public class BelayConfiguration
{
    public SerialCommunicationConfiguration Serial { get; set; } = new();
    public SubprocessCommunicationConfiguration Subprocess { get; set; } = new();
    public MethodCacheConfiguration Cache { get; set; } = new();
    public DeviceConfiguration Device { get; set; } = new();
    public string DefaultConnectionString { get; set; }
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public class BelayLoggingConfiguration
{
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnableFileLogging { get; set; } = false;
    public string LogFilePath { get; set; } = "belay.log";
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    public LogLevel BelayLogLevel { get; set; } = LogLevel.Debug;
}

public class DeviceConfiguration
{
    public int MaxConcurrentDevices { get; set; } = 10;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnableAutoReconnect { get; set; } = true;
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
}
```

### Factory Pattern Implementation

```csharp
// Device factory interface and implementation
public interface IDeviceFactory
{
    Task<IDevice> CreateDeviceAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<T> CreateDeviceAsync<T>(string connectionString, CancellationToken cancellationToken = default) where T : class, IDevice;
    IReadOnlyList<string> GetSupportedConnectionTypes();
}

internal class DeviceFactory : IDeviceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommunicatorFactory _communicatorFactory;
    private readonly ILogger<DeviceFactory> _logger;
    private readonly BelayConfiguration _configuration;
    
    public async Task<IDevice> CreateDeviceAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var connectionInfo = ParseConnectionString(connectionString);
        var communicator = await _communicatorFactory.CreateCommunicatorAsync(connectionInfo, cancellationToken);
        
        // Create device with proper dependency injection
        var device = ActivatorUtilities.CreateInstance<Device>(_serviceProvider, communicator);
        
        await device.ConnectAsync(cancellationToken);
        return device;
    }
    
    public async Task<T> CreateDeviceAsync<T>(string connectionString, CancellationToken cancellationToken = default) 
        where T : class, IDevice
    {
        var connectionInfo = ParseConnectionString(connectionString);
        var communicator = await _communicatorFactory.CreateCommunicatorAsync(connectionInfo, cancellationToken);
        
        // Create custom device type with DI
        var device = ActivatorUtilities.CreateInstance<T>(_serviceProvider, communicator);
        
        if (device is Device baseDevice)
        {
            await baseDevice.ConnectAsync(cancellationToken);
        }
        
        return device;
    }
}

// Communicator factory
public interface ICommunicatorFactory
{
    Task<IDeviceCommunicator> CreateCommunicatorAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken = default);
    IReadOnlyList<string> GetSupportedCommunicators();
}

internal class CommunicatorFactory : ICommunicatorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsSnapshot<SerialCommunicationConfiguration> _serialConfig;
    private readonly IOptionsSnapshot<SubprocessCommunicationConfiguration> _subprocessConfig;
    private readonly ILogger<CommunicatorFactory> _logger;
    
    public async Task<IDeviceCommunicator> CreateCommunicatorAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken = default)
    {
        return connectionInfo.Type.ToLowerInvariant() switch
        {
            "serial" => CreateSerialCommunicator(connectionInfo),
            "subprocess" => CreateSubprocessCommunicator(connectionInfo),
            "webrepl" => CreateWebReplCommunicator(connectionInfo),
            _ => throw new NotSupportedException($"Connection type '{connectionInfo.Type}' is not supported")
        };
    }
    
    private IDeviceCommunicator CreateSerialCommunicator(ConnectionInfo connectionInfo)
    {
        return ActivatorUtilities.CreateInstance<SerialDeviceCommunicator>(
            _serviceProvider, 
            connectionInfo.PortName, 
            _serialConfig.Value);
    }
    
    private IDeviceCommunicator CreateSubprocessCommunicator(ConnectionInfo connectionInfo)
    {
        return ActivatorUtilities.CreateInstance<SubprocessDeviceCommunicator>(
            _serviceProvider,
            connectionInfo.ExecutablePath,
            _subprocessConfig.Value);
    }
}
```

### Enhanced Device Class with DI

```csharp
// Updated Device base class with dependency injection
public abstract partial class Device : IDevice, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDeviceSessionManager _sessionManager;
    private readonly IMethodDeploymentCache _cache;
    private readonly ILogger<Device> _logger;
    private readonly BelayConfiguration _configuration;
    
    // Executors injected via DI
    protected ITaskExecutor TaskExecutor { get; private set; }
    protected ISetupExecutor SetupExecutor { get; private set; }
    protected IThreadExecutor ThreadExecutor { get; private set; }
    protected ITeardownExecutor TeardownExecutor { get; private set; }
    
    protected Device(
        IDeviceCommunicator communicator,
        IServiceProvider serviceProvider,
        IDeviceSessionManager sessionManager,
        IMethodDeploymentCache cache,
        ILogger<Device> logger,
        BelayConfiguration configuration)
    {
        _communicator = communicator ?? throw new ArgumentNullException(nameof(communicator));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        InitializeExecutors();
    }
    
    private void InitializeExecutors()
    {
        TaskExecutor = _serviceProvider.GetRequiredService<ITaskExecutor>();
        SetupExecutor = _serviceProvider.GetRequiredService<ISetupExecutor>();
        ThreadExecutor = _serviceProvider.GetRequiredService<IThreadExecutor>();
        TeardownExecutor = _serviceProvider.GetRequiredService<ITeardownExecutor>();
    }
}
```

### Configuration Management

```csharp
// Configuration builders and validation
public class BelayConfigurationBuilder
{
    private readonly BelayConfiguration _configuration = new();
    
    public BelayConfigurationBuilder UseSerialDefaults()
    {
        _configuration.Serial.BaudRate = 115200;
        _configuration.Serial.ReadTimeout = TimeSpan.FromSeconds(5);
        _configuration.Serial.WriteTimeout = TimeSpan.FromSeconds(5);
        return this;
    }
    
    public BelayConfigurationBuilder UseSubprocessDefaults()
    {
        _configuration.Subprocess.ExecutablePath = "./micropython/ports/unix/build-standard/micropython";
        _configuration.Subprocess.StartupTimeout = TimeSpan.FromSeconds(10);
        return this;
    }
    
    public BelayConfigurationBuilder ConfigureCache(Action<MethodCacheConfiguration> configure)
    {
        configure(_configuration.Cache);
        return this;
    }
    
    public BelayConfiguration Build()
    {
        ValidateConfiguration(_configuration);
        return _configuration;
    }
    
    private static void ValidateConfiguration(BelayConfiguration configuration)
    {
        if (configuration.DefaultTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Default timeout must be greater than zero");
            
        if (configuration.Device.MaxConcurrentDevices <= 0)
            throw new InvalidOperationException("Max concurrent devices must be greater than zero");
    }
}

// Health check implementations
public class DeviceHealthCheck : IHealthCheck
{
    private readonly IDeviceFactory _deviceFactory;
    private readonly BelayConfiguration _configuration;
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_configuration.DefaultConnectionString))
        {
            return HealthCheckResult.Degraded("No default connection string configured");
        }
        
        try
        {
            var supportedTypes = _deviceFactory.GetSupportedConnectionTypes();
            var data = new Dictionary<string, object>
            {
                ["supported_connection_types"] = supportedTypes,
                ["default_connection"] = _configuration.DefaultConnectionString
            };
            
            return HealthCheckResult.Healthy("Device factory is operational", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Device factory error", ex);
        }
    }
}
```

## Integration Points

### Executor Framework Integration
- All executors registered and configured through DI (Issue 002-101)
- Service lifetime management for executor instances
- Configuration injection for executor behavior

### Session Management Integration
- Session managers registered with appropriate lifetime (Issue 002-102)
- Configuration for session management behavior
- Health checks for session state

### Caching Infrastructure Integration
- Cache services registered and configured through DI (Issue 002-103)
- Configurable cache storage implementations
- Health monitoring for cache operations

## Implementation Strategy

### Phase 1: Core DI Infrastructure (Days 1-2)
1. Create service registration extensions
2. Implement configuration classes and builders
3. Add basic factory pattern implementations
4. Integrate with existing Device class

### Phase 2: Advanced Configuration (Days 3-4)
1. Implement comprehensive configuration management
2. Add configuration validation and builders
3. Create pluggable storage and communicator factories
4. Add logging and monitoring configuration

### Phase 3: Health Checks and Monitoring (Days 5-6)
1. Implement health check providers
2. Add service monitoring and diagnostics
3. Create configuration testing utilities
4. Add performance monitoring integration

### Phase 4: Integration and Testing (Day 7)
1. Integrate with all existing components
2. Add comprehensive unit and integration tests
3. Create usage examples and documentation
4. Verify cross-platform compatibility

## Definition of Done

### Functional Requirements
- [ ] Complete service registration infrastructure working
- [ ] Configuration management system operational
- [ ] Factory patterns implemented for all major components
- [ ] Health checks and monitoring functional
- [ ] Integration with existing components complete

### Technical Requirements
- [ ] All services properly registered and configured
- [ ] Configuration validation and error handling working
- [ ] Service lifetime management verified
- [ ] Factory pattern implementations tested
- [ ] Health check providers operational

### Quality Requirements
- [ ] Comprehensive unit test coverage >90%
- [ ] Integration tests with DI container
- [ ] Configuration testing and validation
- [ ] Performance impact assessment completed
- [ ] Documentation and usage examples

## Dependencies

### Prerequisite Issues
- None (This is a foundational architectural component)

### Dependent Issues
- Issue 002-101: Executor Framework Implementation (service registration)
- Issue 002-102: Device Session Management System (service registration)
- Issue 002-103: Method Deployment Caching Infrastructure (service registration)
- All Epic 002 issues benefit from DI infrastructure

## Risk Assessment

### High Risk Items
- **Service Lifetime Complexity**: Complex service lifetime management across device connections
  - *Mitigation*: Clear service scope boundaries, comprehensive testing
- **Configuration Complexity**: Complex configuration with many interdependent settings
  - *Mitigation*: Configuration validation, builder patterns, clear defaults

### Medium Risk Items
- **Performance Impact**: DI overhead may impact device communication performance
  - *Mitigation*: Performance benchmarks, optimization focus
- **Circular Dependencies**: Risk of circular dependencies in complex service graph
  - *Mitigation*: Careful service design, dependency analysis tools

## Testing Requirements

### Unit Testing
- Service registration and configuration
- Factory pattern implementations
- Configuration validation logic
- Health check implementations
- Service lifetime behavior

### Integration Testing
- End-to-end service resolution
- Configuration loading and validation
- Factory creation workflows
- Health check execution
- Service scope behavior

### Performance Testing
- DI container resolution performance
- Service creation overhead
- Configuration loading speed
- Memory usage profiling

## Acceptance Criteria

1. **Service Registration**: All major components registered and resolvable through DI
2. **Configuration**: Comprehensive configuration system with validation and defaults
3. **Factories**: Factory patterns working for devices and communicators
4. **Health Checks**: Health monitoring operational for all major components
5. **Integration**: Seamless integration with existing codebase
6. **Performance**: <1ms overhead for service resolution
7. **Testing**: Comprehensive test coverage with mock injection support

This issue establishes the dependency injection foundation that makes the codebase testable, configurable, and extensible, supporting the enterprise-ready architecture goals.