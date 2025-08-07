# Belay Technical Specification

## Executive Summary

Belay is a Python library and CLI tool that enables seamless communication between host Python code and MicroPython/CircuitPython compatible microcontrollers. It abstracts the complexity of device communication, code synchronization, and execution management through decorator-based APIs and provides a Poetry-inspired package manager for MicroPython dependencies.

## Core Architecture

### 1. Device Communication Layer (`belay/pyboard.py`)

**Purpose**: Low-level communication protocol derived from MicroPython's mpremote project.

**Key Features**:
- Serial/USB communication (`/dev/ttyUSB0`, `COM3`, etc.)
- WebREPL wireless communication (`ws://192.168.1.100:8266`)
- USB device auto-detection and selection
- Raw REPL protocol implementation
- Filesystem operations (put/get files)
- Process management and cancellation

**Communication Protocol**:
- Uses MicroPython's raw REPL mode for reliable execution
- Implements custom response parsing with `_BELAYR` prefixed responses
- Supports both expression evaluation and statement execution
- Handles device disconnection/reconnection with state restoration

### 2. Device Management (`belay/device.py`)

**Purpose**: Main interface class that manages connections and coordinates all device operations.

**Key Components**:

#### Device Class
- **Connection Management**: Establishes and maintains device connections
- **Code Execution**: Executes Python code on remote devices with `device(code_string)`
- **State Management**: Tracks command history for reconnection scenarios
- **Implementation Detection**: Automatically detects MicroPython vs CircuitPython
- **Emitter Support**: Detects and utilizes native/viper compilation modes

#### Decorator System
Four primary decorators for different execution patterns:

1. **`@device.setup`**: Executes code in global context during device initialization
2. **`@device.task`**: Optimized function execution with pre-deployed code
3. **`@device.thread`**: Spawns background threads on device (MicroPython only)
4. **`@device.teardown`**: Cleanup code executed when device connection closes

#### Advanced Features
- **Minification**: Automatic code minification to reduce transmission overhead
- **Implementation Targeting**: Execute different code based on MicroPython/CircuitPython
- **Auto-initialization**: Automatic execution of setup functions during device creation
- **Traceback Mapping**: Maps on-device errors back to host source files with correct line numbers

### 3. Function Execution System (`belay/executers.py`)

**Purpose**: Handles the conversion of Python functions to device-executable code.

#### Executer Classes

**TaskExecuter**:
- Pre-deploys function source code to device during decoration
- Creates lightweight execution wrapper that calls deployed functions
- Supports both regular functions and generators
- Implements generator state management across host-device boundary
- Provides return value parsing and type safety controls

**SetupExecuter/TeardownExecuter**:
- Executes code in global device context
- Used for device initialization and cleanup
- Supports variable assignment and global state management

**ThreadExecuter**:
- Spawns MicroPython threads using `_thread.start_new_thread()`
- Not available on CircuitPython (single-threaded)
- Provides asynchronous execution capabilities

#### Code Processing Pipeline
1. **Source Extraction**: Uses AST analysis to extract function source code
2. **Minification**: Optional code minification for bandwidth efficiency
3. **Deployment**: Sends processed code to device during decoration
4. **Execution**: Creates optimized execution wrappers for function calls

### 4. Proxy Object System (`belay/proxy_object.py`)

**Purpose**: Creates host-side objects that mirror device-side objects, enabling transparent remote object interaction.

**Key Features**:
- **Attribute Access**: `proxy_obj.attribute` translates to device-side access
- **Method Calls**: `proxy_obj.method(args)` executes methods on device
- **Property Access**: Supports getting/setting object properties
- **Indexing Support**: Handles `proxy_obj[key]` operations
- **Magic Method Support**: Implements `__len__`, `__call__`, etc.

**Implementation Details**:
- Uses `__getattribute__` and `__setattr__` overrides for transparent access
- Lazy evaluation - creates new proxy objects for chained access
- Error mapping from device exceptions to appropriate host exceptions
- Automatic syntax error handling for method vs property differentiation

### 5. File Synchronization System (`belay/device_sync_support.py`)

**Purpose**: Efficiently synchronizes local project files with device filesystem.

**Synchronization Process**:
1. **File Discovery**: Scans local directories respecting ignore patterns
2. **Hash Comparison**: Computes and compares file hashes (FNV1a32) between host and device
3. **Selective Transfer**: Only transfers files that have changed
4. **Cleanup Management**: Removes device files not present locally (with keep protection)
5. **Directory Management**: Creates/removes directories as needed

**Advanced Features**:
- **Native Hashing**: Uses compiled native modules for fast hash computation
- **mpy-cross Integration**: Automatic compilation of .py files to .mpy format
- **Ignore Patterns**: Supports gitignore-style patterns for file exclusion
- **Progress Reporting**: Integration with rich progress bars for UI feedback

### 6. Package Management System (`belay/packagemanager/`)

**Purpose**: Poetry-inspired dependency management for MicroPython projects.

#### Configuration Model (`models.py`)
- **Pydantic-based validation** of `pyproject.toml` configuration
- **Dependency Groups**: Support for main, dev, and custom dependency groups
- **URI Support**: GitHub, local files, and other URI schemes
- **Development Mode**: Editable installs for local development

#### Dependency Resolution (`group.py`)
- **Caching System**: Local caching of downloaded dependencies
- **Version Management**: Tracks and updates dependency versions
- **Conflict Resolution**: Handles dependency conflicts and overrides

