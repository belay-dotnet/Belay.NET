# Belay.NET - C# MicroPython Device Library Architecture

## Project Overview

**Belay.NET** is a C# library that provides seamless integration between .NET applications and MicroPython/CircuitPython devices. It enables Windows applications to treat MicroPython devices as off-the-shelf hardware components, providing physical capabilities through a clean, strongly-typed API.

## Solution Structure

```
Belay.NET/
├── src/
│   ├── Belay.Core/                    # Core device communication and management
│   ├── Belay.Attributes/              # Attributes for method decoration
│   ├── Belay.Proxy/                   # Dynamic proxy object system
│   ├── Belay.Sync/                    # File synchronization system
│   ├── Belay.PackageManager/          # NuGet-style package management
│   ├── Belay.CLI/                     # Command-line interface
│   └── Belay.Extensions/              # Extensions for DI and configuration
├── samples/
│   ├── BasicLedControl/
│   ├── SensorReading/
│   ├── AdvancedDeviceSubclassing/
│   └── ProxyObjectDemo/
├── tests/
└── docs/
```

## Core Architecture Components

### 1. Device Communication Layer (`Belay.Core`)

#### `IDeviceCommunication` Interface
```csharp
public interface IDeviceCommunication : IDisposable
{
    Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default);
    Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default);
    Task PutFileAsync(string localPath, string remotePath, CancellationToken cancellationToken = default);
    Task<byte[]> GetFileAsync(string remotePath, CancellationToken cancellationToken = default);
    event EventHandler<DeviceOutputEventArgs> OutputReceived;
    event EventHandler<DeviceStateChangeEventArgs> StateChanged;
}
```

#### `SerialDeviceCommunication` Class
```csharp
public class SerialDeviceCommunication : IDeviceCommunication
{
    private readonly SerialPort _serialPort;
    private readonly SemaphoreSlim _executionSemaphore;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private RawReplState _rawReplState;
    
    public SerialDeviceCommunication(string portName, int baudRate = 115200)
    public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    public async Task EnterRawReplAsync(CancellationToken cancellationToken = default)
    public async Task EnterRawPasteModeAsync(CancellationToken cancellationToken = default)
    public async Task SendCodeWithFlowControlAsync(string code, CancellationToken cancellationToken = default)
    public async Task ExitRawReplAsync(CancellationToken cancellationToken = default)
    // Implementation details...
}

public enum RawReplState
{
    Normal,
    Raw,
    RawPaste
}
```

#### `SubprocessDeviceCommunication` Class
```csharp
public class SubprocessDeviceCommunication : IDeviceCommunication
{
    private readonly Process _micropythonProcess;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;
    private readonly SemaphoreSlim _executionSemaphore;
    
    public SubprocessDeviceCommunication(string micropythonExecutablePath = "micropython")
    public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    public async Task EnterRawReplAsync(CancellationToken cancellationToken = default)
    // Subprocess-specific implementation for testing
}
```

#### `WebReplDeviceCommunication` Class
```csharp
public class WebReplDeviceCommunication : IDeviceCommunication
{
    private readonly ClientWebSocket _webSocket;
    private readonly Uri _deviceUri;
    
    public WebReplDeviceCommunication(string deviceUrl, string password = null)
    // WebSocket-based communication implementation
}
```

### 2. Device Management (`Belay.Core`)

#### `Device` Base Class
```csharp
public class Device : IDisposable
{
    private readonly IDeviceCommunication _communication;
    private readonly ILogger<Device> _logger;
    private readonly List<IExecutor> _executors;
    private readonly DeviceImplementation _implementation;
    
    public Device(IDeviceCommunication communication, ILogger<Device> logger = null)
    public Device(string connectionString, ILogger<Device> logger = null)
    
    // Decorator pattern methods for attribute-based programming
    public TaskExecutor Task => _taskExecutor;
    public SetupExecutor Setup => _setupExecutor;
    public ThreadExecutor Thread => _threadExecutor;
    public TeardownExecutor Teardown => _teardownExecutor;
    
    // Direct execution methods
    public async Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default)
    public async Task ExecuteAsync(string code, CancellationToken cancellationToken = default)
    
    // Proxy object creation
    public IProxyObject Proxy(string objectName)
    
    // File synchronization
    public async Task SyncAsync(string localPath, string remotePath = "/", 
        SyncOptions options = null, CancellationToken cancellationToken = default)
    
    // Device information
    public DeviceImplementation Implementation { get; }
    public DeviceCapabilities Capabilities { get; }
}
```

