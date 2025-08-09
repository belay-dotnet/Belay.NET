// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Sync;

using System.Runtime.CompilerServices;
using Belay.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Extension methods to add file system support to Device instances.
/// This approach avoids circular dependencies between Belay.Core and Belay.Sync.
/// </summary>
public static class DeviceExtensions {
    // Use ConditionalWeakTable to associate DeviceFileSystem instances with Device instances
    // This ensures proper garbage collection and avoids memory leaks
    private static readonly ConditionalWeakTable<Device, DeviceFileSystem> FileSystems = new();

    /// <summary>
    /// Gets or creates a DeviceFileSystem instance for the specified device.
    /// </summary>
    /// <param name="device">The device to get file system support for.</param>
    /// <param name="logger">Optional logger for file system operations.</param>
    /// <returns>A DeviceFileSystem instance for the device.</returns>
    public static DeviceFileSystem GetFileSystem(this Device device, ILogger<DeviceFileSystem>? logger = null) {
        if (device == null) {
            throw new ArgumentNullException(nameof(device));
        }

        return FileSystems.GetValue(device, device => new DeviceFileSystem(device, logger));
    }

    /// <summary>
    /// Gets a DeviceFileSystem instance for the specified device with lazy initialization.
    /// This is the recommended approach for performance-sensitive scenarios.
    /// </summary>
    /// <param name="device">The device to get file system support for.</param>
    /// <returns>A DeviceFileSystem instance for the device.</returns>
    public static DeviceFileSystem FileSystem(this Device device) {
        return device.GetFileSystem();
    }
}
