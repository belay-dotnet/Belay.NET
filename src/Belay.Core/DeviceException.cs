// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

/// <summary>
/// Simple exception for all device-related errors.
/// Follows simple error handling pattern with minimal complexity.
/// </summary>
public class DeviceException : Exception {
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public DeviceException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public DeviceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
