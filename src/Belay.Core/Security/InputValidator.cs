// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Security;

using System.Text.RegularExpressions;

/// <summary>
/// Provides input validation and sanitization for MicroPython code execution.
/// Protects against command injection, malicious code patterns, and unsafe operations.
/// </summary>
/// <remarks>
/// <para>
/// This validator focuses on defensive security for MicroPython device communication.
/// It identifies and blocks common injection attack patterns while allowing legitimate
/// Python code to execute safely.
/// </para>
/// <para>
/// Key protection areas:
/// <list type="bullet">
/// <item><description>Command injection via escaped quotes and shell metacharacters</description></item>
/// <item><description>File system manipulation outside of intended scope</description></item>
/// <item><description>Network access and subprocess execution</description></item>
/// <item><description>Binary data exfiltration and protocol manipulation</description></item>
/// <item><description>Resource exhaustion and infinite loops</description></item>
/// </list>
/// </para>
/// </remarks>
public static class InputValidator {
    
    /// <summary>
    /// Validation result containing both the validation outcome and security details.
    /// </summary>
    public readonly struct ValidationResult {
        /// <summary>
        /// Gets a value indicating whether the input passed validation.
        /// </summary>
        public bool IsValid { get; }
        
        /// <summary>
        /// Gets the reason for validation failure, or null if validation passed.
        /// </summary>
        public string? FailureReason { get; }
        
        /// <summary>
        /// Gets the security risk level associated with the input.
        /// </summary>
        public SecurityRiskLevel RiskLevel { get; }
        
        /// <summary>
        /// Gets additional details about security concerns found in the input.
        /// </summary>
        public IReadOnlyList<string> SecurityConcerns { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationResult"/> struct.
        /// </summary>
        /// <param name="isValid">Whether the validation passed.</param>
        /// <param name="failureReason">The reason for failure, if any.</param>
        /// <param name="riskLevel">The assessed security risk level.</param>
        /// <param name="securityConcerns">List of security concerns found.</param>
        public ValidationResult(bool isValid, string? failureReason = null, SecurityRiskLevel riskLevel = SecurityRiskLevel.Low, IReadOnlyList<string>? securityConcerns = null) {
            IsValid = isValid;
            FailureReason = failureReason;
            RiskLevel = riskLevel;
            SecurityConcerns = securityConcerns ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Security risk levels for input validation.
    /// </summary>
    public enum SecurityRiskLevel {
        /// <summary>
        /// Low risk - standard Python code with minimal security concerns.
        /// </summary>
        Low = 0,
        
        /// <summary>
        /// Medium risk - code contains potentially dangerous patterns but may be legitimate.
        /// </summary>
        Medium = 1,
        
        /// <summary>
        /// High risk - code contains dangerous patterns that should be blocked.
        /// </summary>
        High = 2,
        
        /// <summary>
        /// Critical risk - code contains clear injection or malicious patterns.
        /// </summary>
        Critical = 3
    }

    // Dangerous patterns that indicate potential injection or malicious activity
    private static readonly Regex[] HighRiskPatterns = {
        // Command injection patterns
        new(@"['""][^'""]*['""][^'""]*[;|&`$(){}]", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        
        // Shell command execution
        new(@"(os\.(system|popen|spawn)|subprocess\.|exec\s*\(|eval\s*\()", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        
        // File system manipulation outside normal scope
        new(@"(\.\.[\\/]|__file__|__import__|globals\s*\(\)|locals\s*\(\))", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        
        // Network and socket operations (often used for data exfiltration)
        new(@"(socket\.|urllib|requests|http|ftp|telnet)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        
        // Binary operations and encoding that could hide malicious content
        new(@"(\.encode\s*\(|\.decode\s*\(|base64|hex|binascii)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        
        // Memory or resource exhaustion patterns
        new(@"(\[\s*\]\s*\*\s*\d{6,}|range\s*\(\s*\d{6,})", RegexOptions.Compiled),
        
        // Protocol manipulation
        new(@"(\x[0-9a-fA-F]{2}|\\x[0-9a-fA-F]{2}|chr\s*\()", RegexOptions.Compiled)
    };

    // Medium risk patterns that are concerning but may have legitimate uses
    private static readonly Regex[] MediumRiskPatterns = {
        // Dynamic code execution
        new(@"(compile\s*\(|exec\s*\()", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        
        // File operations that could be misused
        new(@"(open\s*\([^)]*['""][^'""]*[/\\]|\.read\(\)|\.write\()", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        
        // Import operations
        new(@"(__import__\s*\(|import\s+[a-zA-Z_]\w*)", RegexOptions.Compiled),
        
        // Large data structures or loops
        new(@"(for\s+\w+\s+in\s+range\s*\(\s*\d{4,}|\[\s*\d+\s*\]\s*\*\s*\d{4,})", RegexOptions.Compiled),
        
        // String manipulation that could construct dangerous code
        new(@"(\.format\s*\(|%[sd]|f['""])", RegexOptions.Compiled)
    };

    // Patterns that are definitely not allowed in device code
    private static readonly Regex[] BlockedPatterns = {
        // Null bytes and control characters that could break protocol
        new(@"\\0|\\x00|\x00", RegexOptions.Compiled),
        
        // Raw REPL control sequences
        new(@"\\x0[1-5]|[\x01-\x05]", RegexOptions.Compiled),
        
        // Excessive string concatenation (potential buffer overflow)
        new(@"(\+\s*['""][^'""]{1000,}['""]|\*\s*\d{5,})", RegexOptions.Compiled),
        
        // Obvious injection attempts
        new(@"['""]\s*[;|&]\s*[^'""]*['""]\s*[+]", RegexOptions.Compiled)
    };

    // Known dangerous function/module names
    private static readonly HashSet<string> DangerousFunctions = new(StringComparer.OrdinalIgnoreCase) {
        "eval", "exec", "compile", "__import__", "globals", "locals", "vars", "dir",
        "getattr", "setattr", "delattr", "hasattr", "callable", "input", "raw_input"
    };

    private static readonly HashSet<string> DangerousModules = new(StringComparer.OrdinalIgnoreCase) {
        "os", "sys", "subprocess", "socket", "urllib", "urllib2", "http", "ftplib",
        "telnetlib", "smtplib", "poplib", "imaplib", "ssl", "hashlib", "hmac"
    };

    /// <summary>
    /// Validates Python code input for security risks and injection attempts.
    /// </summary>
    /// <param name="code">The Python code to validate.</param>
    /// <param name="allowFileOperations">Whether to allow file operations (default: false).</param>
    /// <param name="allowNetworking">Whether to allow networking operations (default: false).</param>
    /// <returns>A validation result indicating whether the code is safe to execute.</returns>
    /// <exception cref="ArgumentNullException">Thrown when code is null.</exception>
    public static ValidationResult ValidateCode(string code, bool allowFileOperations = false, bool allowNetworking = false) {
        return ValidateCode(code, new SecurityConfiguration {
            AllowFileOperations = allowFileOperations,
            AllowNetworking = allowNetworking
        });
    }

    /// <summary>
    /// Validates Python code input for security risks and injection attempts using a security configuration.
    /// </summary>
    /// <param name="code">The Python code to validate.</param>
    /// <param name="config">The security configuration to use for validation.</param>
    /// <returns>A validation result indicating whether the code is safe to execute.</returns>
    /// <exception cref="ArgumentNullException">Thrown when code or config is null.</exception>
    public static ValidationResult ValidateCode(string code, SecurityConfiguration config) {
        if (code == null) {
            throw new ArgumentNullException(nameof(code));
        }

        if (config == null) {
            throw new ArgumentNullException(nameof(config));
        }

        var concerns = new List<string>();
        var riskLevel = SecurityRiskLevel.Low;

        // Check length limits
        if (config.MaxCodeLength > 0 && code.Length > config.MaxCodeLength) {
            return new ValidationResult(false, $"Code length ({code.Length}) exceeds maximum allowed ({config.MaxCodeLength})", 
                SecurityRiskLevel.High, new[] { $"Excessively long code: {code.Length} characters" });
        }

        // Check for custom blocked patterns first
        foreach (var pattern in config.CustomBlockedPatterns) {
            try {
                var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                if (regex.IsMatch(code)) {
                    return new ValidationResult(false, $"Code contains custom blocked pattern: {pattern}", SecurityRiskLevel.Critical, 
                        new[] { "Contains custom security policy violation" });
                }
            }
            catch (ArgumentException) {
                // Invalid regex pattern - log but continue
                if (config.LogSecurityEvents) {
                    // Would need logger injection here, but for now just continue
                }
            }
        }

        // Check for custom allowed patterns - if any match, allow the code with reduced scrutiny
        bool hasCustomAllowPattern = false;
        foreach (var pattern in config.CustomAllowedPatterns) {
            try {
                var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                if (regex.IsMatch(code)) {
                    hasCustomAllowPattern = true;
                    break;
                }
            }
            catch (ArgumentException) {
                // Invalid regex pattern - ignore
            }
        }

        // Check for blocked patterns first (unless custom allow pattern matches)
        if (!hasCustomAllowPattern) {
            foreach (var pattern in BlockedPatterns) {
                if (pattern.IsMatch(code)) {
                    return new ValidationResult(false, $"Code contains blocked pattern: {pattern}", SecurityRiskLevel.Critical, 
                        new[] { "Contains control characters or protocol injection sequences" });
                }
            }
        }

        // Check for high risk patterns
        foreach (var pattern in HighRiskPatterns) {
            if (pattern.IsMatch(code)) {
                riskLevel = SecurityRiskLevel.High;
                concerns.Add($"High risk pattern detected: {pattern}");
            }
        }

        // Check for medium risk patterns
        foreach (var pattern in MediumRiskPatterns) {
            if (pattern.IsMatch(code)) {
                if (riskLevel < SecurityRiskLevel.Medium) {
                    riskLevel = SecurityRiskLevel.Medium;
                }
                concerns.Add($"Medium risk pattern detected: {pattern}");
            }
        }

        // Check for dangerous functions
        foreach (var func in DangerousFunctions) {
            if (code.Contains(func + "(", StringComparison.OrdinalIgnoreCase)) {
                riskLevel = SecurityRiskLevel.High;
                concerns.Add($"Dangerous function usage: {func}");
            }
        }

        // Check for dangerous modules
        foreach (var module in DangerousModules) {
            if (code.Contains("import " + module, StringComparison.OrdinalIgnoreCase) ||
                code.Contains("from " + module, StringComparison.OrdinalIgnoreCase) ||
                code.Contains(module + ".", StringComparison.OrdinalIgnoreCase)) {
                
                // Allow file operations if explicitly permitted
                if (module.Equals("os", StringComparison.OrdinalIgnoreCase) && config.AllowFileOperations) {
                    if (riskLevel < SecurityRiskLevel.Medium) {
                        riskLevel = SecurityRiskLevel.Medium;
                    }
                    concerns.Add($"File operations detected (allowed): {module}");
                    continue;
                }

                // Allow networking if explicitly permitted
                if (IsNetworkingModule(module) && config.AllowNetworking) {
                    if (riskLevel < SecurityRiskLevel.Medium) {
                        riskLevel = SecurityRiskLevel.Medium;
                    }
                    concerns.Add($"Networking operations detected (allowed): {module}");
                    continue;
                }

                riskLevel = SecurityRiskLevel.High;
                concerns.Add($"Dangerous module usage: {module}");
            }
        }

        // Count nested structures (potential complexity attack)
        if (config.MaxNestingLevel > 0) {
            var nestedLevel = CountMaxNestingLevel(code);
            if (nestedLevel > config.MaxNestingLevel) {
                if (config.ValidationLevel >= ValidationStrictness.Strict) {
                    return new ValidationResult(false, $"Code nesting level ({nestedLevel}) exceeds maximum allowed ({config.MaxNestingLevel})", 
                        SecurityRiskLevel.High, new[] { $"Deeply nested structures: {nestedLevel} levels" });
                } else {
                    riskLevel = SecurityRiskLevel.Medium;
                    concerns.Add($"Deeply nested structures: {nestedLevel} levels");
                }
            }
        }

        // Adjust risk threshold based on validation strictness
        var riskThreshold = config.ValidationLevel switch {
            ValidationStrictness.Relaxed => SecurityRiskLevel.Critical,
            ValidationStrictness.Standard => SecurityRiskLevel.High,
            ValidationStrictness.Strict => SecurityRiskLevel.Medium,
            ValidationStrictness.Maximum => SecurityRiskLevel.Low,
            _ => SecurityRiskLevel.High
        };

        // Apply additional restrictions for higher strictness levels
        if (config.ValidationLevel >= ValidationStrictness.Strict && !hasCustomAllowPattern) {
            // In strict mode, be more aggressive about blocking medium-risk patterns
            foreach (var pattern in MediumRiskPatterns) {
                if (pattern.IsMatch(code)) {
                    riskLevel = SecurityRiskLevel.High;
                    concerns.Add($"Strict mode: elevated risk for pattern: {pattern}");
                }
            }
        }

        // Final decision based on risk level and threshold
        bool isValid = riskLevel <= riskThreshold || hasCustomAllowPattern;
        string? failureReason = isValid ? null : $"Code failed security validation with {riskLevel} risk level (threshold: {riskThreshold})";

        return new ValidationResult(isValid, failureReason, riskLevel, concerns);
    }

    /// <summary>
    /// Sanitizes a string for safe use in Python string literals.
    /// Enhanced version of the existing EscapePythonString method.
    /// </summary>
    /// <param name="input">The string to sanitize.</param>
    /// <param name="useDoubleQuotes">Whether to escape for double quotes instead of single quotes.</param>
    /// <returns>The sanitized string safe for Python string literals.</returns>
    public static string SanitizePythonString(string? input, bool useDoubleQuotes = false) {
        if (string.IsNullOrEmpty(input)) {
            return input ?? string.Empty;
        }

        var result = input
            .Replace("\\", "\\\\")    // Escape backslashes first
            .Replace("\r", "\\r")     // Carriage return
            .Replace("\n", "\\n")     // Line feed
            .Replace("\t", "\\t")     // Tab
            .Replace("\b", "\\b")     // Backspace
            .Replace("\f", "\\f")     // Form feed
            .Replace("\v", "\\v")     // Vertical tab
            .Replace("\0", "\\0");    // Null character

        if (useDoubleQuotes) {
            result = result.Replace("\"", "\\\"");
        } else {
            result = result.Replace("'", "\\'");
        }

        // Remove any remaining control characters
        result = Regex.Replace(result, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

        return result;
    }

    /// <summary>
    /// Validates that a parameter name is safe for Python code generation.
    /// </summary>
    /// <param name="parameterName">The parameter name to validate.</param>
    /// <returns>True if the parameter name is safe, false otherwise.</returns>
    public static bool IsValidParameterName(string? parameterName) {
        if (string.IsNullOrEmpty(parameterName)) {
            return false;
        }

        // Python identifier rules: starts with letter or underscore, followed by letters, digits, or underscores
        if (!Regex.IsMatch(parameterName, @"^[a-zA-Z_][a-zA-Z0-9_]*$")) {
            return false;
        }

        // Check for Python reserved keywords
        var pythonKeywords = new HashSet<string> {
            "and", "as", "assert", "break", "class", "continue", "def", "del", "elif", "else",
            "except", "exec", "finally", "for", "from", "global", "if", "import", "in", "is",
            "lambda", "not", "or", "pass", "print", "raise", "return", "try", "while", "with",
            "yield", "True", "False", "None", "async", "await", "nonlocal"
        };

        return !pythonKeywords.Contains(parameterName);
    }

    /// <summary>
    /// Creates a safe Python code template with validated parameter substitution.
    /// </summary>
    /// <param name="template">The Python code template with {parameter} placeholders.</param>
    /// <param name="parameters">Dictionary of parameter names and values to substitute.</param>
    /// <returns>The safely constructed Python code.</returns>
    /// <exception cref="ArgumentException">Thrown when template or parameters are invalid.</exception>
    public static string CreateSafeCodeFromTemplate(string template, IReadOnlyDictionary<string, object?> parameters) {
        if (string.IsNullOrEmpty(template)) {
            throw new ArgumentException("Template cannot be null or empty", nameof(template));
        }

        var result = template;
        
        foreach (var kvp in parameters) {
            if (!IsValidParameterName(kvp.Key)) {
                throw new ArgumentException($"Invalid parameter name: {kvp.Key}", nameof(parameters));
            }

            var placeholder = "{" + kvp.Key + "}";
            var safeValue = ConvertToSafePythonLiteral(kvp.Value);
            result = result.Replace(placeholder, safeValue);
        }

        // Validate the final code
        var validation = ValidateCode(result);
        if (!validation.IsValid) {
            throw new ArgumentException($"Generated code failed security validation: {validation.FailureReason}", nameof(template));
        }

        return result;
    }

    /// <summary>
    /// Converts a .NET value to a safe Python literal representation.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>The safe Python literal representation.</returns>
    private static string ConvertToSafePythonLiteral(object? value) {
        return value switch {
            null => "None",
            bool b => b ? "True" : "False",
            string s => $"'{SanitizePythonString(s)}'",
            byte or sbyte or short or ushort or int or uint or long or ulong => value.ToString()!,
            float f => f.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => $"'{SanitizePythonString(value.ToString())}'"
        };
    }

    /// <summary>
    /// Checks if a module name is related to networking operations.
    /// </summary>
    /// <param name="moduleName">The module name to check.</param>
    /// <returns>True if the module is networking-related, false otherwise.</returns>
    private static bool IsNetworkingModule(string moduleName) {
        var networkingModules = new[] { "socket", "urllib", "urllib2", "http", "ftplib", "telnetlib", "smtplib", "poplib", "imaplib", "ssl" };
        return networkingModules.Contains(moduleName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Counts the maximum nesting level of brackets, parentheses, and braces in the code.
    /// </summary>
    /// <param name="code">The code to analyze.</param>
    /// <returns>The maximum nesting level found.</returns>
    private static int CountMaxNestingLevel(string code) {
        var level = 0;
        var maxLevel = 0;
        var inString = false;
        var stringChar = '\0';

        for (int i = 0; i < code.Length; i++) {
            var c = code[i];
            
            // Handle string literals
            if ((c == '"' || c == '\'') && (i == 0 || code[i - 1] != '\\')) {
                if (!inString) {
                    inString = true;
                    stringChar = c;
                } else if (c == stringChar) {
                    inString = false;
                }
                continue;
            }

            if (inString) continue;

            // Count nesting
            if (c == '(' || c == '[' || c == '{') {
                level++;
                maxLevel = Math.Max(maxLevel, level);
            } else if (c == ')' || c == ']' || c == '}') {
                level = Math.Max(0, level - 1);
            }
        }

        return maxLevel;
    }
}