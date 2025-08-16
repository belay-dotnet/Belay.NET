// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit.Security;

using Belay.Core.Security;
using Xunit;

/// <summary>
/// Unit tests for the InputValidator class, covering security validation and injection protection.
/// Tests both positive cases (legitimate code) and negative cases (malicious/dangerous patterns).
/// </summary>
public class InputValidatorTests {
    
    [Fact]
    public void ValidateCode_NullInput_ThrowsArgumentNullException() {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => InputValidator.ValidateCode(null!));
    }

    [Fact]
    public void ValidateCode_EmptyInput_IsValid() {
        // Act
        var result = InputValidator.ValidateCode("");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(InputValidator.SecurityRiskLevel.Low, result.RiskLevel);
    }

    [Fact]
    public void ValidateCode_SimpleValidCode_IsValid() {
        // Arrange
        var code = @"
import machine
led = machine.Pin(2, machine.Pin.OUT)
led.value(1)
";

        // Act
        var result = InputValidator.ValidateCode(code);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(InputValidator.SecurityRiskLevel.Medium, result.RiskLevel); // Medium due to import
    }

    [Theory]
    [InlineData("os.system('rm -rf /')")]
    [InlineData("subprocess.call(['rm', '-rf', '/'])")]
    [InlineData("exec('malicious code')")]
    [InlineData("eval('dangerous_expression')")]
    [InlineData("__import__('os').system('bad')")]
    public void ValidateCode_DangerousPatterns_IsInvalid(string dangerousCode) {
        // Act
        var result = InputValidator.ValidateCode(dangerousCode);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.RiskLevel >= InputValidator.SecurityRiskLevel.High);
    }

    [Theory]
    [InlineData("\x00null byte")]
    [InlineData("test\x01control char")]
    [InlineData("'test' ; echo 'injection'")]
    [InlineData("\\x00")]
    [InlineData("\\x01\\x02\\x03")]
    public void ValidateCode_BlockedPatterns_IsInvalid(string blockedCode) {
        // Act
        var result = InputValidator.ValidateCode(blockedCode);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(InputValidator.SecurityRiskLevel.Critical, result.RiskLevel);
    }

    [Fact]
    public void ValidateCode_FileOperationsDisallowed_IsInvalid() {
        // Arrange
        var code = "import os\nos.listdir('/')";

        // Act
        var result = InputValidator.ValidateCode(code, allowFileOperations: false);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.RiskLevel >= InputValidator.SecurityRiskLevel.High);
    }

    [Fact]
    public void ValidateCode_FileOperationsAllowed_IsValid() {
        // Arrange
        var code = "import os\nos.listdir('/')";

        // Act
        var result = InputValidator.ValidateCode(code, allowFileOperations: true);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(InputValidator.SecurityRiskLevel.Medium, result.RiskLevel);
        Assert.Contains("File operations detected (allowed)", string.Join(", ", result.SecurityConcerns));
    }

    [Fact]
    public void ValidateCode_NetworkingDisallowed_IsInvalid() {
        // Arrange
        var code = "import socket\ns = socket.socket()";

        // Act
        var result = InputValidator.ValidateCode(code, allowNetworking: false);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.RiskLevel >= InputValidator.SecurityRiskLevel.High);
    }

    [Fact]
    public void ValidateCode_NetworkingAllowed_IsValid() {
        // Arrange
        var code = "import socket\ns = socket.socket()";

        // Act
        var result = InputValidator.ValidateCode(code, allowNetworking: true);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(InputValidator.SecurityRiskLevel.Medium, result.RiskLevel);
        Assert.Contains("Networking operations detected (allowed)", string.Join(", ", result.SecurityConcerns));
    }

    [Fact]
    public void ValidateCode_ExcessiveLength_IsInvalid() {
        // Arrange
        var longCode = new string('a', 60000);

        // Act
        var result = InputValidator.ValidateCode(longCode);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.RiskLevel >= InputValidator.SecurityRiskLevel.High);
        Assert.Contains("Excessively long code", result.FailureReason!);
    }

    [Fact]
    public void ValidateCode_DeeplyNestedStructures_HasMediumRisk() {
        // Arrange
        var nestedCode = new string('[', 25) + new string(']', 25);

        // Act
        var result = InputValidator.ValidateCode(nestedCode);

        // Assert
        Assert.True(result.IsValid); // Still valid but flagged as concerning
        Assert.True(result.RiskLevel >= InputValidator.SecurityRiskLevel.Medium);
        Assert.Contains("Deeply nested structures", string.Join(", ", result.SecurityConcerns));
    }

    [Theory]
    [InlineData("print('hello')")]
    [InlineData("x = 42\ny = x * 2")]
    [InlineData("for i in range(10):\n    print(i)")]
    [InlineData("def simple_function():\n    return 'safe'")]
    public void ValidateCode_LegitimateCode_IsValid(string legitimateCode) {
        // Act
        var result = InputValidator.ValidateCode(legitimateCode);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.RiskLevel <= InputValidator.SecurityRiskLevel.Medium);
    }

    [Fact]
    public void SanitizePythonString_HandlesSpecialCharacters() {
        // Arrange
        var input = "test'string\"with\r\n\t\\special\x00chars";

        // Act
        var result = InputValidator.SanitizePythonString(input);

        // Assert
        Assert.DoesNotContain("\x00", result);
        Assert.Contains("\\'", result);
        Assert.Contains("\\r", result);
        Assert.Contains("\\n", result);
        Assert.Contains("\\t", result);
        Assert.Contains("\\\\", result);
    }

    [Theory]
    [InlineData("validName")]
    [InlineData("_underscore")]
    [InlineData("camelCase")]
    [InlineData("snake_case")]
    [InlineData("name123")]
    public void IsValidParameterName_ValidNames_ReturnsTrue(string name) {
        // Act & Assert
        Assert.True(InputValidator.IsValidParameterName(name));
    }

    [Theory]
    [InlineData("123invalid")]
    [InlineData("invalid-name")]
    [InlineData("invalid.name")]
    [InlineData("for")]
    [InlineData("import")]
    [InlineData("class")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValidParameterName_InvalidNames_ReturnsFalse(string? name) {
        // Act & Assert
        Assert.False(InputValidator.IsValidParameterName(name));
    }

    [Fact]
    public void CreateSafeCodeFromTemplate_ValidTemplate_ReturnsSecureCode() {
        // Arrange
        var template = "machine.Pin({pin}).value({state})";
        var parameters = new Dictionary<string, object?> {
            ["pin"] = 2,
            ["state"] = true
        };

        // Act
        var result = InputValidator.CreateSafeCodeFromTemplate(template, parameters);

        // Assert
        Assert.Equal("machine.Pin(2).value(True)", result);
    }

    [Fact]
    public void CreateSafeCodeFromTemplate_InvalidParameterName_ThrowsArgumentException() {
        // Arrange
        var template = "test {invalid-param}";
        var parameters = new Dictionary<string, object?> {
            ["invalid-param"] = "value"
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            InputValidator.CreateSafeCodeFromTemplate(template, parameters));
    }

    [Fact]
    public void CreateSafeCodeFromTemplate_MaliciousTemplate_ThrowsArgumentException() {
        // Arrange
        var template = "os.system('{command}')";
        var parameters = new Dictionary<string, object?> {
            ["command"] = "safe_command"
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            InputValidator.CreateSafeCodeFromTemplate(template, parameters));
    }

    [Fact]
    public void CreateSafeCodeFromTemplate_InjectionAttempt_IsSanitized() {
        // Arrange
        var template = "print('{message}')";
        var parameters = new Dictionary<string, object?> {
            ["message"] = "'; os.system('evil'); '"
        };

        // Act
        var result = InputValidator.CreateSafeCodeFromTemplate(template, parameters);

        // Assert
        Assert.Equal("print('\\'; os.system(\\'evil\\'); \\')", result);
        Assert.DoesNotContain("os.system('evil')", result);
    }

    [Fact]
    public void ValidateCode_WithSecurityConfiguration_RespectsSettings() {
        // Arrange
        var dangerousCode = "import os\nos.system('test')";
        var relaxedConfig = new SecurityConfiguration {
            ValidationLevel = ValidationStrictness.Relaxed,
            AllowFileOperations = true
        };

        // Act
        var result = InputValidator.ValidateCode(dangerousCode, relaxedConfig);

        // Assert - Should still be invalid due to os.system, but different handling
        Assert.False(result.IsValid);
        Assert.True(result.RiskLevel >= InputValidator.SecurityRiskLevel.High);
    }

    [Fact]
    public void ValidateCode_CustomBlockedPattern_IsBlocked() {
        // Arrange
        var code = "custom_dangerous_function()";
        var config = new SecurityConfiguration();
        config.CustomBlockedPatterns.Add("custom_dangerous_function");

        // Act
        var result = InputValidator.ValidateCode(code, config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(InputValidator.SecurityRiskLevel.Critical, result.RiskLevel);
    }

    [Fact]
    public void ValidateCode_CustomAllowedPattern_IsAllowed() {
        // Arrange
        var code = "os.system('trusted_operation')";
        var config = new SecurityConfiguration();
        config.CustomAllowedPatterns.Add("trusted_operation");

        // Act
        var result = InputValidator.ValidateCode(code, config);

        // Assert
        Assert.True(result.IsValid); // Should be allowed due to custom pattern
    }

    [Fact]
    public void ValidateCode_StrictMode_RejectsMorePatterns() {
        // Arrange
        var code = "compile('code', 'file', 'exec')";
        var standardConfig = new SecurityConfiguration { ValidationLevel = ValidationStrictness.Standard };
        var strictConfig = new SecurityConfiguration { ValidationLevel = ValidationStrictness.Strict };

        // Act
        var standardResult = InputValidator.ValidateCode(code, standardConfig);
        var strictResult = InputValidator.ValidateCode(code, strictConfig);

        // Assert
        Assert.True(standardResult.IsValid); // May be allowed in standard mode
        Assert.False(strictResult.IsValid); // Should be blocked in strict mode
    }

    [Fact]
    public void ValidateCode_MaximumStrictness_RejectsAlmostEverything() {
        // Arrange
        var code = "import machine\nled = machine.Pin(2)";
        var config = new SecurityConfiguration { ValidationLevel = ValidationStrictness.Maximum };

        // Act
        var result = InputValidator.ValidateCode(code, config);

        // Assert
        Assert.False(result.IsValid); // Should be blocked in maximum strictness
    }

    [Fact]
    public void SecurityConfiguration_ForDevelopment_HasRelaxedSettings() {
        // Act
        var config = SecurityConfiguration.ForDevelopment();

        // Assert
        Assert.Equal(ValidationStrictness.Relaxed, config.ValidationLevel);
        Assert.True(config.AllowFileOperations);
        Assert.True(config.AllowNetworking);
    }

    [Fact]
    public void SecurityConfiguration_ForProduction_HasStrictSettings() {
        // Act
        var config = SecurityConfiguration.ForProduction();

        // Assert
        Assert.Equal(ValidationStrictness.Strict, config.ValidationLevel);
        Assert.False(config.AllowFileOperations);
        Assert.False(config.AllowNetworking);
    }

    [Fact]
    public void SecurityConfiguration_Clone_CreatesIndependentCopy() {
        // Arrange
        var original = SecurityConfiguration.ForDevelopment();
        original.CustomBlockedPatterns.Add("test");

        // Act
        var clone = original.Clone();
        clone.ValidationLevel = ValidationStrictness.Strict;
        clone.CustomBlockedPatterns.Add("different");

        // Assert
        Assert.NotEqual(original.ValidationLevel, clone.ValidationLevel);
        Assert.Single(original.CustomBlockedPatterns);
        Assert.Equal(2, clone.CustomBlockedPatterns.Count);
    }
}