#### Device Factory and Connection String Support
```csharp
public static class DeviceFactory
{
    public static Device Create(string connectionString, IServiceProvider serviceProvider = null)
    {
        // Examples:
        // "serial:COM3:115200"
        // "webrepl:192.168.1.100:8266:password"
        // "usb:auto" (auto-detect USB device)
        // "subprocess:micropython" (for testing with unix port)
        // "subprocess:/path/to/micropython/ports/unix/build-standard/micropython"
    }
}
```

### 3. Attribute-Based Programming (`Belay.Attributes`)

#### Execution Attributes
```csharp
[AttributeUsage(AttributeTargets.Method)]
public class TaskAttribute : ExecutorAttribute
{
    public bool Minify { get; set; } = true;
    public bool Record { get; set; } = false;
    public bool Trusted { get; set; } = false;
    public string Implementation { get; set; } = "";
}

[AttributeUsage(AttributeTargets.Method)]
public class SetupAttribute : ExecutorAttribute
{
    public bool AutoInit { get; set; } = false;
    public bool IgnoreErrors { get; set; } = false;
    public string Implementation { get; set; } = "";
}

[AttributeUsage(AttributeTargets.Method)]
public class ThreadAttribute : ExecutorAttribute
{
    public bool Record { get; set; } = true;
    public string Implementation { get; set; } = "";
}

[AttributeUsage(AttributeTargets.Method)]
public class TeardownAttribute : ExecutorAttribute
{
    public bool IgnoreErrors { get; set; } = false;
    public string Implementation { get; set; } = "";
}
```

#### Usage Example with Custom Device Class
```csharp
public class MyIoTDevice : Device
{
    public MyIoTDevice(string connectionString) : base(connectionString) { }
    
    [Setup(AutoInit = true)]
    public async Task InitializeHardware()
    {
        // C# code here gets converted to Python and executed on device
        await ExecuteAsync(@"
from machine import Pin, ADC
led_pin = Pin(25, Pin.OUT)
sensor_pin = ADC(4)
");
    }
    
    [Task]
    public async Task<bool> SetLedAsync(bool state)
    {
        return await ExecuteAsync<bool>($"led_pin.value({(state ? 1 : 0)})");
    }
    
    [Task]
    public async Task<double> ReadTemperatureAsync()
    {
        return await ExecuteAsync<double>(@"
reading = sensor_pin.read_u16()
reading *= 3.3 / 65535
temperature = 27 - (reading - 0.706) / 0.001721
return temperature
");
    }
    
    [Thread(Implementation = "micropython")]
    public async Task StartBlinkingAsync()
    {
        await ExecuteAsync(@"
def blink_loop():
    while True:
        led_pin.toggle()
        time.sleep(0.5)
        
import _thread
_thread.start_new_thread(blink_loop, ())
");
    }
}
```

### 4. Executor System (`Belay.Core`)

#### Base Executor Interface
```csharp
public interface IExecutor
{
    Task<T> ExecuteAsync<T>(MethodInfo method, object[] args, CancellationToken cancellationToken = default);
    Task ExecuteAsync(MethodInfo method, object[] args, CancellationToken cancellationToken = default);
    Task DeployAsync(MethodInfo method, CancellationToken cancellationToken = default);
}
```

