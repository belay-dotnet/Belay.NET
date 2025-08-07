# Issue 002-108: Factory Pattern Implementation

**Status**: Not Started  
**Priority**: MEDIUM  
**Estimated Effort**: 4 days  
**Epic**: 002.1 (Architectural Improvements)  
**Dependencies**: Issue 002-104 (Dependency Injection Infrastructure)

## Problem Statement

The current architecture lacks comprehensive factory patterns for device creation, communication initialization, and component instantiation. Device and communicator creation is handled inconsistently without proper connection string parsing or extensible factory patterns. A comprehensive factory pattern implementation is needed to provide connection string parsing, extensible factory patterns for future connection types, and configuration-driven factory selection.

## Technical Requirements

### Connection String Parsing

```csharp
// Connection string parser and models
public interface IConnectionStringParser
{
    ConnectionInfo Parse(string connectionString);
    bool IsValid(string connectionString);
    IReadOnlyList<string> GetSupportedSchemes();
}

public record ConnectionInfo
{
    public string Type { get; init; }
    public string Host { get; init; }
    public int? Port { get; init; }
    public string Path { get; init; }
    public Dictionary<string, string> Parameters { get; init; } = new();
    public string OriginalString { get; init; }
    
    public static ConnectionInfo Parse(string connectionString)
    {
        return new ConnectionStringParser().Parse(connectionString);
    }
}

internal class ConnectionStringParser : IConnectionStringParser
{
    private readonly Dictionary<string, Func<string, ConnectionInfo>> _parsers;
    
    public ConnectionStringParser()
    {
        _parsers = new Dictionary<string, Func<string, ConnectionInfo>>(StringComparer.OrdinalIgnoreCase)
        {
            ["serial"] = ParseSerial,
            ["subprocess"] = ParseSubprocess,
            ["webrepl"] = ParseWebRepl,
            ["tcp"] = ParseTcp,
            ["usb"] = ParseUsb
        };
    }
    
    public ConnectionInfo Parse(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new BelayConfigurationException("Connection string cannot be null or empty");
        
        // Handle simple port names like "COM3" or "/dev/ttyUSB0"
        if (!connectionString.Contains(':'))
        {
            return ParseSimpleSerialPort(connectionString);
        }
        
        var uri = new Uri(connectionString);
        var scheme = uri.Scheme.ToLowerInvariant();
        
        if (!_parsers.ContainsKey(scheme))
        {
            throw new BelayConfigurationException($"Unsupported connection type: {scheme}");
        }
        
        return _parsers[scheme](connectionString);
    }
    
    private ConnectionInfo ParseSerial(string connectionString)
    {
        // Examples: 
        // serial:COM3
        // serial:COM3?baudrate=115200&timeout=5000
        // serial:/dev/ttyUSB0?baudrate=9600
        
        var uri = new Uri(connectionString);
        var parameters = ParseQueryParameters(uri.Query);
        
        return new ConnectionInfo
        {
            Type = "serial",
            Path = uri.Host + uri.AbsolutePath,
            Parameters = parameters,
            OriginalString = connectionString
        };
    }
    
    private ConnectionInfo ParseSubprocess(string connectionString)
    {
        // Examples:
        // subprocess:./micropython
        // subprocess:python?args=-i&workdir=/tmp
        // subprocess:c:\python\python.exe?args=-u
        
        var uri = new Uri(connectionString);
        var parameters = ParseQueryParameters(uri.Query);
        
        return new ConnectionInfo
        {
            Type = "subprocess",
            Path = uri.Host + uri.AbsolutePath,
            Parameters = parameters,
            OriginalString = connectionString
        };
    }
    
    private ConnectionInfo ParseWebRepl(string connectionString)
    {
        // Examples:
        // webrepl:192.168.1.100
        // webrepl:192.168.1.100:8266?password=mypass&timeout=10000
        
        var uri = new Uri(connectionString);
        var parameters = ParseQueryParameters(uri.Query);
        
        return new ConnectionInfo
        {
            Type = "webrepl",
            Host = uri.Host,
            Port = uri.Port == -1 ? 8266 : uri.Port,
            Parameters = parameters,
            OriginalString = connectionString
        };
    }
    
    private Dictionary<string, string> ParseQueryParameters(string query)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        if (string.IsNullOrEmpty(query)) return parameters;
        
        query = query.TrimStart('?');
        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(keyValue[0]);
            var value = keyValue.Length > 1 ? Uri.UnescapeDataString(keyValue[1]) : "";
            parameters[key] = value;
        }
        
        return parameters;
    }
}
```

### Enhanced Factory Interfaces

