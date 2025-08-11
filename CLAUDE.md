# CLAUDE.md - Belay.NET Development Guide

This file provides guidance to Claude Code when working on the Belay.NET project - a C# library for seamless integration between .NET applications and MicroPython/CircuitPython devices.

## Project Overview

**Belay.NET** is a C# port of the Python Belay library that enables Windows applications to treat MicroPython devices as off-the-shelf hardware components. The library provides a clean, strongly-typed API for device communication, file synchronization, and remote code execution.

## Key Knowledge Areas

### Raw REPL Protocol Understanding
- **Raw Mode**: Entered via Ctrl-A (0x01), allows programmatic code execution
- **Raw-Paste Mode**: More advanced protocol with flow control for large code transfers
- **Protocol Sequence**: `\x05A\x01` initialization, window-size management, flow control
- **Exit Sequences**: Ctrl-B (0x02) exits raw mode, Ctrl-D (0x04) executes code
- **Implementation**: Must handle flow control bytes `\x01` (increase window) and `\x04` (end data)

### MicroPython Submodule
- Include micropython git submodule for reference and testing
- Unix port can be built for local testing: `./ports/unix/build-standard/micropython`
- Reference documentation: `./docs/reference/repl.rst`
- Used for subprocess-based testing without physical hardware
- **Important**: When working within the micropython submodule directory, reference `./CLAUDE_micropython.md` for MicroPython-specific development guidance

### Documentation Submodule
- The ./docs submodule _is_ the belay-dotnet.github.io repository so all updates to the docs website should be made directly there, comitted and pushed.

### Architecture Principles
- **Async-First**: All device communication uses Task-based async patterns
- **Strong Typing**: Generic return types with compile-time safety
- **DI Ready**: Full dependency injection support with IServiceCollection extensions
- **Cross-Platform**: .NET 6+ for Windows/Linux/macOS compatibility
- **Enterprise Ready**: Structured logging, configuration, health checks

## Documentation Requirements

### Documentation-Driven Development
Per Issue 006-003 (Documentation Completion Strategy), all feature development MUST include documentation updates:

1. **Identify Documentation Impact**: Before implementing any feature, identify all documentation pages that need updates
2. **Update Stub Pages**: Complete any related stub documentation pages as part of the same issue/PR
3. **Validate Examples**: Ensure all code examples in documentation compile and run correctly
4. **No New Stubs**: Avoid creating new stub pages unless absolutely necessary

### Documentation in Definition of Done
Every issue's Definition of Done must include:
- [ ] API documentation comments complete and accurate
- [ ] Related stub pages identified and updated
- [ ] Code samples validated and working
- [ ] Breaking changes documented if applicable

### Documentation-Issue Mapping
Key documentation pages tied to specific issues:
- `error-handling.md` → Issue 002-105
- `sensor-reading.md`, `aspnet-core.md`, `background-services.md` → Issue 002-106
- `multiple-devices.md` → Issue 002-107
- `custom-attributes.md` → Issue 002-109
- Hardware documentation → Hardware validation phase

## Documentation Placeholder Tracking

### Finding Placeholder Pages
Use this command to identify all placeholder pages that need completion:

```bash
# Find all placeholder pages with "Documentation in Progress" warning
grep -r "Documentation in Progress" docs/ --include="*.md" -l

# Get detailed status of each placeholder page  
grep -r -A2 -B1 "Documentation in Progress" docs/ --include="*.md"

# Count remaining placeholder pages
grep -r "Documentation in Progress" docs/ --include="*.md" -l | wc -l

# Find specific completion expectations
grep -r "Expected completion" docs/ --include="*.md" -A1 -B1
```

### Current Placeholder Status
**Last Updated**: 2025-08-08

**Total Placeholder Pages**: 17

**Examples Placeholder Pages (6):**
- `docs/examples/sensor-reading.md` → Issue 002-106
- `docs/examples/error-handling.md` → Issue 002-105  
- `docs/examples/aspnet-core.md` → Issue 002-106
- `docs/examples/background-services.md` → Issue 002-106
- `docs/examples/multiple-devices.md` → Issue 002-107
- `docs/examples/custom-attributes.md` → Issue 002-109

**Hardware Placeholder Pages (8):**
- `docs/hardware/compatibility.md` → Hardware validation phase
- `docs/hardware/connections.md` → Issue 002-106
- `docs/hardware/raspberry-pi-pico.md` → Hardware validation phase
- `docs/hardware/esp32.md` → Hardware validation phase
- `docs/hardware/pyboard.md` → Hardware validation phase
- `docs/hardware/circuitpython.md` → CircuitPython validation testing
- `docs/hardware/troubleshooting-connections.md` → Hardware validation phase
- `docs/hardware/troubleshooting-performance.md` → Issue 002-106

**Articles Placeholder Pages (3):**
- `docs/articles/device-programming.md` → Issue 002-106
- `docs/articles/attributes-reference.md` → Issue 002-106
- `docs/articles/hardware-testing.md` → Hardware validation phase

**Guide Placeholder Pages (2):**
- `docs/guide/configuration.md` → Issue 002-106
- `docs/guide/testing.md` → Issue 002-110

### Placeholder Completion Checklist
When completing a placeholder page:

1. **Remove the warning block**:
   ```markdown
   ::: warning Documentation in Progress
   This documentation is currently being developed...
   :::
   ```

2. **Replace "Coming Soon" section** with actual content

3. **Update code examples** from placeholder to working examples

4. **Verify all internal links** work correctly

5. **Test code examples** compile and execute

6. **Update this tracking section** in CLAUDE.md

### Quality Gates
- **Issue Completion**: Cannot close issue until linked documentation pages are complete
- **Release Gates**: Cannot release milestone with placeholder pages
- **PR Review**: Must verify documentation completion in code reviews
- **CI/CD**: Automated checks for placeholder content before deployment