#### Task Executor Implementation
```csharp
public class TaskExecutor : IExecutor
{
    private readonly Device _device;
    private readonly ConcurrentDictionary<string, DeployedFunction> _deployedFunctions;
    
    public async Task<T> ExecuteAsync<T>(MethodInfo method, object[] args, CancellationToken cancellationToken = default)
    {
        var functionName = method.Name;
        
        // Ensure function is deployed
        if (!_deployedFunctions.ContainsKey(functionName))
        {
            await DeployAsync(method, cancellationToken);
        }
        
        // Execute with optimized call
        var arguments = SerializeArguments(args);
        var result = await _device.ExecuteAsync<T>($"{functionName}({arguments})", cancellationToken);
        
        return result;
    }
    
    public async Task DeployAsync(MethodInfo method, CancellationToken cancellationToken = default)
    {
        var pythonCode = CSharpToPythonConverter.Convert(method);
        var minifiedCode = _options.Minify ? PythonMinifier.Minify(pythonCode) : pythonCode;
        
        await _device.ExecuteAsync(minifiedCode, cancellationToken);
        
        _deployedFunctions[method.Name] = new DeployedFunction
        {
            Method = method,
            PythonCode = pythonCode,
            DeployedAt = DateTime.UtcNow
        };
    }
}
```

### 5. Dynamic Proxy System (`Belay.Proxy`)

#### Proxy Interface and Implementation
```csharp
public interface IProxyObject
{
    Task<T> GetAsync<T>(string propertyName, CancellationToken cancellationToken = default);
    Task SetAsync(string propertyName, object value, CancellationToken cancellationToken = default);
    Task<T> CallAsync<T>(string methodName, params object[] args);
    Task CallAsync(string methodName, params object[] args);
    IProxyObject this[string key] { get; }
    IProxyObject this[int index] { get; }
}

public class ProxyObject : IProxyObject
{
    private readonly Device _device;
    private readonly string _objectPath;
    
    public ProxyObject(Device device, string objectPath)
    {
        _device = device;
        _objectPath = objectPath;
    }
    
    public async Task<T> GetAsync<T>(string propertyName, CancellationToken cancellationToken = default)
    {
        return await _device.ExecuteAsync<T>($"{_objectPath}.{propertyName}", cancellationToken);
    }
    
    public IProxyObject this[string key] => new ProxyObject(_device, $"{_objectPath}['{key}']");
    public IProxyObject this[int index] => new ProxyObject(_device, $"{_objectPath}[{index}]");
}
```

#### Usage Example
```csharp
var device = new Device("serial:COM3");

// Create a user object on the device
await device.ExecuteAsync(@"
class User:
    def __init__(self, name):
        self.name = name
    
    def greetings(self):
        return f'Hello {self.name}!'

user = User('Alice')
");

// Create proxy to interact with the remote object
var userProxy = device.Proxy("user");

// Get property value
var name = await userProxy.GetAsync<string>("name");
Console.WriteLine($"User name: {name}"); // User name: Alice

// Call method
var greeting = await userProxy.CallAsync<string>("greetings");
Console.WriteLine(greeting); // Hello Alice!
```

### 6. File Synchronization System (`Belay.Sync`)

#### Sync Options and Configuration
```csharp
public class SyncOptions
{
    public string[] IgnorePatterns { get; set; } = { "*.tmp", "*.log", ".git", "bin", "obj" };
    public string[] KeepFiles { get; set; } = { "boot.py", "webrepl_cfg.py", "lib" };
    public bool Minify { get; set; } = true;
    public string MpyCrossPath { get; set; }
    public bool DeleteRemoteFiles { get; set; } = true;
    public IProgress<SyncProgress> Progress { get; set; }
}

public class SyncProgress
{
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public string CurrentFile { get; set; }
    public SyncOperation Operation { get; set; } // Upload, Delete, Skip
}
```

