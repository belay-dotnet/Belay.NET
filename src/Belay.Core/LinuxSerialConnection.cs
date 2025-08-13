// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;

namespace Belay.Core;

/// <summary>
/// Minimal Linux-compatible serial connection using file I/O operations.
/// Simple cross-platform serial communication for MicroPython devices.
/// </summary>
public sealed class LinuxSerialConnection : IDisposable
{
    private readonly string portPath;
    private FileStream? portStream;
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinuxSerialConnection"/> class.
    /// </summary>
    /// <param name="portPath">Path to the serial device (e.g., /dev/ttyUSB0).</param>
    public LinuxSerialConnection(string portPath)
    {
        this.portPath = portPath ?? throw new ArgumentNullException(nameof(portPath));
    }

    /// <summary>
    /// Gets a value indicating whether the connection is open.
    /// </summary>
    public bool IsOpen => this.portStream?.CanRead == true;

    /// <summary>
    /// Opens the serial connection.
    /// </summary>
    public async Task OpenAsync()
    {
        if (this.disposed)
            throw new ObjectDisposedException(nameof(LinuxSerialConnection));

        if (this.IsOpen)
            return;

        // Configure serial port using stty (no command injection - validate path)
        if (!this.portPath.StartsWith("/dev/"))
            throw new ArgumentException("Invalid device path");
            
        await ConfigureSerialPortAsync().ConfigureAwait(false);

        // Open the device file for read/write
        this.portStream = new FileStream(this.portPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        
        // Brief delay for device initialization
        await Task.Delay(100).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a string to the serial port.
    /// </summary>
    /// <param name="data">The data to write.</param>
    public async Task WriteAsync(string data)
    {
        if (!this.IsOpen)
            throw new InvalidOperationException("Port is not open");

        var bytes = Encoding.UTF8.GetBytes(data);
        await this.portStream!.WriteAsync(bytes).ConfigureAwait(false);
        await this.portStream.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Reads existing data from the port with simple timeout.
    /// </summary>
    /// <returns>Available data as string.</returns>
    public async Task<string> ReadExistingAsync()
    {
        if (!this.IsOpen)
            return string.Empty;

        var buffer = new byte[1024];
        
        try
        {
            // Simple timeout approach - no complex logic
            using var cts = new CancellationTokenSource(100); // 100ms timeout
            var bytesRead = await this.portStream!.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
            
            return bytesRead > 0 ? Encoding.UTF8.GetString(buffer, 0, bytesRead) : string.Empty;
        }
        catch (OperationCanceledException)
        {
            return string.Empty; // Timeout - no data available
        }
    }

    /// <summary>
    /// Closes the serial connection.
    /// </summary>
    public void Close()
    {
        this.portStream?.Close();
        this.portStream = null;
    }

    private async Task ConfigureSerialPortAsync()
    {
        // Simple stty configuration
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "stty",
                Arguments = $"-F {this.portPath} 115200 cs8 -cstopb -parenb raw -echo",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Failed to configure serial port: {error}");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
            return;

        this.Close();
        this.disposed = true;
    }
}