```csharp
// Enhanced device factory with extensibility
public interface IDeviceFactory
{
    Task<IDevice> CreateDeviceAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<T> CreateDeviceAsync<T>(string connectionString, CancellationToken cancellationToken = default) where T : class, IDevice;
    
    void RegisterDeviceType<T>(string typeName) where T : class, IDevice;
    void RegisterCommunicatorFactory(string connectionType, Func<ConnectionInfo, IDeviceCommunicator> factory);
    
    IReadOnlyList<string> GetSupportedConnectionTypes();
    IReadOnlyList<string> GetRegisteredDeviceTypes();
    
    Task<DeviceDiscoveryResult> DiscoverDevicesAsync(string connectionType = null, CancellationToken cancellationToken = default);
}

// Enhanced communicator factory
public interface ICommunicatorFactory
{
    Task<IDeviceCommunicator> CreateCommunicatorAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken = default);
    void RegisterCommunicator<T>(string connectionType, Func<ConnectionInfo, IServiceProvider, T> factory) where T : class, IDeviceCommunicator;
    
    IReadOnlyList<string> GetSupportedCommunicators();
    Task<bool> TestConnectionAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken = default);
}

// Device discovery interfaces
public interface IDeviceDiscovery
{
    Task<IReadOnlyList<DiscoveredDevice>> DiscoverDevicesAsync(CancellationToken cancellationToken = default);
    Task<bool> IsDeviceAvailableAsync(string devicePath, CancellationToken cancellationToken = default);
}

public record DiscoveredDevice(
    string DisplayName,
    string ConnectionString,
    string DeviceType,
    Dictionary<string, object> Properties,
    bool IsAvailable
);

public record DeviceDiscoveryResult(
    IReadOnlyList<DiscoveredDevice> Devices,
    TimeSpan DiscoveryTime,
    DateTime DiscoveredAt
);
```

### Implementation Classes

