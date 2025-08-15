# Issue 002-116: Input Validation and Command Injection Protection

**Epic**: 002 - Attribute-Based Programming Foundation  
**Type**: Security Enhancement  
**Priority**: HIGH  
**Severity**: Major  
**Sprint Assignment**: Sprint 7 (Post-Core Architecture)  
**Story Points**: 8  
**Risk Level**: HIGH - Security vulnerability in production IoT scenarios

## Summary

Implement comprehensive input validation and command injection protection across all device communication interfaces to prevent security vulnerabilities in production IoT and automation deployments.

## Background

Critical code review identified missing validation for device paths and Python code inputs throughout the codebase. The current `EscapePythonString` method requires security review, and there's no systematic approach to preventing command injection attacks when executing Python code on remote devices.

## Problem Statement

**Current State:**
- No validation for device connection strings (e.g., `serial:COM3; rm -rf /`)
- Missing sanitization for Python code strings before execution
- Incomplete escaping in `EscapePythonString` method (DeviceConnection.cs:511)
- No protection against malicious code injection in user-provided Python snippets
- File paths accepted without validation, risking directory traversal attacks

**Risks:**
- Command injection vulnerabilities in production IoT scenarios
- Potential for unauthorized code execution on connected devices
- Directory traversal attacks through unvalidated file paths
- Data exfiltration or device compromise in multi-tenant environments

## Technical Requirements

### Input Validation Framework
```csharp
public interface IInputValidator
{
    bool ValidateDevicePath(string path, out string sanitizedPath);
    bool ValidatePythonCode(string code, out ValidationResult result);
    string SanitizePythonString(string input);
    bool ValidateFilePath(string path, out string normalizedPath);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<SecurityIssue> Issues { get; set; }
    public string SanitizedCode { get; set; }
}
```

### Security Checks Required

1. **Device Path Validation**
   - Validate serial port names against OS-specific patterns
   - Reject paths with shell metacharacters
   - Whitelist allowed device path formats
   - Implement connection string parser with strict validation

2. **Python Code Sanitization**
   - Detect and block dangerous imports (os, subprocess, etc.)
   - Implement configurable security policies
   - Option for sandboxed execution mode
   - Escape special characters properly

3. **File Path Security**
   - Prevent directory traversal (../, ..\)
   - Normalize paths before validation
   - Implement root directory restrictions
   - Validate against allowed file extensions

## Implementation Plan

### Phase 1: Core Validation Infrastructure (2 days)
- Create `Belay.Core.Security` namespace
- Implement `IInputValidator` interface
- Create `DevicePathValidator` class
- Create `PythonCodeValidator` class
- Create `FilePathValidator` class

### Phase 2: Integration Points (2 days)
- Update `DeviceConnection.EscapePythonString` with security review
- Add validation to `Device.FromConnectionString`
- Add validation to all `ExecuteAsync` methods
- Add validation to file transfer operations

### Phase 3: Security Policies (1 day)
- Create `SecurityPolicy` configuration class
- Implement strict/permissive modes
- Add policy configuration to DI container
- Create default security policies

### Phase 4: Testing and Hardening (2 days)
- Create comprehensive security test suite
- Test with known injection patterns
- Fuzz testing for edge cases
- Security audit documentation

## Acceptance Criteria

### Functional Requirements
- [ ] All device paths validated before use
- [ ] Python code sanitized before execution
- [ ] File paths normalized and validated
- [ ] Security policies configurable via DI
- [ ] Backward compatibility maintained with warnings

### Security Requirements
- [ ] Block common injection patterns
- [ ] Prevent directory traversal attacks
- [ ] Sanitize all special characters
- [ ] Log security validation failures
- [ ] Support strict and permissive modes

### Testing Requirements
- [ ] Unit tests for all validators
- [ ] Integration tests with malicious inputs
- [ ] Fuzz testing for edge cases
- [ ] Performance impact <5ms per validation
- [ ] Security audit trail logging

## Dependencies

### Blocking Dependencies
- None - can be implemented independently

### Blocked By This Issue
- Issue 002-106: Cross-Component Integration (needs secure integration)
- Production deployment readiness

### Related Issues
- Issue 002-105: Unified Exception Handling (security exceptions)
- Issue 002-110: Testing Infrastructure (security testing)

## Definition of Done

- [ ] All validation classes implemented and tested
- [ ] Integration points updated with validation
- [ ] Security policies configurable and documented
- [ ] Performance benchmarks show <5ms overhead
- [ ] Security test suite achieving 100% coverage
- [ ] Documentation includes security best practices
- [ ] Code review by security-focused reviewer
- [ ] No high/critical security warnings from static analysis
- [ ] Migration guide for existing code

## Technical Design Details

### Validation Pipeline
```csharp
public class SecureDeviceConnection : IDeviceConnection
{
    private readonly IInputValidator _validator;
    private readonly SecurityPolicy _policy;
    
    public async Task<T> ExecuteAsync<T>(string code, CancellationToken token)
    {
        // Validation pipeline
        var validationResult = _validator.ValidatePythonCode(code);
        if (!validationResult.IsValid && _policy.Mode == SecurityMode.Strict)
        {
            throw new SecurityValidationException(validationResult.Issues);
        }
        
        // Log security events
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Security validation failed: {Issues}", 
                validationResult.Issues);
        }
        
        // Execute with sanitized code
        return await base.ExecuteAsync<T>(validationResult.SanitizedCode, token);
    }
}
```

### Example Security Policy
```csharp
public class SecurityPolicy
{
    public SecurityMode Mode { get; set; } = SecurityMode.Strict;
    public List<string> BlockedImports { get; set; } = new() 
    { 
        "os", "subprocess", "sys", "eval", "exec", "__import__"
    };
    public bool AllowFileSystemAccess { get; set; } = false;
    public bool AllowNetworkAccess { get; set; } = false;
    public int MaxCodeLength { get; set; } = 10000;
    public List<string> AllowedDevicePrefixes { get; set; } = new()
    {
        "serial:", "subprocess:", "tcp:", "websocket:"
    };
}
```

## Risk Mitigation

### Implementation Risks
- **Risk**: Breaking existing code with strict validation
- **Mitigation**: Implement permissive mode by default with migration warnings

- **Risk**: Performance impact on high-frequency operations
- **Mitigation**: Cache validation results, optimize regex patterns

- **Risk**: False positives blocking legitimate code
- **Mitigation**: Configurable policies, comprehensive testing

### Security Risks
- **Risk**: Incomplete validation patterns
- **Mitigation**: Use established security libraries, regular updates

- **Risk**: Bypass through encoding tricks
- **Mitigation**: Multiple validation layers, canonicalization

## Estimation Notes

**Story Points Breakdown:**
- Core validation infrastructure: 3 points
- Integration and refactoring: 2 points
- Security testing: 2 points
- Documentation and review: 1 point

**Complexity Factors:**
- Security expertise required
- Cross-platform considerations
- Performance optimization needed
- Backward compatibility maintenance

## Sprint Planning Recommendation

**Sprint 7 Placement Rationale:**
- After core architecture stabilization
- Before production deployment
- Allows security-focused sprint
- Can be developed in parallel with other improvements

**Team Requirements:**
- Developer with security experience
- Access to security scanning tools
- Code review from security expert
- Testing on multiple platforms

## Success Metrics

- Zero security vulnerabilities in penetration testing
- <5ms validation overhead per operation
- 100% of input paths validated
- No false positives in normal usage
- Clear security documentation for users