# Issue 001-003: Subprocess Communication for Testing

**Epic**: Epic 001 - Device Communication Foundation  
**Status**: Implementation Complete - Integration Testing Blocked  
**Priority**: High  
**Estimated Effort**: 0.75 weeks  
**Assignee**: TBD  
**Dependencies**: Issue 001-001 (Raw REPL Protocol)

## Summary

Implement subprocess-based communication with MicroPython unix port executable to enable testing without physical hardware. This provides a reliable, fast testing environment that closely mimics real device behavior while being completely software-based.

## Technical Requirements

### Core Subprocess Communication
```csharp
public class SubprocessDeviceCommunication : IDeviceCommunication
{
    private readonly Process _micropythonProcess;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;
    private readonly StreamReader _stderr;
    private readonly RawReplProtocol _replProtocol;
    private readonly SemaphoreSlim _executionSemaphore;
    private volatile bool _disposed;
    
    public SubprocessDeviceCommunication(string micropythonExecutablePath = "micropython",
        string[] additionalArgs = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = micropythonExecutablePath,
            Arguments = string.Join(" ", additionalArgs ?? Array.Empty<string>()),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };
        
        _micropythonProcess = new Process { StartInfo = startInfo };
    }
    
    public async Task<string> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    public async Task<T> ExecuteAsync<T>(string code, CancellationToken cancellationToken = default)  
    public async Task StartAsync(CancellationToken cancellationToken = default)
    public async Task StopAsync(CancellationToken cancellationToken = default)
}
```

### MicroPython Unix Port Integration
```csharp
public static class MicroPythonUnixPort
{
    public static async Task<string> FindExecutableAsync()
    {
        // Search for micropython executable in common locations:
        // 1. Environment PATH
        // 2. ./micropython/ports/unix/build-standard/micropython (submodule)
        // 3. /usr/local/bin/micropython (system install)
        // 4. User-specified paths via configuration
        
        var searchPaths = new[]
        {
            "micropython", // PATH lookup
            Path.Combine("micropython", "ports", "unix", "build-standard", "micropython"),
            "/usr/local/bin/micropython",
            "/opt/micropython/bin/micropython"
        };
        
        foreach (var path in searchPaths)
        {
            if (await IsValidMicroPythonExecutableAsync(path))
            {
                return Path.GetFullPath(path);
            }
        }
        
        throw new FileNotFoundException("MicroPython unix port executable not found");
    }
    
    public static async Task<bool> BuildUnixPortAsync(string micropythonRepoPath, 
        CancellationToken cancellationToken = default)
    {
        // Automated build of unix port if source available
        var unixPortPath = Path.Combine(micropythonRepoPath, "ports", "unix");
        
        if (!Directory.Exists(unixPortPath))
        {
            return false;
        }
        
        // Run: make submodules && make
        var makeSubmodules = new ProcessStartInfo("make", "submodules")
        {
            WorkingDirectory = unixPortPath,
            UseShellExecute = false
        };
        
        using var submodulesProcess = Process.Start(makeSubmodules);
        await submodulesProcess.WaitForExitAsync(cancellationToken);
        
        if (submodulesProcess.ExitCode != 0)
        {
            return false;
        }
        
        var makeBuild = new ProcessStartInfo("make", "")
        {
            WorkingDirectory = unixPortPath,
            UseShellExecute = false
        };
        
        using var buildProcess = Process.Start(makeBuild);
        await buildProcess.WaitForExitAsync(cancellationToken);
        
        return buildProcess.ExitCode == 0;
    }
}
```

## Process Management

