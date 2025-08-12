// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Tests.Infrastructure;

/// <summary>
/// Utility class for MicroPython unix port management in tests.
/// </summary>
public static class MicroPythonUnixPort
{
    /// <summary>
    /// Finds the MicroPython executable.
    /// </summary>
    /// <returns>Path to MicroPython executable or null if not found.</returns>
    public static string? FindMicroPythonExecutable()
    {
        var home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home))
            return null;

        var micropythonPath = Path.Combine(home, "belay.net", "micropython", "ports", "unix", "build-standard", "micropython");
        return File.Exists(micropythonPath) ? micropythonPath : null;
    }

    /// <summary>
    /// Builds the MicroPython unix port.
    /// </summary>
    public static void BuildUnixPort()
    {
        // In tests, we'll skip actual building
        // The CI environment should have pre-built MicroPython
    }
}