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
- there is a RPI Pico micropython available for testing on tty: /dev/usb/tty-Board_in_FS_mode-e6614c311b7e6f35