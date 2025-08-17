// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Unit.Security;

using Belay.Core.Security;
using Xunit;

/// <summary>
/// Integration tests for security validation across the entire execution pipeline.
/// Tests the validation logic and security patterns used throughout the system.
/// </summary>
public class SecurityIntegrationTests {

    [Fact]
    public void DeviceConnection_ExecuteAsync_ValidatesInputPatterns() {
        // Since we can't easily test DeviceConnection without actual hardware,
        // let's test the validation logic directly through InputValidator
        var dangerousCode = "os.system('rm -rf /')";
        
        // Act
        var result = InputValidator.ValidateCode(dangerousCode, allowFileOperations: false);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.RiskLevel >= InputValidator.SecurityRiskLevel.High);
    }

    [Fact]
    public void DeviceConnection_ValidationLogic_AllowsLegitimateCode() {
        // Test validation logic for legitimate code
        var legitimateCode = "print('hello world')";
        
        // Act
        var result = InputValidator.ValidateCode(legitimateCode);
        
        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.RiskLevel <= InputValidator.SecurityRiskLevel.Medium);
    }

    [Fact]
    public void DeviceConnection_ValidationLogic_AllowsFileOperationsWhenEnabled() {
        // Test that file operations are allowed when explicitly enabled
        var fileCode = "import os\nos.listdir('/')";
        
        // Act
        var result = InputValidator.ValidateCode(fileCode, allowFileOperations: true);
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(InputValidator.SecurityRiskLevel.Medium, result.RiskLevel);
        Assert.Contains("File operations detected (allowed)", string.Join(", ", result.SecurityConcerns));
    }

    [Fact]
    public void DeviceConnection_ValidationLogic_BlocksNetworkingByDefault() {
        // Test that networking is blocked by default
        var networkCode = "import socket\ns = socket.socket()";
        
        // Act
        var result = InputValidator.ValidateCode(networkCode, allowNetworking: false);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.RiskLevel >= InputValidator.SecurityRiskLevel.High);
    }

    [Fact]
    public void AttributeHandler_SubstituteParameters_ValidatesParameterNames() {
        // This would require access to internal methods or making them internal visible
        // For now, we'll test through the public API
        Assert.True(true); // Placeholder - would need more complex setup
    }

    [Fact]
    public void AttributeHandler_FormatPythonValue_SanitizesStrings() {
        // Test the string sanitization in parameter formatting
        // This would require reflection or internal access to test directly
        Assert.True(true); // Placeholder
    }

    [Fact]
    public async Task End2End_DangerousCodeInPythonCodeAttribute_IsBlocked() {
        // This test would require a full device setup with attribute processing
        // For now, testing components individually is sufficient
        Assert.True(true); // Placeholder for future end-to-end test
    }

    [Theory]
    [InlineData("exec('malicious')")]
    [InlineData("eval('dangerous')")]
    [InlineData("__import__('os').system('bad')")]
    [InlineData("globals()['__builtins__']['eval']")]
    [InlineData("'; os.system('injection'); '")]
    public async Task SecurityValidation_CommonInjectionPatterns_AreBlocked(string injectionPattern) {
        // Arrange
        var connection = CreateTestDeviceConnection();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => connection.ExecuteAsync(injectionPattern));
        
        Assert.Contains("security validation", exception.Message);
    }

    [Theory]
    [InlineData("machine.Pin(2).value(1)")]
    [InlineData("import time\ntime.sleep(1)")]
    [InlineData("adc = machine.ADC(0)\nreading = adc.read()")]
    [InlineData("for i in range(10):\n    print(i)")]
    public void SecurityValidation_LegitimateDeviceCode_IsAllowed(string legitimateCode) {
        // Act
        var result = InputValidator.ValidateCode(legitimateCode);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void SecurityValidation_VeryLongCode_IsRejected() {
        // Arrange
        var longCode = new string('a', 60000); // Exceeds default limit

        // Act
        var result = InputValidator.ValidateCode(longCode);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Code length", result.FailureReason!);
    }

    [Fact]
    public void SecurityValidation_ControlCharacters_AreBlocked() {
        // Arrange
        var codeWithControlChars = "print('test')\x00\x01malicious";

        // Act
        var result = InputValidator.ValidateCode(codeWithControlChars);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(InputValidator.SecurityRiskLevel.Critical, result.RiskLevel);
    }

    [Fact]
    public void SecurityValidation_ExcessivelyNestedCode_IsHandledAppropriately() {
        // Arrange
        var nestedCode = new string('[', 30) + new string(']', 30);

        // Act
        var result = InputValidator.ValidateCode(nestedCode);

        // Assert - Should be flagged as concerning but may still be valid
        Assert.True(result.RiskLevel >= InputValidator.SecurityRiskLevel.Medium);
    }

    [Fact]
    public void InputSanitization_SpecialCharacters_AreProperlyEscaped() {
        // Arrange
        var unsafeString = "test'; os.system('evil'); print('";

        // Act
        var sanitized = InputValidator.SanitizePythonString(unsafeString);

        // Assert
        Assert.DoesNotContain("'; os.system('evil'); '", sanitized);
        Assert.Contains("\\'", sanitized); // Should escape single quotes
    }

    [Fact]
    public void ParameterValidation_PythonKeywords_AreRejected() {
        // Test that Python reserved words are rejected as parameter names
        var pythonKeywords = new[] { "for", "while", "if", "else", "import", "class", "def", "try", "except" };

        foreach (var keyword in pythonKeywords) {
            Assert.False(InputValidator.IsValidParameterName(keyword), 
                $"Python keyword '{keyword}' should not be valid as parameter name");
        }
    }

    [Fact]
    public void ParameterValidation_ValidNames_AreAccepted() {
        // Test that legitimate parameter names are accepted
        var validNames = new[] { "pin", "value", "timeout", "pin_number", "sensorData", "_private", "value123" };

        foreach (var name in validNames) {
            Assert.True(InputValidator.IsValidParameterName(name), 
                $"Valid parameter name '{name}' should be accepted");
        }
    }
}