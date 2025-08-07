// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Discovery;

using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Belay.Core.Communication;
using Microsoft.Extensions.Logging;

/// <summary>
/// Information about a discovered MicroPython device.
/// </summary>
public class DeviceInfo
{
    /// <summary>
    /// Gets serial port name (e.g., COM3, /dev/ttyUSB0).
    /// </summary>
    public required string PortName { get; init; }

    /// <summary>
    /// Gets connection string for device factory.
    /// </summary>
    public string ConnectionString => $"serial:{this.PortName}";

    /// <summary>
    /// Gets microPython implementation name (micropython or circuitpython).
    /// </summary>
    public string? Implementation { get; init; }

    /// <summary>
    /// Gets microPython version information.
    /// </summary>
    public Version? Version { get; init; }

    /// <summary>
    /// Gets platform/board identifier.
    /// </summary>
    public string? Platform { get; init; }

    /// <summary>
    /// Gets device capabilities.
    /// </summary>
    public string[] Capabilities { get; init; } =[];

    /// <summary>
    /// Gets a value indicating whether whether the device supports raw-paste mode.
    /// </summary>
    public bool SupportsRawPasteMode { get; init; }

    /// <summary>
    /// Gets device description for display purposes.
    /// </summary>
    public string Description => this.Implementation != null && this.Platform != null
        ? $"{this.Implementation} {this.Version} on {this.Platform} ({this.PortName})"
        : $"MicroPython Device ({this.PortName})";

    /// <summary>
    /// Gets confidence score for device identification (0.0 to 1.0).
    /// </summary>
    public double IdentificationConfidence { get; init; } = 0.0;
}

/// <summary>
/// Discovery service for MicroPython devices connected via serial/USB.
/// </summary>
public class SerialDeviceDiscoveryLogger
{
}

/// <inheritdoc/>
public static partial class SerialDeviceDiscovery
{
    private static readonly ILogger Logger =
        Microsoft.Extensions.Logging.Abstractions.NullLogger<SerialDeviceDiscoveryLogger>.Instance;

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Discover all MicroPython devices connected to the system.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<DeviceInfo[]> DiscoverMicroPythonDevicesAsync(
        CancellationToken cancellationToken = default)
        {
        string[] availablePorts = SerialPort.GetPortNames();
        var devices = new List<DeviceInfo>();
        var probeTasks = new List<Task<DeviceInfo?>>();

        Logger.LogDebug("Discovering MicroPython devices on {Count} available ports", availablePorts.Length);

        // Probe all ports concurrently with timeout
        foreach (string? port in availablePorts)
        {
            probeTasks.Add(ProbeDeviceWithTimeoutAsync(port, cancellationToken));
        }

        // Wait for all probes to complete
        DeviceInfo?[] results = await Task.WhenAll(probeTasks);

        // Collect successful identifications
        foreach (DeviceInfo? result in results)
        {
            if (result != null)
            {
                devices.Add(result);
            }
        }

        Logger.LogInformation("Discovered {Count} MicroPython devices", devices.Count);
        return[.. devices];
    }