### Documentation Debt Metrics
Track progress with these commands:

```bash
# Calculate completion percentage
TOTAL=17
CURRENT=$(grep -r "Documentation in Progress" docs/ --include="*.md" -l | wc -l)
COMPLETED=$((TOTAL - CURRENT))
PERCENTAGE=$((COMPLETED * 100 / TOTAL))
echo "Documentation completion: $COMPLETED/$TOTAL ($PERCENTAGE%)"

# Find oldest placeholders (by creation date)
find docs/ -name "*.md" -exec grep -l "Documentation in Progress" {} \; | xargs ls -lt

# Identify high-priority placeholders
grep -r "URGENT\|CRITICAL\|High Priority" docs/ --include="*.md" -l
```

## XML Documentation Standards

Based on comprehensive code review, Belay.NET follows strict XML documentation standards to ensure high-quality API documentation generation. The project maintains **98% documentation coverage** but requires consistency improvements.

### Required Standards for All Public Members

#### Essential Tags (Required)
- **`<summary>`** - Required for all public types and members - Brief, clear description
- **`<param>`** - Required for all method parameters - Describe purpose and constraints  
- **<returns>** - Required for all non-void methods - Describe return value and possible states
- **`<typeparam>`** - Required for all generic type parameters - Explain type constraints
- **`<exception>`** - Required for all thrown exceptions - Document when and why thrown

#### Quality Enhancement Tags (Strongly Recommended)
- **`<example>`** - **TARGET: 30% of public classes/methods** - Real-world, practical code samples
- **`<remarks>`** - Complex functionality, architectural notes, usage guidelines
- **`<seealso>`** - Cross-references to related types and methods

### Documentation Templates

#### Method Documentation Template
```csharp
/// <summary>
/// Brief description of what the method does and its primary purpose.
/// </summary>
/// <param name="paramName">Description of parameter, including constraints and expected values.</param>
/// <returns>Description of return value, including possible states or null conditions.</returns>
/// <exception cref="ArgumentException">Thrown when parameter is invalid.</exception>
/// <exception cref="InvalidOperationException">Thrown when object state prevents operation.</exception>
/// <remarks>
/// <para>
/// Additional context about the method's behavior, performance considerations,
/// or architectural significance.
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Basic Usage</strong></para>
/// <code>
/// // Realistic, working example code
/// var result = await SomeMethodAsync("example", CancellationToken.None);
/// Console.WriteLine($"Result: {result}");
/// </code>
/// </example>
```

#### Class Documentation Template  
```csharp
/// <summary>
/// Brief description of the class purpose and primary responsibility.
/// </summary>
/// <remarks>
/// <para>
/// Detailed explanation of the class's role in the system architecture,
/// key behaviors, and usage patterns.
/// </para>
/// <para>
/// Key features and capabilities:
/// <list type="bullet">
/// <item><description>Feature 1 with brief explanation</description></item>
/// <item><description>Feature 2 with brief explanation</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Basic Setup</strong></para>
/// <code>
/// using var device = new DeviceType(connectionString);
/// await device.ConnectAsync();
/// </code>
/// <para><strong>Advanced Usage</strong></para>
/// <code>
/// // More complex example showing real-world usage
/// </code>
/// </example>
```

#### Interface Documentation Template
```csharp
/// <summary>
/// Defines the contract for [specific functionality].
/// </summary>
/// <remarks>
/// <para>
/// This interface establishes the standard behavior for [use case].
/// Implementations should ensure [key requirements].
/// </para>
/// </remarks>
```

### Common Issues and Fixes

#### ❌ INCORRECT: Misuse of inheritdoc
```csharp
/// <inheritdoc/>  // Wrong - property doesn't inherit
public string Output { get; }
```

#### ✅ CORRECT: Proper property documentation  
```csharp
/// <summary>
/// Gets the output received from the MicroPython device.
/// </summary>
/// <value>
/// The raw string output, or empty string if no output received.
/// </value>
public string Output { get; }
```

#### ❌ INCORRECT: Missing parameter documentation
```csharp
/// <summary>Execute code on device.</summary>
public async Task<string> ExecuteAsync(string code, CancellationToken token)
```

#### ✅ CORRECT: Complete method documentation
```csharp
/// <summary>
/// Executes Python code on the connected MicroPython device.
/// </summary>
/// <param name="code">The Python code to execute on the device.</param>
/// <param name="token">Cancellation token for the operation.</param>
/// <returns>The output returned by the Python code execution.</returns>
/// <exception cref="InvalidOperationException">Thrown when device is not connected.</exception>
/// <exception cref="DeviceTimeoutException">Thrown when execution times out.</exception>
public async Task<string> ExecuteAsync(string code, CancellationToken token)
```

### Quality Metrics and Enforcement

- **Coverage Threshold**: 98% (currently achieved)
- **Example Target**: 30% of public classes/methods (currently 2%)
- **Deployment Blocking**: Enforced via CI/CD quality gates
- **Gold Standard**: Belay.Attributes assembly (65% with examples)

### High-Priority Documentation Debt

Critical files requiring immediate attention:
1. `SubprocessDeviceCommunication.cs` - Fix 15+ incorrectly used `<inheritdoc/>` tags
2. `IDeviceCommunication.cs` - Add missing parameter/return documentation  
3. `RawReplProtocol.cs` - Replace inheritdoc with proper property descriptions
4. Core executor interfaces - Add practical examples for key functionality
5. Service registration extensions - Add DI configuration examples

- there is a RPI Pico micropython available for testing on tty: /dev/usb/tty-Board_in_FS_mode-e6614c311b7e6f35