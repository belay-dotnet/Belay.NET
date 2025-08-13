// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.IO.Ports;

namespace Belay.Core;

/// <summary>
/// Simple device discovery that returns available serial ports.
/// Replaces complex detection logic with basic port enumeration.
/// </summary>
public static class DeviceDiscovery
{
    /// <summary>
    /// Discovers available serial ports that could be MicroPython devices.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of connection strings for discovered devices.</returns>
    public static Task<string[]> DiscoverDevicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var portNames = SerialPort.GetPortNames();
            var connectionStrings = portNames.Select(port => $"serial:{port}").ToArray();
            
            return Task.FromResult(connectionStrings);
        }
        catch
        {
            // Return empty array if discovery fails
            return Task.FromResult(Array.Empty<string>());
        }
    }

    /// <summary>
    /// Creates a device connection for the first discovered device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A device connection for the first discovered device, or null if none found.</returns>
    public static async Task<DeviceConnection?> DiscoverFirstAsync(CancellationToken cancellationToken = default)
    {
        var devices = await DiscoverDevicesAsync(cancellationToken);
        if (devices.Length == 0)
            return null;

        // Parse first connection string
        var firstDevice = devices[0];
        var parts = firstDevice.Split(':', 2);
        if (parts.Length != 2)
            return null;

        return new DeviceConnection(DeviceConnection.ConnectionType.Serial, parts[1]);
    }
}