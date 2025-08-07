// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Testing;

using System.Diagnostics;
using Microsoft.Extensions.Logging;

/// <summary>
/// Helper for working with MicroPython unix port for testing.
/// </summary>
public class MicroPythonUnixPortLogger
{
}

/// <inheritdoc/>
public static class MicroPythonUnixPort
{
    private static readonly ILogger Logger =
        Microsoft.Extensions.Logging.Abstractions.NullLogger<MicroPythonUnixPortLogger>.Instance;

    /// <summary>
    /// Find the MicroPython unix port executable (synchronous wrapper).
    /// </summary>
    /// <returns></returns>
    public static string? FindMicroPythonExecutable()
    {
        try
        {
            // Direct approach: check common locations first
            string[] commonPaths =
            [
                "/home/corona/belay.net/micropython/ports/unix/build-standard/micropython", // Host absolute path
                "micropython/ports/unix/build-standard/micropython",            // Relative from project root
                "./micropython/ports/unix/build-standard/micropython",          // Relative current dir
                "micropython", // PATH lookup
            ];

            foreach (string? path in commonPaths)
            {
                Console.WriteLine($"[DEBUG] FindMicroPythonExecutable: Checking {path}");
                if (File.Exists(path))
                {
                    try
                    {
                        bool isValid = Task.Run(async () => await IsValidMicroPythonExecutableAsync(path)).GetAwaiter().GetResult();
                        if (isValid)
                        {
                            return Path.GetFullPath(path);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }

            // Fallback to the async method
            return Task.Run(async () => await FindExecutableAsync()).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Build the MicroPython unix port (synchronous wrapper).
    /// </summary>
    /// <returns></returns>
    public static string BuildUnixPort()
    {
        // First try to find an existing executable directly
        Console.WriteLine("[DEBUG] BuildUnixPort: Starting search for existing executable");
        string? existingPath = FindMicroPythonExecutable();
        Console.WriteLine($"[DEBUG] BuildUnixPort: FindMicroPythonExecutable returned: {existingPath}");
        if (!string.IsNullOrEmpty(existingPath))
        {
            Console.WriteLine($"[DEBUG] BuildUnixPort: Found existing executable at: {existingPath}");
            return existingPath;
        }

        string repoPath = FindMicroPythonRepoPath();
        string expectedPath = GetBuiltExecutablePath(repoPath);

        // Check if already built first
        if (File.Exists(expectedPath))
        {
            try
            {
                bool isValid = Task.Run(async () => await IsValidMicroPythonExecutableAsync(expectedPath)).GetAwaiter().GetResult();
                if (isValid)
                {
                    return expectedPath;
                }
            }
            catch
            {
                // Fall through to rebuild
            }
        }

        bool success = Task.Run(async () => await BuildUnixPortAsync(repoPath)).GetAwaiter().GetResult();
        if (success)
        {
            return GetBuiltExecutablePath(repoPath);
        }

        throw new InvalidOperationException("Failed to build MicroPython unix port");
    }

    /// <summary>
    /// Find the MicroPython repository path by searching up the directory tree.
    /// </summary>
    private static string FindMicroPythonRepoPath()
    {
        string currentDir = Environment.CurrentDirectory;
        Console.WriteLine($"[DEBUG] FindMicroPythonRepoPath: currentDir={currentDir}");

        string[] searchPaths =
        [
            Path.Combine(currentDir, "micropython"),
            Path.Combine(currentDir, "..", "micropython"),
            Path.Combine(currentDir, "..", "..", "micropython"),
            Path.Combine(currentDir, "..", "..", "..", "micropython"),
            Path.Combine(currentDir, "..", "..", "..", "..", "micropython"),
            Path.Combine(currentDir, "..", "..", "..", "..", "..", "micropython"),
            Path.Combine(currentDir, "..", "..", "..", "..", "..", "..", "micropython"),
        ];

        foreach (string? path in searchPaths)
        {
            string normalizedPath = Path.GetFullPath(path);
            Console.WriteLine($"[DEBUG] FindMicroPythonRepoPath: Checking {normalizedPath}");

            if (Directory.Exists(normalizedPath) &&
                Directory.Exists(Path.Combine(normalizedPath, "ports", "unix")))
                {
                Console.WriteLine($"[DEBUG] FindMicroPythonRepoPath: FOUND at {normalizedPath}");
                return normalizedPath;
            }
        }

        Console.WriteLine($"[DEBUG] FindMicroPythonRepoPath: NOT FOUND in any search path");
        throw new DirectoryNotFoundException("MicroPython repository not found. Please ensure the micropython submodule is initialized.");
    }

    /// <summary>
    /// Find the MicroPython unix port executable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<string> FindExecutableAsync()
    {
        var searchPaths = new List<string>
        {
            "micropython", // PATH lookup
            "/usr/local/bin/micropython",
            "/opt/micropython/bin/micropython",
        };

        // Add paths based on MicroPython repo location
        try
        {
            string repoPath = FindMicroPythonRepoPath();
            searchPaths.Add(GetBuiltExecutablePath(repoPath));
        }
        catch
        {
            // If repo not found, add some common relative paths as fallback
            searchPaths.Add(Path.Combine("micropython", "ports", "unix", "build-standard", "micropython"));
            searchPaths.Add("./micropython/ports/unix/build-standard/micropython");
        }

        foreach (string path in searchPaths)
        {
            if (await IsValidMicroPythonExecutableAsync(path))
            {
                Logger.LogDebug("Found MicroPython executable at: {Path}", path);
                return Path.GetFullPath(path);
            }
        }

        throw new FileNotFoundException("MicroPython unix port executable not found. " +
            "Please build the unix port or ensure it's available in PATH.");
    }

    /// <summary>
    /// Check if a path points to a valid MicroPython executable.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<bool> IsValidMicroPythonExecutableAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            // Test by running a simple command
            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "-c \"print('test')\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            string output = await process.StandardOutput.ReadToEndAsync();

            return process.ExitCode == 0 && output.Contains("test");
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to validate MicroPython executable: {Path}", path);
            return false;
        }
    }

    /// <summary>
    /// Build the MicroPython unix port if source is available.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<bool> BuildUnixPortAsync(
        string micropythonRepoPath,
        CancellationToken cancellationToken = default)
        {
        string unixPortPath = Path.Combine(micropythonRepoPath, "ports", "unix");

        Console.WriteLine($"[DEBUG] BuildUnixPortAsync: micropythonRepoPath={micropythonRepoPath}");
        Console.WriteLine($"[DEBUG] BuildUnixPortAsync: unixPortPath={unixPortPath}");
        Console.WriteLine($"[DEBUG] BuildUnixPortAsync: Directory.Exists(unixPortPath)={Directory.Exists(unixPortPath)}");

        if (!Directory.Exists(unixPortPath))
        {
            Logger.LogWarning("Unix port directory not found: {Path}", unixPortPath);
            return false;
        }

        try
        {
            Logger.LogInformation("Building MicroPython unix port...");

            // Run: make submodules
            bool submodulesResult = await RunMakeCommandAsync(unixPortPath, "submodules", cancellationToken);
            if (!submodulesResult)
            {
                Logger.LogError("Failed to initialize submodules for unix port build");
                return false;
            }

            // Run: make
            bool buildResult = await RunMakeCommandAsync(unixPortPath, string.Empty, cancellationToken);
            if (!buildResult)
            {
                Logger.LogError("Failed to build unix port");
                return false;
            }

            Logger.LogInformation("Successfully built MicroPython unix port");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error building MicroPython unix port");
            return false;
        }
    }

    /// <summary>
    /// Get the expected path to the built unix port executable.
    /// </summary>
    /// <returns></returns>
    public static string GetBuiltExecutablePath(string micropythonRepoPath)
    {
        return Path.Combine(micropythonRepoPath, "ports", "unix", "build-standard", "micropython");
    }

    private static async Task<bool> RunMakeCommandAsync(string workingDirectory, string arguments,
        CancellationToken cancellationToken)
        {
        var startInfo = new ProcessStartInfo
        {
            FileName = "make",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return false;
        }

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            string stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            Logger.LogError(
                "Make command failed with exit code {ExitCode}: {Error}",
                process.ExitCode, stderr);
        }

        return process.ExitCode == 0;
    }
}

/// <summary>
/// Test helper for subprocess communication.
/// </summary>
public static class SubprocessTestHelper
{
    /// <summary>
    /// Create a SubprocessDeviceCommunication instance for testing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<Communication.SubprocessDeviceCommunication> CreateTestDeviceAsync()
    {
        string executablePath = await MicroPythonUnixPort.FindExecutableAsync();
        var device = new Communication.SubprocessDeviceCommunication(executablePath);
        await device.StartAsync();
        return device;
    }

    /// <summary>
    /// Check if MicroPython unix port is available for testing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<bool> IsMicroPythonAvailableAsync()
    {
        try
        {
            await MicroPythonUnixPort.FindExecutableAsync();
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Get the MicroPython executable path from environment or default.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<string> GetMicroPythonExecutableAsync()
    {
        string? envPath = Environment.GetEnvironmentVariable("MICROPYTHON_EXECUTABLE");
        if (!string.IsNullOrEmpty(envPath) && await MicroPythonUnixPort.IsValidMicroPythonExecutableAsync(envPath))
        {
            return envPath;
        }

        return await MicroPythonUnixPort.FindExecutableAsync();
    }
}