#### Download System (`downloaders/`)
- **GitHub Integration**: Direct GitHub repository and file downloads
- **HTTP Support**: Generic HTTP(S) file downloads
- **Validation**: Integrity checking and validation of downloaded content

### 7. Command Line Interface (`belay/cli/`)

**Purpose**: Provides comprehensive CLI tools for MicroPython development.

#### Core Commands
- **`belay run`**: Execute Python scripts with device connection
- **`belay sync`**: Synchronize files to device
- **`belay install`**: Install MicroPython packages
- **`belay clean`**: Clean device filesystem
- **`belay terminal`**: Interactive device terminal
- **`belay select`**: Device selection and management

#### Advanced Features
- **Virtual Environment**: Creates isolated MICROPYPATH environments
- **Project Templates**: Generates new project structures
- **Cache Management**: Dependency cache operations
- **Device Information**: Hardware and firmware detection

## Technical Implementation Details

### Communication Protocol

**Raw REPL Protocol**:
- Enters raw REPL mode for reliable code execution
- Uses special response prefixes (`_BELAYR`) to distinguish output from code results
- Implements timeout handling and connection recovery
- Supports both soft and hard resets

**Response Parsing**:
```python
# Response format: _BELAY + code + data
# R = Result, S = StopIteration (for generators)
if line.startswith("_BELAYR"):
    return ast.literal_eval(line[7:])  # Parse result
```

**Error Handling**:
- Maps device-side tracebacks to host source files
- Preserves line number information across host-device boundary
- Provides context-aware error messages

### Code Minification

**Minification Engine** (`belay/_minify.py`):
- Removes comments and docstrings
- Eliminates unnecessary whitespace
- Preserves Python syntax and semantics
- Configurable minification levels

### Memory Management

**Device Memory Considerations**:
- Efficient code deployment to minimize device memory usage
- Cleanup of temporary variables and functions
- Garbage collection integration
- Memory-aware file synchronization

**Host Memory Management**:
- Connection pooling for multiple devices
- Cached compilation results
- Efficient file hash storage

## Security Considerations

### Code Execution Safety
- **Trusted/Untrusted Modes**: Controls which return types can be parsed
- **Expression Validation**: AST-based validation of executable code
- **Sandboxing**: Limited execution context on device

### Communication Security
- **Serial Communication**: Physical access required
- **WebREPL Security**: Basic authentication support
- **No Network Exposure**: Device code not exposed to network

## Performance Characteristics

### Execution Performance
- **Task Deployment**: ~50-200ms initial deployment per function
- **Task Execution**: ~5-20ms per function call after deployment
- **File Synchronization**: Hash-based differential sync, ~1-5 files/second
- **WebREPL Latency**: ~20-100ms additional latency vs serial

### Memory Usage
- **Host Memory**: ~10-50MB for typical projects
- **Device Memory**: ~1-10KB per deployed function
- **File Transfer**: Streaming with configurable buffer sizes

## Supported Hardware Platforms

### MicroPython Boards
- **Raspberry Pi Pico/Pico W**
- **ESP32/ESP8266**
- **pyboard v1.1**
- **Generic STM32-based boards**

### CircuitPython Boards
- **Adafruit boards** (Feather, Metro, etc.)
- **Raspberry Pi Pico** (CircuitPython firmware)
- **SAMD-based boards**

### Connection Methods
- **Serial/USB**: Direct USB connection
- **WebREPL**: WiFi-based wireless connection
- **Telnet**: Network-based connection (experimental)

## Compatibility Matrix

| Feature | MicroPython | CircuitPython | Notes |
|---------|-------------|---------------|-------|
| @task decorator | ✓ | ✓ | Full support |
| @thread decorator | ✓ | ✗ | CircuitPython is single-threaded |
| @setup/@teardown | ✓ | ✓ | Full support |
| File sync | ✓ | ✓ | Full support |
| Proxy objects | ✓ | ✓ | Full support |
| Package manager | ✓ | ✓ | Full support |
| WebREPL | ✓ | ✗ | MicroPython only |
| Native compilation | ✓ | Limited | Platform dependent |

## Integration Points for C# Port

### Critical Components for C# Implementation
1. **Serial Communication Layer**: System.IO.Ports or LibUsbDotNet
2. **Code Execution Engine**: Roslyn for C# code compilation and execution
3. **Synchronization System**: File system watchers and hash comparison
4. **Proxy Object System**: Dynamic proxy generation with Castle.DynamicProxy
5. **Package Management**: NuGet-style dependency resolution

### Architecture Considerations for C#
- **Async/Await Pattern**: Replace Python's synchronous model with Task-based async
- **Strong Typing**: Leverage C# type system for better compile-time safety
- **Event-Driven Model**: Use events for device state changes and notifications
- **Dependency Injection**: Support IoC containers for better testability
- **Configuration System**: Use Microsoft.Extensions.Configuration

## Extension Points

### Custom Device Classes
- Subclass `Device` for hardware-specific functionality
- Override `__pre_autoinit__` and `__post_init__` for custom initialization
- Add hardware-specific methods and properties

### Custom Executers
- Implement `Executer` base class for specialized execution patterns
- Register custom executers with the `Registry` system
- Support for different communication protocols or execution models

### Package Manager Extensions
- Custom downloaders for proprietary package sources
- Alternative package formats and validation schemes
- Integration with corporate artifact repositories

This specification provides the foundation for implementing a C# equivalent that maintains the core functionality and ease-of-use of the Python Belay library while leveraging C#'s strengths in the Windows development ecosystem.