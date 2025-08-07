# CLAUDE_micropython.md - MicroPython Submodule Development Guide

This file provides guidance when working within the MicroPython submodule directory for Belay.NET development and testing purposes.

## Submodule Purpose

The MicroPython submodule serves multiple purposes for Belay.NET:

1. **Reference Implementation**: Access to official MicroPython Raw REPL protocol implementation
2. **Testing Infrastructure**: Unix port provides subprocess-based testing without hardware
3. **Protocol Documentation**: Official REPL documentation and implementation details
4. **Version Tracking**: Ensures compatibility testing against specific MicroPython versions

## Key Directories and Files

### Core REPL Implementation
- `./py/repl.c` - Core REPL implementation with raw mode handling
- `./py/lexer.c` - Python lexer used by REPL
- `./py/compile.c` - Python compiler integration
- `./shared/readline/readline.c` - Line editing and input handling

### Protocol Documentation
- `./docs/reference/repl.rst` - Official Raw REPL protocol documentation
- `./docs/reference/constrained.rst` - Memory and performance constraints
- `./docs/library/index.rst` - Built-in module documentation

### Unix Port (for Testing)
- `./ports/unix/` - Unix port implementation directory
- `./ports/unix/main.c` - Main entry point and REPL setup
- `./ports/unix/Makefile` - Build system for unix port
- `./ports/unix/build-standard/` - Build output directory

### Example Implementations
- `./tools/pyboard.py` - Python implementation of device communication (reference)
- `./tools/mpremote/` - Official remote control tool implementation

## Building Unix Port for Testing

### Prerequisites
```bash
# Install build dependencies
sudo apt-get install build-essential gcc make

# Ensure submodules are initialized
cd micropython
git submodule update --init
```

### Build Commands
```bash
cd ports/unix
make submodules  # Initialize required submodules
make             # Build the unix port
```

### Build Output
- Executable: `./build-standard/micropython`
- Usage: `./build-standard/micropython [script.py]`
- Interactive: `./build-standard/micropython` (starts REPL)

## Testing with Unix Port

### Basic REPL Testing
```bash
# Start interactive REPL
./build-standard/micropython

# Test raw mode manually
# 1. Press Ctrl-A to enter raw mode
# 2. Type Python code
# 3. Press Ctrl-D to execute
# 4. Press Ctrl-B to exit raw mode
```

### Subprocess Integration
```bash
# Use in subprocess for automated testing
echo "print('Hello from MicroPython')" | ./build-standard/micropython
```

### Raw REPL Protocol Testing
```bash
# Test raw-paste mode support
echo -e "\x05A\x01" | ./build-standard/micropython
# Should respond with window size if supported
```

## Protocol Implementation References

### Raw Mode Entry Sequence
1. **Normal to Raw**: Send `\x01` (Ctrl-A)
2. **Expected Response**: `"raw REPL; CTRL-B to exit\r\n>"`
3. **Code Transmission**: Send Python code as UTF-8
4. **Execution Trigger**: Send `\x04` (Ctrl-D)
5. **Response**: `"OK"` followed by output and `">"`
6. **Exit Raw Mode**: Send `\x02` (Ctrl-B)

### Raw-Paste Mode Protocol
1. **Entry**: From raw mode, send `\x05A\x01`
2. **Confirmation**: Read response (should start with "R")
3. **Window Size**: Read 16-bit little-endian window size increment
4. **Flow Control**: 
   - Send data respecting window size
   - Handle `\x01` (increase window) and `\x04` (end data) signals
5. **Completion**: Send `\x04` to signal end of data
6. **Output**: Read execution results until prompt

## Code References for Implementation

### Raw REPL State Management
Reference `./py/repl.c` functions:
- `pyexec_raw_repl()` - Main raw REPL loop
- `pyexec_raw_repl_process_char()` - Character processing
- `readline_process_char()` - Input processing

### Error Handling Patterns
Reference error handling in:
- `./py/runtime.c` - Exception handling
- `./py/obj.c` - Object system errors
- `./ports/unix/main.c` - Top-level error handling

## Development Practices

### Version Compatibility
- Always test against the pinned submodule version
- Document any version-specific protocol behavior
- Update submodule carefully with compatibility testing

### Performance Considerations
- Unix port may have different timing characteristics than embedded targets
- Memory constraints differ significantly from embedded devices
- I/O performance is much faster than serial connections

### Security Considerations
- Unix port runs with host privileges
- Embedded targets have different security models
- Test security boundaries appropriate to target environment

## Common Development Tasks

### Updating MicroPython Version
```bash
cd micropython
git fetch
git checkout [desired-version-tag]
cd ../
git add micropython
git commit -m "Update MicroPython to [version]"
```

### Debugging Protocol Issues
1. **Enable Verbose Output**: Build with `MICROPY_DEBUG_VERBOSE=1`
2. **Add Debug Prints**: Modify `./py/repl.c` temporarily
3. **Compare with pyboard.py**: Reference official Python implementation
4. **Use Hex Dumps**: Examine raw byte sequences

### Testing Protocol Changes
```bash
# Build with debug
cd ports/unix
make DEBUG=1

# Run specific tests
./build-standard/micropython -c "import sys; print(sys.implementation)"
```

## Integration Points with Belay.NET

### Subprocess Communication Testing
- Use unix port executable for `SubprocessDeviceCommunication`
- Verify protocol compatibility without physical hardware
- Performance baseline for communication overhead

### Protocol Validation
- Compare Belay.NET protocol implementation with reference
- Validate edge cases and error conditions
- Ensure compatibility across MicroPython versions

### Documentation Synchronization
- Keep protocol documentation in sync with MicroPython docs
- Reference official documentation in Belay.NET comments
- Maintain compatibility matrix for different versions

## Important Notes

⚠️ **Submodule Modifications**: Never commit modifications to the MicroPython submodule. Any changes should be made as patches or in separate branches.

⚠️ **Build Dependencies**: The unix port build requires development tools that may not be available in all environments.

⚠️ **Testing Limitations**: Unix port behavior may differ from embedded targets in timing, memory usage, and I/O characteristics.

⚠️ **Version Dependencies**: Always verify that the submodule version supports the protocol features being implemented.

This guide ensures that work within the MicroPython submodule is aligned with Belay.NET development goals while respecting the upstream project's structure and practices.