### Lifecycle Management  
```csharp
public async Task StartAsync(CancellationToken cancellationToken = default)
{
    if (_micropythonProcess.HasExited)
    {
        throw new InvalidOperationException("Process has already exited");
    }
    
    // Start the process
    _micropythonProcess.Start();
    
    // Setup stream wrappers
    _stdin = _micropythonProcess.StandardInput;
    _stdout = _micropythonProcess.StandardOutput;
    _stderr = _micropythonProcess.StandardError;
    
    // Initialize raw REPL protocol over stdin/stdout
    var combinedStream = new DuplexStream(_stdin.BaseStream, _stdout.BaseStream);
    await _replProtocol.InitializeAsync(combinedStream, cancellationToken);
    
    // Start background output monitoring
    _ = Task.Run(() => MonitorOutputAsync(_cancellationTokenSource.Token));
    _ = Task.Run(() => MonitorErrorAsync(_cancellationTokenSource.Token));
    
    // Wait for MicroPython to be ready
    await WaitForReadyStateAsync(cancellationToken);
}

private async Task WaitForReadyStateAsync(CancellationToken cancellationToken)
{
    // Wait for ">>>" prompt indicating MicroPython is ready
    var buffer = new char[3];
    int position = 0;
    
    while (!cancellationToken.IsCancellationRequested)
    {
        var ch = (char)await _stdout.ReadAsync();
        
        if (ch == '>')
        {
            buffer[position] = ch;
            position = (position + 1) % 3;
            
            if (buffer[0] == '>' && buffer[1] == '>' && buffer[2] == '>')
            {
                return; // Ready!
            }
        }
        else
        {
            position = 0;
        }
    }
}
```

### Output Handling
```csharp
private async Task MonitorOutputAsync(CancellationToken cancellationToken)
{
    try
    {
        while (!cancellationToken.IsCancellationRequested && !_micropythonProcess.HasExited)
        {
            var line = await _stdout.ReadLineAsync();
            if (line != null)
            {
                OutputReceived?.Invoke(this, new DeviceOutputEventArgs(line));
            }
        }
    }
    catch (Exception ex)
    {
        // Log error but don't crash
        _logger?.LogError(ex, "Error monitoring subprocess output");
    }
}

private async Task MonitorErrorAsync(CancellationToken cancellationToken)
{
    try
    {
        while (!cancellationToken.IsCancellationRequested && !_micropythonProcess.HasExited)
        {
            var line = await _stderr.ReadLineAsync();
            if (line != null)
            {
                // Treat stderr output as device errors
                OutputReceived?.Invoke(this, new DeviceOutputEventArgs(line, isError: true));
            }
        }
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "Error monitoring subprocess stderr");
    }
}
```

## Stream Abstraction

### Duplex Stream Implementation
```csharp
public class DuplexStream : Stream
{
    private readonly Stream _inputStream;
    private readonly Stream _outputStream;
    
    public DuplexStream(Stream inputStream, Stream outputStream)
    {
        _inputStream = inputStream ?? throw new ArgumentNullException(nameof(inputStream));
        _outputStream = outputStream ?? throw new ArgumentNullException(nameof(outputStream));
    }
    
    public override bool CanRead => _outputStream.CanRead;
    public override bool CanWrite => _inputStream.CanWrite;
    public override bool CanSeek => false;
    
    public override int Read(byte[] buffer, int offset, int count)
    {
        return _outputStream.Read(buffer, offset, count);
    }
    
    public override void Write(byte[] buffer, int offset, int count)
    {
        _inputStream.Write(buffer, offset, count);
        _inputStream.Flush(); // Ensure immediate transmission
    }
    
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _outputStream.ReadAsync(buffer, offset, count, cancellationToken);
    }
    
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return _inputStream.WriteAsync(buffer, offset, count, cancellationToken);
    }
    
    // ... other Stream methods
}
```

## Testing Infrastructure

### Test Helpers
```csharp
public static class SubprocessTestHelper
{
    public static async Task<SubprocessDeviceCommunication> CreateTestDeviceAsync()
    {
        var executablePath = await MicroPythonUnixPort.FindExecutableAsync();
        var device = new SubprocessDeviceCommunication(executablePath);
        await device.StartAsync();
        return device;
    }
    
    public static async Task<bool> IsMicroPythonAvailableAsync()
    {
        try
        {
            await MicroPythonUnixPort.FindExecutableAsync();
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }
}

[TestFixture]
[Category("Subprocess")]
public class SubprocessDeviceCommunicationTests
{
    private SubprocessDeviceCommunication _device;
    
    [OneTimeSetUp]
    public async Task SetUp()
    {
        if (!await SubprocessTestHelper.IsMicroPythonAvailableAsync())
        {
            Assert.Ignore("MicroPython unix port not available for testing");
        }
        
        _device = await SubprocessTestHelper.CreateTestDeviceAsync();
    }
    
    [Test]
    public async Task ExecuteSimpleExpression_ShouldReturnResult()
    {
        var result = await _device.ExecuteAsync("2 + 2");
        Assert.AreEqual("4", result.Trim());
    }
    
    [Test]
    public async Task ExecuteWithError_ShouldThrowException()
    {
        var ex = await Assert.ThrowsAsync<DeviceExecutionException>(
            () => _device.ExecuteAsync("1 / 0"));
        
        Assert.That(ex.Message, Contains.Substring("division by zero"));
    }
    
    [OneTimeTearDown]
    public async Task TearDown()
    {
        if (_device != null)
        {
            await _device.StopAsync();
            _device.Dispose();
        }
    }
}
```

