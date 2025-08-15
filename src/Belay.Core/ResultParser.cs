// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

using System.Text.Json;

/// <summary>
/// Simple result parser that replaces complex result mapping infrastructure.
/// Handles conversion from device output to strongly-typed results.
/// </summary>
public static class ResultParser {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parses device output as the specified type.
    /// </summary>
    /// <typeparam name="T">The type to parse the output as.</typeparam>
    /// <param name="deviceOutput">The raw output from the device.</param>
    /// <returns>The parsed result.</returns>
    /// <exception cref="DeviceException">Thrown when parsing fails.</exception>
    public static T ParseResult<T>(string deviceOutput) {
        // Handle string type directly
        if (typeof(T) == typeof(string)) {
            return (T)(object)deviceOutput;
        }

        // Handle empty/null output
        if (string.IsNullOrWhiteSpace(deviceOutput)) {
            return default(T)!;
        }

        // Clean up the output first
        var cleanOutput = CleanDeviceOutput(deviceOutput);

        try {
            // Handle primitive types
            if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal)) {
                return ParsePrimitive<T>(cleanOutput);
            }

            // Handle nullable primitive types
            var underlyingType = Nullable.GetUnderlyingType(typeof(T));
            if (underlyingType != null && underlyingType.IsPrimitive) {
                if (string.IsNullOrWhiteSpace(cleanOutput) || cleanOutput.Equals("None", StringComparison.OrdinalIgnoreCase)) {
                    return default(T)!;
                }

                return ParsePrimitive<T>(cleanOutput);
            }

            // Handle arrays
            if (typeof(T).IsArray) {
                return JsonSerializer.Deserialize<T>(cleanOutput, JsonOptions)!;
            }

            // Handle complex objects via JSON
            return JsonSerializer.Deserialize<T>(cleanOutput, JsonOptions)!;
        }
        catch (Exception ex) when (!(ex is DeviceException)) {
            throw new DeviceException(
                $"Failed to parse device output as {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    private static T ParsePrimitive<T>(string value) {
        var targetType = typeof(T);
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return underlyingType.Name switch {
            nameof(Boolean) => (T)(object)ParseBoolean(value),
            nameof(Int32) => (T)(object)int.Parse(value),
            nameof(Int64) => (T)(object)long.Parse(value),
            nameof(Single) => (T)(object)float.Parse(value),
            nameof(Double) => (T)(object)double.Parse(value),
            nameof(Decimal) => (T)(object)decimal.Parse(value),
            nameof(Byte) => (T)(object)byte.Parse(value),
            nameof(Int16) => (T)(object)short.Parse(value),
            nameof(UInt32) => (T)(object)uint.Parse(value),
            nameof(UInt64) => (T)(object)ulong.Parse(value),
            nameof(UInt16) => (T)(object)ushort.Parse(value),
            _ => throw new DeviceException($"Unsupported primitive type: {underlyingType.Name}"),
        };
    }

    private static bool ParseBoolean(string value) {
        return value.ToLowerInvariant() switch {
            "true" => true,
            "false" => false,
            "1" => true,
            "0" => false,
            "yes" => true,
            "no" => false,
            "on" => true,
            "off" => false,
            _ => bool.Parse(value),
        };
    }

    private static string CleanDeviceOutput(string output) {
        if (string.IsNullOrEmpty(output)) {
            return output;
        }

        // Remove common MicroPython REPL artifacts
        output = output.Trim();

        // Remove trailing >>> prompts
        if (output.EndsWith(">>>")) {
            output = output[..^3].Trim();
        }

        // Remove trailing ... prompts
        if (output.EndsWith("...")) {
            output = output[..^3].Trim();
        }

        // Handle Python None -> null
        if (output.Equals("None", StringComparison.OrdinalIgnoreCase)) {
            output = "null";
        }

        // Handle Python True/False -> true/false
        output = output.Replace("True", "true").Replace("False", "false");

        // Handle Python single quotes -> double quotes for JSON
        if (output.StartsWith('\'') && output.EndsWith('\'')) {
            output = $"\"{output[1..^1]}\"";
        }

        return output;
    }
}
