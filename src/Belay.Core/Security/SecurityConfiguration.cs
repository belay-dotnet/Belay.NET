// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Security;

/// <summary>
/// Configuration options for security validation and protection features in Belay.NET.
/// Controls the strictness of input validation, code injection protection, and other security measures.
/// </summary>
/// <remarks>
/// <para>
/// The security configuration allows fine-tuning the balance between security and functionality.
/// More restrictive settings provide better protection but may prevent legitimate use cases,
/// while more permissive settings enable broader functionality but with increased risk.
/// </para>
/// <para>
/// Configuration can be applied at the device level or globally, with device-specific settings
/// taking precedence over global settings.
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Basic Security Configuration</strong></para>
/// <code>
/// var config = new SecurityConfiguration {
///     ValidationLevel = ValidationStrictness.Strict,
///     AllowFileOperations = true,
///     AllowNetworking = false,
///     LogSecurityEvents = true
/// };
///
/// var device = Device.FromConnectionString("serial:COM3")
///     .WithSecurityConfiguration(config);
/// </code>
/// <para><strong>Development Environment (Relaxed)</strong></para>
/// <code>
/// var devConfig = SecurityConfiguration.ForDevelopment();
/// var device = Device.FromConnectionString("subprocess:micropython")
///     .WithSecurityConfiguration(devConfig);
/// </code>
/// <para><strong>Production Environment (Strict)</strong></para>
/// <code>
/// var prodConfig = SecurityConfiguration.ForProduction();
/// var device = Device.FromConnectionString("serial:/dev/ttyUSB0")
///     .WithSecurityConfiguration(prodConfig);
/// </code>
/// </example>
public class SecurityConfiguration {
    /// <summary>
    /// Gets or sets the level of strictness for input validation.
    /// </summary>
    /// <value>
    /// The validation strictness level. Default is <see cref="ValidationStrictness.Standard"/>.
    /// </value>
    /// <remarks>
    /// Higher strictness levels provide better security but may block legitimate code patterns.
    /// Lower strictness levels allow more flexibility but with increased security risk.
    /// </remarks>
    public ValidationStrictness ValidationLevel { get; set; } = ValidationStrictness.Standard;