```csharp
// Enhanced device factory implementation
internal class DeviceFactory : IDeviceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICommunicatorFactory _communicatorFactory;
    private readonly IConnectionStringParser _connectionParser;
    private readonly ILogger<DeviceFactory> _logger;
    private readonly Dictionary<string, Type> _registeredDeviceTypes = new();
    private readonly Dictionary<string, IDeviceDiscovery> _discoveryProviders = new();
    
    public async Task<IDevice> CreateDeviceAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        return await CreateDeviceAsync<Device>(connectionString, cancellationToken);
    }
    
    public async Task<T> CreateDeviceAsync<T>(string connectionString, CancellationToken cancellationToken = default) where T : class, IDevice
    {
        var connectionInfo = _connectionParser.Parse(connectionString);
        
        _logger.LogInformation("Creating device of type {DeviceType} for connection {ConnectionType}:{ConnectionPath}", 
            typeof(T).Name, connectionInfo.Type, connectionInfo.Path);
        
        // Create communicator
        var communicator = await _communicatorFactory.CreateCommunicatorAsync(connectionInfo, cancellationToken);
        
        // Create device instance using DI
        var device = ActivatorUtilities.CreateInstance<T>(_serviceProvider, communicator);
        
        // Initialize device connection
        if (device is Device baseDevice)
        {
            await baseDevice.ConnectAsync(cancellationToken);
            
            // Apply connection-specific configuration
            await ConfigureDeviceFromConnectionAsync(baseDevice, connectionInfo, cancellationToken);
        }
        
        _logger.LogInformation("Successfully created and connected device {DeviceType}", typeof(T).Name);
        return device;
    }
    
    public void RegisterDeviceType<T>(string typeName) where T : class, IDevice
    {
        _registeredDeviceTypes[typeName] = typeof(T);
        _logger.LogDebug("Registered device type {TypeName} -> {DeviceType}", typeName, typeof(T).Name);
    }
    
    public async Task<DeviceDiscoveryResult> DiscoverDevicesAsync(string connectionType = null, CancellationToken cancellationToken = default)
    {
        var discoveryStart = DateTime.UtcNow;
        var allDevices = new List<DiscoveredDevice>();
        
        var providersToQuery = string.IsNullOrEmpty(connectionType) 
            ? _discoveryProviders.Values 
            : _discoveryProviders.Where(kvp => kvp.Key.Equals(connectionType, StringComparison.OrdinalIgnoreCase)).Select(kvp => kvp.Value);
        
        foreach (var provider in providersToQuery)
        {
            try
            {
                var devices = await provider.DiscoverDevicesAsync(cancellationToken);
                allDevices.AddRange(devices);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Device discovery failed for provider {ProviderType}", provider.GetType().Name);
            }
        }
        
        var discoveryTime = DateTime.UtcNow - discoveryStart;
        
        return new DeviceDiscoveryResult(allDevices, discoveryTime, discoveryStart);
    }
    
    private async Task ConfigureDeviceFromConnectionAsync(Device device, ConnectionInfo connectionInfo, CancellationToken cancellationToken)
    {
        // Apply connection-specific configuration from parameters
        foreach (var parameter in connectionInfo.Parameters)
        {
            switch (parameter.Key.ToLowerInvariant())
            {
                case "timeout":
                    if (TimeSpan.TryParse(parameter.Value, out var timeout))
                    {
                        // Apply timeout configuration
                    }
                    break;
                case "retries":
                    if (int.TryParse(parameter.Value, out var retries))
                    {
                        // Apply retry configuration
                    }
                    break;
            }
        }
    }
}

// Enhanced communicator factory
internal class CommunicatorFactory : ICommunicatorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Func<ConnectionInfo, IServiceProvider, IDeviceCommunicator>> _communicatorFactories = new();
    private readonly ILogger<CommunicatorFactory> _logger;
    
    public CommunicatorFactory(IServiceProvider serviceProvider, ILogger<CommunicatorFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        RegisterDefaultCommunicators();
    }
    
    private void RegisterDefaultCommunicators()
    {
        RegisterCommunicator<SerialDeviceCommunicator>("serial", (info, sp) =>
        {
            var config = sp.GetRequiredService<IOptionsSnapshot<SerialCommunicationConfiguration>>();
            return new SerialDeviceCommunicator(info.Path, config.Value, sp.GetRequiredService<ILogger<SerialDeviceCommunicator>>());
        });
        
        RegisterCommunicator<SubprocessDeviceCommunicator>("subprocess", (info, sp) =>
        {
            var config = sp.GetRequiredService<IOptionsSnapshot<SubprocessCommunicationConfiguration>>();
            return new SubprocessDeviceCommunicator(info.Path, config.Value, sp.GetRequiredService<ILogger<SubprocessDeviceCommunicator>>());
        });
    }
    
    public async Task<IDeviceCommunicator> CreateCommunicatorAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken = default)
    {
        var connectionType = connectionInfo.Type.ToLowerInvariant();
        
        if (!_communicatorFactories.ContainsKey(connectionType))
        {
            throw new BelayConfigurationException($"No communicator factory registered for connection type: {connectionType}");
        }
        
        try
        {
            var factory = _communicatorFactories[connectionType];
            var communicator = factory(connectionInfo, _serviceProvider);
            
            // Test connection if supported
            if (await TestConnectionAsync(connectionInfo, cancellationToken))
            {
                _logger.LogInformation("Successfully created communicator for {ConnectionType}:{ConnectionPath}", 
                    connectionType, connectionInfo.Path);
            }
            else
            {
                _logger.LogWarning("Connection test failed for {ConnectionType}:{ConnectionPath}", 
                    connectionType, connectionInfo.Path);
            }
            
            return communicator;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create communicator for {ConnectionType}:{ConnectionPath}", 
                connectionType, connectionInfo.Path);
            throw new DeviceConnectionException($"Failed to create communicator: {ex.Message}", 
                connectionInfo.OriginalString, connectionInfo.Path, ex);
        }
    }
    
    public void RegisterCommunicator<T>(string connectionType, Func<ConnectionInfo, IServiceProvider, T> factory) where T : class, IDeviceCommunicator
    {
        _communicatorFactories[connectionType.ToLowerInvariant()] = factory;
        _logger.LogDebug("Registered communicator factory for connection type {ConnectionType} -> {CommunicatorType}", 
            connectionType, typeof(T).Name);
    }
    
    public async Task<bool> TestConnectionAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken = default)
    {
        // Basic connection testing - can be extended per connection type
        try
        {
            switch (connectionInfo.Type.ToLowerInvariant())
            {
                case "serial":
                    return await TestSerialConnectionAsync(connectionInfo, cancellationToken);
                case "subprocess":
                    return await TestSubprocessConnectionAsync(connectionInfo, cancellationToken);
                default:
                    return true; // Assume valid if we can't test
            }
        }
        catch
        {
            return false;
        }
    }
}

// Serial device discovery implementation
internal class SerialDeviceDiscovery : IDeviceDiscovery
{
    private readonly ILogger<SerialDeviceDiscovery> _logger;
    
    public async Task<IReadOnlyList<DiscoveredDevice>> DiscoverDevicesAsync(CancellationToken cancellationToken = default)
    {
        var devices = new List<DiscoveredDevice>();
        
        try
        {
            var portNames = SerialPort.GetPortNames();
            
            foreach (var portName in portNames)
            {
                try
                {
                    var properties = await GetSerialPortPropertiesAsync(portName, cancellationToken);
                    var isAvailable = await TestSerialPortAvailabilityAsync(portName, cancellationToken);
                    
                    devices.Add(new DiscoveredDevice(
                        DisplayName: $"Serial Port {portName}",
                        ConnectionString: $"serial:{portName}",
                        DeviceType: "serial",
                        Properties: properties,
                        IsAvailable: isAvailable
                    ));
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to query serial port {PortName}", portName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover serial devices");
        }
        
        return devices;
    }
    
    private async Task<Dictionary<string, object>> GetSerialPortPropertiesAsync(string portName, CancellationToken cancellationToken)
    {
        // Get additional properties about the serial port if possible
        return new Dictionary<string, object>
        {
            ["port_name"] = portName,
            ["port_type"] = "serial"
        };
    }
    
    private async Task<bool> TestSerialPortAvailabilityAsync(string portName, CancellationToken cancellationToken)
    {
        try
        {
            using var port = new SerialPort(portName);
            port.Open();
            return port.IsOpen;
        }
        catch
        {
            return false;
        }
    }
}
```