#### Synchronization Service
```csharp
public class FileSynchronizer
{
    private readonly Device _device;
    private readonly ILogger<FileSynchronizer> _logger;
    
    public async Task SyncAsync(string localPath, string remotePath = "/", 
        SyncOptions options = null, CancellationToken cancellationToken = default)
    {
        options ??= new SyncOptions();
        
        // Discover local files
        var localFiles = DiscoverFiles(localPath, options.IgnorePatterns);
        
        // Get remote file hashes
        var remoteHashes = await GetRemoteFileHashesAsync(remotePath, cancellationToken);
        
        // Calculate sync operations
        var operations = CalculateSyncOperations(localFiles, remoteHashes, options);
        
        // Execute sync operations
        await ExecuteSyncOperationsAsync(operations, options.Progress, cancellationToken);
    }
    
    private async Task<Dictionary<string, string>> GetRemoteFileHashesAsync(
        string remotePath, CancellationToken cancellationToken)
    {
        // Deploy hash computation function if not present
        await _device.ExecuteAsync(HashComputationScript, cancellationToken);
        
        // Get hashes for all remote files
        var result = await _device.ExecuteAsync<Dictionary<string, string>>(
            $"compute_file_hashes('{remotePath}')", cancellationToken);
            
        return result;
    }
}
```

### 7. Package Management System (`Belay.PackageManager`)

#### Package Configuration
```csharp
public class BelayProjectConfiguration
{
    public string ProjectName { get; set; }
    public string[] IgnorePatterns { get; set; }
    public Dictionary<string, PackageDependency[]> Dependencies { get; set; }
    public Dictionary<string, DependencyGroup> Groups { get; set; }
    public string DependenciesPath { get; set; } = ".belay/dependencies";
}

public class PackageDependency
{
    public string Uri { get; set; }
    public bool Develop { get; set; } = false;
    public bool RenameToInit { get; set; } = false;
    public string Version { get; set; }
}

public class DependencyGroup
{
    public bool Optional { get; set; } = false;
    public Dictionary<string, PackageDependency[]> Dependencies { get; set; }
}
```

#### Package Manager Service
```csharp
public class PackageManager
{
    private readonly IPackageDownloader[] _downloaders;
    private readonly IPackageCache _cache;
    private readonly ILogger<PackageManager> _logger;
    
    public async Task InstallAsync(string packageSpecifier, string group = "main", 
        CancellationToken cancellationToken = default)
    {
        // Parse package specifier (e.g., "github:user/repo", "https://example.com/package.py")
        var packageInfo = PackageSpecifier.Parse(packageSpecifier);
        
        // Download and cache
        var packagePath = await DownloadPackageAsync(packageInfo, cancellationToken);
        
        // Add to project configuration
        await AddToProjectConfigurationAsync(packageInfo, group, cancellationToken);
        
        // Sync to device if requested
        if (_options.AutoSync)
        {
            await SyncDependenciesAsync(cancellationToken);
        }
    }
    
    public async Task SyncDependenciesAsync(CancellationToken cancellationToken = default)
    {
        var config = await LoadProjectConfigurationAsync(cancellationToken);
        var tempDir = CreateVirtualEnvironment(config);
        
        try
        {
            // Sync virtual environment to device
            await _device.SyncAsync(tempDir, "/lib", new SyncOptions
            {
                DeleteRemoteFiles = true,
                IgnorePatterns = new[] { "*.pyc", "__pycache__" }
            }, cancellationToken);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
```

### 8. Dependency Injection and Configuration (`Belay.Extensions`)

#### Service Collection Extensions
```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBelay(this IServiceCollection services, 
        Action<BelayOptions> configureOptions = null)
    {
        services.Configure(configureOptions ?? (_ => { }));
        
        services.AddSingleton<IPackageCache, FileSystemPackageCache>();
        services.AddSingleton<PackageManager>();
        services.AddSingleton<FileSynchronizer>();
        
        services.AddTransient<IPackageDownloader, GitHubPackageDownloader>();
        services.AddTransient<IPackageDownloader, HttpPackageDownloader>();
        
        services.AddTransient<Device>(provider => 
        {
            var options = provider.GetRequiredService<IOptions<BelayOptions>>().Value;
            var logger = provider.GetService<ILogger<Device>>();
            return new Device(options.DefaultConnectionString, logger);
        });
        
        return services;
    }
    
    public static IServiceCollection AddDevice<TDevice>(this IServiceCollection services,
        string connectionString) where TDevice : Device
    {
        services.AddTransient<TDevice>(provider =>
        {
            var logger = provider.GetService<ILogger<TDevice>>();
            return (TDevice)Activator.CreateInstance(typeof(TDevice), connectionString, logger);
        });
        
        return services;
    }
}
```