## CI/CD Integration

### Build Script Integration
```yaml
# .github/workflows/test.yml
name: Tests

on: [push, pull_request]

jobs:
  test-with-micropython:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
      with:
        submodules: recursive
        
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
        
    - name: Install build dependencies
      run: |
        sudo apt-get update
        sudo apt-get install -y gcc make build-essential
        
    - name: Build MicroPython unix port
      run: |
        cd micropython/ports/unix
        make submodules
        make
        
    - name: Run tests
      run: |
        export MICROPYTHON_EXECUTABLE=$(pwd)/micropython/ports/unix/build-standard/micropython
        dotnet test --filter Category=Subprocess
```

## Configuration Support

### Subprocess Configuration Options
```csharp
public class SubprocessCommunicationOptions
{
    public string ExecutablePath { get; set; } = "micropython";
    public string[] AdditionalArguments { get; set; } = Array.Empty<string>();
    public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;
    public TimeSpan StartupTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public bool AutoBuildIfNeeded { get; set; } = true;
    public string MicroPythonRepoPath { get; set; } = "./micropython";
}
```

## Performance Characteristics

### Expected Performance
- **Startup time**: <1 second (vs 2-5 seconds for serial)
- **Execution overhead**: <10ms (vs 20-50ms for serial)
- **Reliability**: >99.9% (no hardware dependencies)
- **Concurrency**: Full support for parallel test execution

### Benchmarking
```csharp
[Test]
[Category("Performance")]
public async Task BenchmarkExecutionSpeed()
{
    const int iterations = 100;
    var stopwatch = Stopwatch.StartNew();
    
    for (int i = 0; i < iterations; i++)
    {
        await _device.ExecuteAsync("42");
    }
    
    stopwatch.Stop();
    var averageTime = stopwatch.ElapsedMilliseconds / (double)iterations;
    
    Assert.That(averageTime, Is.LessThan(20), 
        $"Average execution time {averageTime}ms exceeds threshold");
}
```

## Implementation Plan

### Phase 1: Core Infrastructure (2 days)
- Implement SubprocessDeviceCommunication class
- Create process lifecycle management
- Implement DuplexStream for stdin/stdout handling
- Basic unit tests

### Phase 2: Integration & Robustness (1.5 days)
- MicroPython executable discovery and validation
- Automated unix port building capability
- Error handling and process monitoring
- Test infrastructure setup

### Phase 3: CI/CD & Documentation (0.5 days)  
- CI/CD pipeline integration
- Performance benchmarking
- Documentation and examples
- Final testing and validation

## Acceptance Criteria

### Functional Requirements
- [ ] Successfully start and communicate with MicroPython unix port
- [ ] Execute Python code with same reliability as serial communication
- [ ] Automatic discovery and building of MicroPython executable
- [ ] Proper process lifecycle management and cleanup
- [ ] Support for parallel test execution

### Performance Requirements
- [ ] Startup time <1 second
- [ ] Execution overhead <20ms per operation
- [ ] Support >100 concurrent operations per second
- [ ] Memory usage <2MB per subprocess instance

### Integration Requirements
- [ ] Seamless integration with CI/CD pipelines
- [ ] Cross-platform support (Linux primary, Windows secondary)
- [ ] Automated test infrastructure using subprocess communication
- [ ] Performance benchmarking and monitoring

## Definition of Done

- [ ] All acceptance criteria met and validated
- [ ] Comprehensive unit test coverage (>90%)
- [ ] CI/CD pipeline integration working
- [ ] Performance benchmarks meet requirements
- [ ] Cross-platform compatibility verified
- [ ] Code review approved
- [ ] Documentation complete and accurate
- [ ] Memory and resource leak testing passed

This subprocess communication implementation provides essential testing infrastructure that enables rapid development and validation without dependence on physical hardware.