## Integration Points

### Dependency Injection Integration
- Factories registered and configured through DI (Issue 002-104)
- Service provider integration for component creation
- Configuration injection for factory behavior

### Session Management Integration
- Device creation includes session management setup (Issue 002-102)
- Connection configuration integrated with session context
- Factory-created devices participate in session lifecycle

### Exception Handling Integration
- Factory operations use unified exception handling (Issue 002-105)
- Connection errors mapped to appropriate exception types
- Factory context preserved in error messages

## Implementation Strategy

### Phase 1: Connection String Parsing (Day 1)
1. Implement connection string parser with URI support
2. Add support for all major connection types
3. Create extensible parsing system
4. Add validation and error handling

### Phase 2: Enhanced Factory Implementation (Days 2-3)
1. Implement enhanced device and communicator factories
2. Add registration system for extensible factories
3. Create device discovery infrastructure
4. Add connection testing capabilities

### Phase 3: Device Discovery (Day 4)
1. Implement device discovery for serial and subprocess
2. Add device availability testing
3. Create discovery result aggregation
4. Add discovery caching and optimization

## Definition of Done

### Functional Requirements
- [ ] Connection string parsing working for all connection types
- [ ] Device and communicator factories operational
- [ ] Factory registration system working
- [ ] Device discovery implemented for major connection types
- [ ] Connection testing and validation working

### Technical Requirements
- [ ] All factory interfaces properly implemented
- [ ] Connection string parsing comprehensive and extensible
- [ ] Device discovery working across platforms
- [ ] Factory registration and extensibility verified
- [ ] Connection testing reliable

### Quality Requirements
- [ ] Comprehensive unit test coverage >90%
- [ ] Integration tests with real device creation
- [ ] Connection string parsing validation tests
- [ ] Device discovery testing across platforms
- [ ] Performance benchmarks for factory operations

## Dependencies

### Prerequisite Issues
- Issue 002-104: Dependency Injection Infrastructure (factory registration)

### Dependent Issues
- All Epic 002 issues benefit from improved factory patterns
- Future connection type extensions

## Risk Assessment

### High Risk Items
- **Cross-Platform Discovery**: Device discovery may behave differently across platforms
  - *Mitigation*: Platform-specific implementations, comprehensive testing
- **Connection String Compatibility**: Connection string format may not cover all scenarios
  - *Mitigation*: Extensible parsing, backward compatibility

### Medium Risk Items
- **Factory Complexity**: Complex factory registration may introduce bugs
  - *Mitigation*: Clear factory interfaces, comprehensive testing
- **Discovery Performance**: Device discovery may be slow
  - *Mitigation*: Asynchronous discovery, caching mechanisms

## Testing Requirements

### Unit Testing
- Connection string parsing for all formats
- Factory registration and creation logic
- Device discovery functionality
- Connection testing and validation
- Error handling and edge cases

### Integration Testing
- End-to-end device creation workflows
- Cross-platform device discovery
- Factory extensibility testing
- Real device connection testing

### Performance Testing
- Factory creation performance
- Device discovery speed
- Connection string parsing performance

## Acceptance Criteria

1. **Connection Parsing**: Comprehensive connection string parsing for all connection types
2. **Factory Patterns**: Working factory patterns with extensibility support
3. **Device Discovery**: Cross-platform device discovery operational
4. **Connection Testing**: Reliable connection testing and validation
5. **Integration**: Seamless integration with DI and existing components
6. **Extensibility**: Easy registration of new device types and connection methods
7. **Performance**: <100ms for device creation and connection testing

This issue provides comprehensive factory pattern implementation with connection string parsing, device discovery, and extensible factory registration to support the growing needs of the Belay.NET architecture.