#### Configuration Options
```csharp
public class BelayOptions
{
    public string DefaultConnectionString { get; set; } = "usb:auto";
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool MinifyByDefault { get; set; } = true;
    public string TempDirectory { get; set; } = Path.GetTempPath();
    public LogLevel DefaultLogLevel { get; set; } = LogLevel.Information;
    public Dictionary<string, string> DeviceProfiles { get; set; } = new();
}
```

### 9. ASP.NET Core Integration

#### Device Controller Example
```csharp
[ApiController]
[Route("api/[controller]")]
public class DeviceController : ControllerBase
{
    private readonly MyIoTDevice _device;
    private readonly ILogger<DeviceController> _logger;
    
    public DeviceController(MyIoTDevice device, ILogger<DeviceController> logger)
    {
        _device = device;
        _logger = logger;
    }
    
    [HttpPost("led/{state}")]
    public async Task<IActionResult> SetLed(bool state, CancellationToken cancellationToken)
    {
        try
        {
            await _device.SetLedAsync(state);
            return Ok(new { success = true, state });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set LED state");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
    
    [HttpGet("temperature")]
    public async Task<IActionResult> GetTemperature(CancellationToken cancellationToken)
    {
        try
        {
            var temperature = await _device.ReadTemperatureAsync();
            return Ok(new { temperature, timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read temperature");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}
```

### 10. WPF/WinUI Integration

#### Device Service for Desktop Applications
```csharp
public class DeviceService : INotifyPropertyChanged
{
    private readonly MyIoTDevice _device;
    private double _temperature;
    private bool _ledState;
    
    public DeviceService(MyIoTDevice device)
    {
        _device = device;
        
        // Start background monitoring
        _ = Task.Run(MonitorDeviceAsync);
    }
    
    public double Temperature
    {
        get => _temperature;
        private set
        {
            if (_temperature != value)
            {
                _temperature = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool LedState
    {
        get => _ledState;
        set
        {
            if (_ledState != value)
            {
                _ledState = value;
                OnPropertyChanged();
                _ = Task.Run(() => _device.SetLedAsync(value));
            }
        }
    }
    
    private async Task MonitorDeviceAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                Temperature = await _device.ReadTemperatureAsync();
                await Task.Delay(1000, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                // Handle connection errors
            }
        }
    }
    
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

## Key Design Principles

### 1. Async-First Design
- All device communication is asynchronous using Task-based patterns
- Supports cancellation tokens throughout the API
- Non-blocking operations for responsive applications

### 2. Strong Typing
- Generic return types for device method calls
- Compile-time type checking where possible
- Rich exception hierarchy for detailed error handling

### 3. Dependency Injection Ready
- All major components support DI containers
- Configuration-driven device setup
- Testable architecture with interface abstractions

### 4. Cross-Platform Compatibility
- .NET 6+ support for Windows, Linux, macOS
- Platform-specific optimizations where beneficial
- Consistent API across platforms

### 5. Performance Optimized
- Connection pooling and reuse
- Efficient serialization/deserialization
- Lazy loading of device capabilities

### 6. Enterprise Ready
- Structured logging with Microsoft.Extensions.Logging
- Configuration management with IConfiguration
- Health checks and monitoring support
- Comprehensive documentation and samples

This architecture provides a robust foundation for the Belay.NET library, enabling seamless integration of MicroPython devices into C# applications while maintaining the simplicity and power of the original Python Belay library.