    /// <summary>
    /// Discover MicroPython devices matching specific criteria.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<DeviceInfo[]> DiscoverDevicesAsync(
        Func<DeviceInfo, bool> filter,
        CancellationToken cancellationToken = default)
        {
        DeviceInfo[] allDevices = await DiscoverMicroPythonDevicesAsync(cancellationToken);
        return allDevices.Where(filter).ToArray();
    }

    /// <summary>
    /// Find the best MicroPython device automatically.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<DeviceInfo?> FindBestDeviceAsync(CancellationToken cancellationToken = default)
    {
        DeviceInfo[] devices = await DiscoverMicroPythonDevicesAsync(cancellationToken);

        if (devices.Length == 0)
        {
            return null;
        }

        // Sort by identification confidence and known device preference
        return devices
            .OrderByDescending(d => d.IdentificationConfidence)
            .ThenByDescending(d => IsKnownDevice(d.PortName))
            .First();
    }

    /// <summary>
    /// Check if a specific port has a MicroPython device.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<DeviceInfo?> ProbePortAsync(
        string portName,
        CancellationToken cancellationToken = default)
        {
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new ArgumentException("Port name cannot be null or empty", nameof(portName));
        }

        return await ProbeDeviceWithTimeoutAsync(portName, cancellationToken);
    }

    private static async Task<DeviceInfo?> ProbeDeviceWithTimeoutAsync(
        string portName,
        CancellationToken cancellationToken)
        {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ProbeTimeout);

            return await ProbeDeviceAsync(portName, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.LogDebug("Probe timeout for port {PortName}", portName);
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Probe failed for port {PortName}", portName);
            return null;
        }
    }

    private static async Task<DeviceInfo?> ProbeDeviceAsync(string portName, CancellationToken cancellationToken)
    {
        Logger.LogDebug("Probing device on port {PortName}", portName);

        SerialDeviceCommunication? communication = null;
        try
        {
            // Create communication with shorter timeout for probing
            communication = new SerialDeviceCommunication(portName, baudRate: 115200, timeout: 2000);

            // Try to connect
            await communication.ConnectAsync(cancellationToken);

            // Query device information
            DeviceInfo deviceInfo = await QueryDeviceInformationAsync(communication, portName, cancellationToken);

            Logger.LogDebug(
                "Successfully identified device on {PortName}: {Description}",
                portName, deviceInfo.Description);

            return deviceInfo;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to probe device on port {PortName}", portName);
            return null;
        }
        finally
        {
            try
            {
                if (communication != null)
                {
                    await communication.DisconnectAsync(cancellationToken);
                    communication.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Error cleaning up probe connection for {PortName}", portName);
            }
        }
    }

    private static async Task<DeviceInfo> QueryDeviceInformationAsync(
        SerialDeviceCommunication communication, string portName, CancellationToken cancellationToken)
        {
        var deviceInfo = new DeviceInfo { PortName = portName };
        double confidence = 0.1; // Base confidence for responding device

        try
        {
            // Query implementation information
            string implInfo = await communication.ExecuteAsync(
                "import sys; print(sys.implementation.name, sys.implementation.version, sys.platform)",
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(implInfo))
            {
                string[] parts = implInfo.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    string implementation = parts[0];
                    string versionStr = parts[1].Trim('(', ')');
                    string platform = parts[2];

                    // Parse version
                    Version? version = null;
                    if (TryParseVersion(versionStr, out Version? parsedVersion))
                    {
                        version = parsedVersion;
                    }

                    deviceInfo = new DeviceInfo
                    {
                        PortName = deviceInfo.PortName,
                        Implementation = implementation,
                        Version = version,
                        Platform = platform,
                        Capabilities = deviceInfo.Capabilities,
                        SupportsRawPasteMode = deviceInfo.SupportsRawPasteMode,
                        IdentificationConfidence = deviceInfo.IdentificationConfidence,
                    };

                    confidence += 0.7; // High confidence for proper implementation info

                    // Check if it's a known MicroPython implementation
                    if (implementation.Equals("micropython", StringComparison.OrdinalIgnoreCase) ||
                        implementation.Equals("circuitpython", StringComparison.OrdinalIgnoreCase))
                        {
                        confidence += 0.2; // Bonus for recognized implementation
                    }
                }
            }

            // Test raw-paste mode support
            bool supportsRawPaste = false;
            try
            {
                // This is a simple test - a full implementation would test the actual protocol
                await communication.ExecuteAsync("1+1", cancellationToken);
                supportsRawPaste = true;
                confidence += 0.1;
            }
            catch
            {
                // Raw-paste mode test failed, but device still works
            }

            // Query additional capabilities
            var capabilities = new List<string>();

            try
            {
                string modules = await communication.ExecuteAsync("help('modules')", cancellationToken);
                if (modules.Contains("machine"))
                {
                    capabilities.Add("machine");
                }

                if (modules.Contains("network"))
                {
                    capabilities.Add("network");
                }

                if (modules.Contains("bluetooth"))
                {
                    capabilities.Add("bluetooth");
                }
            }
            catch
            {
                // Capabilities query failed, continue anyway
            }

            return new DeviceInfo
            {
                PortName = deviceInfo.PortName,
                Implementation = deviceInfo.Implementation,
                Version = deviceInfo.Version,
                Platform = deviceInfo.Platform,
                Capabilities =[.. capabilities],
                SupportsRawPasteMode = supportsRawPaste,
                IdentificationConfidence = Math.Min(confidence, 1.0),
            };
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error querying device information for {PortName}", portName);

            // Return minimal device info if basic queries fail
            return new DeviceInfo
            {
                PortName = deviceInfo.PortName,
                Implementation = deviceInfo.Implementation,
                Version = deviceInfo.Version,
                Platform = deviceInfo.Platform,
                Capabilities = deviceInfo.Capabilities,
                SupportsRawPasteMode = deviceInfo.SupportsRawPasteMode,
                IdentificationConfidence = confidence,
            };
        }
    }

    private static bool TryParseVersion(string versionString, out Version version)
    {
        version = new Version();

        try
        {
            // Handle version strings like "(1, 20, 0)" or "1.20.0"
            string cleanVersion = versionString.Trim('(', ')').Replace(',', '.');
            var versionRegex = MyRegex();
            Match match = versionRegex.Match(cleanVersion);

            if (match.Success)
            {
                int major = int.Parse(match.Groups[1].Value);
                int minor = int.Parse(match.Groups[2].Value);
                int build = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

                version = new Version(major, minor, build);
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to parse version string: {VersionString}", versionString);
        }

        return false;
    }

    private static bool IsKnownDevice(string portName)
    {
        // Simple heuristic based on port name patterns
        // More sophisticated implementations could query USB device information
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows COM ports
            return portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux device patterns for common MicroPython devices
            return portName.StartsWith("/dev/ttyUSB") ||
                   portName.StartsWith("/dev/ttyACM") ||
                   portName.StartsWith("/dev/ttyS");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS device patterns
            return portName.StartsWith("/dev/cu.") ||
                   portName.StartsWith("/dev/tty.");
        }

        return false;
    }

    /// <summary>
    /// Find devices running MicroPython.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<DeviceInfo[]> FindMicroPythonDevicesAsync(
        CancellationToken cancellationToken = default)
        {
        return await DiscoverDevicesAsync(
            d => d.Implementation?.Equals("micropython", StringComparison.OrdinalIgnoreCase) == true,
            cancellationToken);
    }

    /// <summary>
    /// Find devices running CircuitPython.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<DeviceInfo[]> FindCircuitPythonDevicesAsync(
        CancellationToken cancellationToken = default)
        {
        return await DiscoverDevicesAsync(
            d => d.Implementation?.Equals("circuitpython", StringComparison.OrdinalIgnoreCase) == true,
            cancellationToken);
    }

    /// <summary>
    /// Find devices on a specific platform.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<DeviceInfo[]> FindDevicesOnPlatformAsync(
        string platform, CancellationToken cancellationToken = default)
        {
        return await DiscoverDevicesAsync(
            d => d.Platform?.Contains(platform, StringComparison.OrdinalIgnoreCase) == true,
            cancellationToken);
    }

    [GeneratedRegex(@"(\d+)\.?(\d+)\.?(\d+)?")]
    private static partial Regex MyRegex();
}
