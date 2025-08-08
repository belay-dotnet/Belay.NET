// Copyright (c) 2024 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implementation of error mapping for device and system errors.
/// </summary>
internal class ErrorMapper : IErrorMapper {
    private readonly ILogger<ErrorMapper> logger;
    private readonly Dictionary<string, Func<string, string?, BelayException>> deviceErrorPatterns;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorMapper"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ErrorMapper(ILogger<ErrorMapper> logger) {
        this.logger = logger;
        this.deviceErrorPatterns = this.InitializeDeviceErrorPatterns();
    }

    /// <inheritdoc/>
    public BelayException MapException(Exception exception, string? context = null) {
        return exception switch {
            BelayException belayEx => this.EnrichWithContext(belayEx, context),
            TimeoutException timeoutEx => new DeviceTimeoutException("Operation timed out", TimeSpan.Zero, context),
            UnauthorizedAccessException authEx => new DeviceConnectionException($"Access denied: {authEx.Message}", context),
            InvalidOperationException invalidEx => new BelayConfigurationException($"Invalid operation: {invalidEx.Message}"),
            ArgumentException argEx => new BelayValidationException($"Invalid argument: {argEx.Message}", argEx.ParamName ?? "unknown"),
            _ => this.CreateGenericBelayException(exception),
        };
    }

    /// <inheritdoc/>
    public BelayException MapDeviceError(string deviceOutput, string? executedCode = null) {
        if (string.IsNullOrEmpty(deviceOutput)) {
            return new DeviceExecutionException("Device returned empty error response", executedCode);
        }

        // Parse common MicroPython error patterns
        foreach (var pattern in this.deviceErrorPatterns) {
            if (deviceOutput.Contains(pattern.Key, StringComparison.OrdinalIgnoreCase)) {
                var lineNumber = this.ExtractLineNumber(deviceOutput);
                var exception = pattern.Value(deviceOutput, executedCode);

                if (exception is DeviceExecutionException execEx && lineNumber.HasValue) {
                    return new DeviceExecutionException(execEx.Message, execEx.Code, deviceOutput, lineNumber);
                }

                return exception;
            }
        }

        // Default mapping for unknown device errors
        return new DeviceExecutionException($"Device error: {deviceOutput}", executedCode, deviceOutput);
    }

    /// <inheritdoc/>
    public T EnrichException<T>(T exception, Dictionary<string, object> context)
        where T : BelayException {
        exception.WithContext(context);
        return exception;
    }

    private Dictionary<string, Func<string, string?, BelayException>> InitializeDeviceErrorPatterns() {
        return new Dictionary<string, Func<string, string?, BelayException>>(StringComparer.OrdinalIgnoreCase) {
            ["SyntaxError"] = (output, code) => new DeviceCodeSyntaxException($"Syntax error in device code: {this.ExtractErrorMessage(output)}", code ?? string.Empty),
            ["MemoryError"] = (output, code) => new DeviceMemoryException($"Device out of memory: {this.ExtractErrorMessage(output)}"),
            ["OSError"] = (output, code) => new DeviceExecutionException($"Device OS error: {this.ExtractErrorMessage(output)}", code, output),
            ["ImportError"] = (output, code) => new DeviceExecutionException($"Module import failed: {this.ExtractErrorMessage(output)}", code, output),
            ["AttributeError"] = (output, code) => new DeviceExecutionException($"Attribute error: {this.ExtractErrorMessage(output)}", code, output),
            ["NameError"] = (output, code) => new DeviceExecutionException($"Name error: {this.ExtractErrorMessage(output)}", code, output),
            ["ValueError"] = (output, code) => new DeviceExecutionException($"Value error: {this.ExtractErrorMessage(output)}", code, output),
            ["TypeError"] = (output, code) => new DeviceExecutionException($"Type error: {this.ExtractErrorMessage(output)}", code, output),
            ["KeyError"] = (output, code) => new DeviceExecutionException($"Key error: {this.ExtractErrorMessage(output)}", code, output),
            ["IndexError"] = (output, code) => new DeviceExecutionException($"Index error: {this.ExtractErrorMessage(output)}", code, output),
            ["ZeroDivisionError"] = (output, code) => new DeviceExecutionException($"Division by zero: {this.ExtractErrorMessage(output)}", code, output),
            ["RuntimeError"] = (output, code) => new DeviceExecutionException($"Runtime error: {this.ExtractErrorMessage(output)}", code, output),
            ["KeyboardInterrupt"] = (output, code) => new DeviceExecutionException($"Operation was interrupted: {this.ExtractErrorMessage(output)}", code, output),
            ["SystemExit"] = (output, code) => new DeviceExecutionException($"System exit: {this.ExtractErrorMessage(output)}", code, output),
        };
    }

    private BelayException EnrichWithContext(BelayException exception, string? context) {
        if (!string.IsNullOrEmpty(context)) {
            exception.WithContext("operation_context", context);
        }

        return exception;
    }

    private string ExtractErrorMessage(string deviceOutput) {
        // Parse MicroPython error format to extract clean error message
        var lines = deviceOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Look for the actual error message (usually the last non-empty line)
        for (int i = lines.Length - 1; i >= 0; i--) {
            var line = lines[i].Trim();
            if (!string.IsNullOrEmpty(line) && !line.StartsWith("Traceback")) {
                return line;
            }
        }

        return deviceOutput.Trim();
    }

    private int? ExtractLineNumber(string deviceOutput) {
        // Extract line number from MicroPython traceback
        var match = Regex.Match(deviceOutput, @"line (\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private BelayException CreateGenericBelayException(Exception exception) {
        return new DeviceExecutionException($"Unexpected error: {exception.Message}", exception, code: null, deviceStackTrace: exception.StackTrace);
    }
}

/// <summary>
/// Implementation of exception enrichment services.
/// </summary>
internal class ExceptionEnricher : IExceptionEnricher {
    private readonly ILogger<ExceptionEnricher> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExceptionEnricher"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ExceptionEnricher(ILogger<ExceptionEnricher> logger) {
        this.logger = logger;
    }

    /// <inheritdoc/>
    public T Enrich<T>(T exception, string? component = null, Dictionary<string, object>? additionalContext = null)
        where T : Exception {
        if (exception is BelayException belayEx) {
            if (!string.IsNullOrEmpty(component)) {
                belayEx.WithContext("component", component);
            }

            if (additionalContext != null) {
                belayEx.WithContext(additionalContext);
            }
        }

        this.logger.LogError(exception, "Exception enriched in component {Component}", component ?? "Unknown");
        return exception;
    }

    /// <inheritdoc/>
    public T EnrichWithDeviceContext<T>(T exception, string? deviceType = null, string? firmwareVersion = null, string? sessionId = null)
        where T : Exception {
        if (exception is BelayException belayEx) {
            if (!string.IsNullOrEmpty(deviceType)) {
                belayEx.WithContext("device_type", deviceType);
            }

            if (!string.IsNullOrEmpty(firmwareVersion)) {
                belayEx.WithContext("firmware_version", firmwareVersion);
            }

            if (!string.IsNullOrEmpty(sessionId)) {
                belayEx.WithContext("session_id", sessionId);
            }
        }

        return exception;
    }
}