    /// <summary>
    /// Gets or sets a value indicating whether file operations are allowed in executed code.
    /// </summary>
    /// <value>
    /// <c>true</c> if file operations should be allowed; otherwise, <c>false</c>.
    /// Default is <c>true</c> to support common device file management scenarios.
    /// </value>
    /// <remarks>
    /// When enabled, allows Python code to use file I/O operations, os module functions,
    /// and other file system related functionality. Disable for enhanced security in
    /// environments where file access is not required.
    /// </remarks>
    public bool AllowFileOperations { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether networking operations are allowed in executed code.
    /// </summary>
    /// <value>
    /// <c>true</c> if networking operations should be allowed; otherwise, <c>false</c>.
    /// Default is <c>false</c> for security reasons.
    /// </value>
    /// <remarks>
    /// When enabled, allows Python code to use socket operations, HTTP requests,
    /// and other network-related functionality. Enable only when network access
    /// is specifically required and the environment is trusted.
    /// </remarks>
    public bool AllowNetworking { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to log security-related events and warnings.
    /// </summary>
    /// <value>
    /// <c>true</c> if security events should be logged; otherwise, <c>false</c>.
    /// Default is <c>true</c> for security monitoring.
    /// </value>
    /// <remarks>
    /// When enabled, security validation failures, risk assessments, and other
    /// security-related events are logged. Useful for security monitoring and
    /// debugging validation issues.
    /// </remarks>
    public bool LogSecurityEvents { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum allowed length for Python code before it's considered a security risk.
    /// </summary>
    /// <value>
    /// The maximum code length in characters. Default is 50,000 characters.
    /// Set to 0 to disable length-based validation.
    /// </value>
    /// <remarks>
    /// Extremely long code can indicate potential denial-of-service attacks or
    /// code generation issues. This setting helps prevent resource exhaustion.
    /// </remarks>
    public int MaxCodeLength { get; set; } = 50000;

    /// <summary>
    /// Gets or sets the maximum allowed nesting level for code structures before triggering security warnings.
    /// </summary>
    /// <value>
    /// The maximum nesting level. Default is 20 levels.
    /// Set to 0 to disable nesting-based validation.
    /// </value>
    /// <remarks>
    /// Deeply nested code structures can indicate complexity attacks or poorly
    /// structured code that may cause parsing or execution issues.
    /// </remarks>
    public int MaxNestingLevel { get; set; } = 20;

    /// <summary>
    /// Gets or sets custom patterns that should be blocked in addition to the default security patterns.
    /// </summary>
    /// <value>
    /// A collection of regular expression patterns to block. Default is empty.
    /// </value>
    /// <remarks>
    /// Allows adding application-specific security patterns beyond the built-in
    /// protection. Patterns are evaluated as regular expressions and will cause
    /// validation to fail if they match the input code.
    /// </remarks>
    public IList<string> CustomBlockedPatterns { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets custom patterns that should be allowed even if they would normally be blocked.
    /// </summary>
    /// <value>
    /// A collection of regular expression patterns to allow. Default is empty.
    /// </value>
    /// <remarks>
    /// Allows creating exceptions to the standard security validation for specific
    /// patterns that are known to be safe in the application context. Use with caution
    /// as this can weaken security protections.
    /// </remarks>
    public IList<string> CustomAllowedPatterns { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets a value indicating whether parameter substitution in PythonCodeAttribute should be validated.
    /// </summary>
    /// <value>
    /// <c>true</c> if parameter substitution should be validated; otherwise, <c>false</c>.
    /// Default is <c>true</c> for security.
    /// </value>
    /// <remarks>
    /// When enabled, parameter values passed to PythonCodeAttribute templates are
    /// validated and sanitized before substitution. Disable only if you have
    /// alternative validation mechanisms in place.
    /// </remarks>
    public bool ValidateParameterSubstitution { get; set; } = true;

    /// <summary>
    /// Creates a security configuration suitable for development environments.
    /// </summary>
    /// <returns>A security configuration with relaxed settings for development.</returns>
    /// <remarks>
    /// Development configurations prioritize functionality over security to enable
    /// rapid prototyping and testing. Not suitable for production environments.
    /// </remarks>
    public static SecurityConfiguration ForDevelopment() {
        return new SecurityConfiguration {
            ValidationLevel = ValidationStrictness.Relaxed,
            AllowFileOperations = true,
            AllowNetworking = true,
            LogSecurityEvents = true,
            MaxCodeLength = 100000,
            MaxNestingLevel = 50,
        };
    }

    /// <summary>
    /// Creates a security configuration suitable for production environments.
    /// </summary>
    /// <returns>A security configuration with strict settings for production.</returns>
    /// <remarks>
    /// Production configurations prioritize security over convenience, with
    /// strict validation and minimal permissions. Recommended for deployed
    /// applications and untrusted environments.
    /// </remarks>
    public static SecurityConfiguration ForProduction() {
        return new SecurityConfiguration {
            ValidationLevel = ValidationStrictness.Strict,
            AllowFileOperations = false,
            AllowNetworking = false,
            LogSecurityEvents = true,
            MaxCodeLength = 10000,
            MaxNestingLevel = 10,
        };
    }

    /// <summary>
    /// Creates a security configuration suitable for testing environments.
    /// </summary>
    /// <returns>A security configuration balanced for automated testing.</returns>
    /// <remarks>
    /// Testing configurations balance security with the need to test various
    /// code patterns and scenarios. Suitable for CI/CD environments and
    /// automated test suites.
    /// </remarks>
    public static SecurityConfiguration ForTesting() {
        return new SecurityConfiguration {
            ValidationLevel = ValidationStrictness.Standard,
            AllowFileOperations = true,
            AllowNetworking = false,
            LogSecurityEvents = false, // Reduce noise in test logs
            MaxCodeLength = 25000,
            MaxNestingLevel = 15,
        };
    }

    /// <summary>
    /// Creates a copy of the current configuration.
    /// </summary>
    /// <returns>A new SecurityConfiguration instance with the same settings.</returns>
    public SecurityConfiguration Clone() {
        return new SecurityConfiguration {
            ValidationLevel = this.ValidationLevel,
            AllowFileOperations = this.AllowFileOperations,
            AllowNetworking = this.AllowNetworking,
            LogSecurityEvents = this.LogSecurityEvents,
            MaxCodeLength = this.MaxCodeLength,
            MaxNestingLevel = this.MaxNestingLevel,
            CustomBlockedPatterns = new List<string>(this.CustomBlockedPatterns),
            CustomAllowedPatterns = new List<string>(this.CustomAllowedPatterns),
            ValidateParameterSubstitution = this.ValidateParameterSubstitution,
        };
    }
}

/// <summary>
/// Defines the levels of strictness for security validation.
/// </summary>
public enum ValidationStrictness {
    /// <summary>
    /// Minimal validation - only blocks clearly malicious patterns.
    /// Allows most Python code to execute with basic safety checks.
    /// </summary>
    Relaxed = 0,

    /// <summary>
    /// Standard validation - blocks suspicious patterns and enforces basic security.
    /// Provides a good balance between security and functionality.
    /// </summary>
    Standard = 1,

    /// <summary>
    /// Strict validation - blocks potentially risky patterns and enforces strong security.
    /// May prevent some legitimate use cases but provides enhanced protection.
    /// </summary>
    Strict = 2,

    /// <summary>
    /// Maximum validation - blocks all but the most basic, safe operations.
    /// Suitable for high-security environments with limited functionality requirements.
    /// </summary>
    Maximum = 3,
}
