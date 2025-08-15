// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Minimal Linux-compatible serial connection using file I/O operations.
/// Simple cross-platform serial communication for MicroPython devices.
/// </summary>
public sealed class LinuxSerialConnection : IDisposable {
    private readonly string portPath;
    private FileStream? portStream;
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinuxSerialConnection"/> class.
    /// </summary>
    /// <param name="portPath">Path to the serial device (e.g., /dev/ttyUSB0).</param>
    public LinuxSerialConnection(string portPath) {
        this.portPath = portPath ?? throw new ArgumentNullException(nameof(portPath));
    }

    /// <summary>
    /// Gets a value indicating whether the connection is open.
    /// </summary>
    public bool IsOpen => this.portStream?.CanRead == true;

    /// <summary>
    /// Gets the underlying stream for advanced protocol operations.
    /// </summary>
    /// <returns>The file stream used for serial communication.</returns>
    public Stream GetStream() {
        if (!this.IsOpen || this.portStream == null) {
            throw new InvalidOperationException("Port is not open");
        }

        return this.portStream;
    }

    /// <summary>
    /// Opens the serial connection.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task OpenAsync() {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(LinuxSerialConnection));
        }

        if (this.IsOpen) {
            return;
        }

        // Configure serial port using stty (no command injection - validate path)
        if (!this.portPath.StartsWith("/dev/")) {
            throw new ArgumentException("Invalid device path");
        }

        // Configure serial port BEFORE opening to prevent blocking
        await this.ConfigureSerialPortAsync().ConfigureAwait(false);

        // Open the device file with timeout to prevent indefinite blocking
        this.portStream = await this.OpenSerialFileWithTimeoutAsync().ConfigureAwait(false);

        // Brief delay for device initialization
        await Task.Delay(100).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a string to the serial port.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task WriteAsync(string data) {
        if (!this.IsOpen) {
            throw new InvalidOperationException("Port is not open");
        }

        var bytes = Encoding.UTF8.GetBytes(data);
        await this.portStream!.WriteAsync(bytes).ConfigureAwait(false);
        await this.portStream.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Reads existing data from the port with simple timeout.
    /// </summary>
    /// <returns>Available data as string.</returns>
    public async Task<string> ReadExistingAsync() {
        if (!this.IsOpen || this.portStream == null) {
            return string.Empty;
        }

        var buffer = new byte[1024];
        try {
            // Longer timeout for Raw REPL responses - devices need time to process
            using var cts = new CancellationTokenSource(1000); // 1 second timeout
            var bytesRead = await this.portStream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
            return bytesRead > 0 ? Encoding.UTF8.GetString(buffer, 0, bytesRead) : string.Empty;
        }
        catch (OperationCanceledException) {
            return string.Empty;
        }
    }

    /// <summary>
    /// Closes the serial connection.
    /// </summary>
    public void Close() {
        this.portStream?.Close();
        this.portStream = null;
    }

    private async Task<FileStream> OpenSerialFileWithTimeoutAsync() {
        // Run the blocking FileStream open in a separate task with timeout
        var openTask = Task.Run(() => {
            try {
                return new FileStream(this.portPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.None);
            }
            catch (Exception ex) {
                throw new InvalidOperationException($"Failed to open serial device {this.portPath}: {ex.Message}", ex);
            }
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);

        var completedTask = await Task.WhenAny(openTask, timeoutTask).ConfigureAwait(false);

        if (completedTask == timeoutTask) {
            throw new InvalidOperationException($"Opening serial device {this.portPath} timed out after 5 seconds. Device may be busy or not responding.");
        }

        return await openTask.ConfigureAwait(false);
    }

    private async Task ConfigureSerialPortAsync() {
        try {
            // Configure serial port using standard settings similar to mpremote
            // mpremote uses: 115200 baud, 8 data bits, no parity, 1 stop bit, raw mode
            var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "stty",
                    Arguments = $"-F {this.portPath} 115200 raw -echo -echoe -echok -echoctl -echoke -crtscts -hupcl min 1 time 0",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                },
            };

            process.Start();

            // Add timeout to prevent hanging
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            if (process.ExitCode != 0) {
                var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"Failed to configure serial port: {error}");
            }
        }
        catch (OperationCanceledException) {
            throw new InvalidOperationException("Serial port configuration timed out after 5 seconds");
        }
        catch (Exception ex) {
            throw new InvalidOperationException($"Failed to configure serial port: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        if (this.disposed) {
            return;
        }

        this.Close();
        this.disposed = true;
    }
}
