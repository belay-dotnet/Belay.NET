// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

using System.Diagnostics;
using System.Text;

/// <summary>
/// Process-based serial connection that uses external processes for I/O like mpremote does.
/// This approach avoids the FileStream blocking issues on Linux serial devices.
/// </summary>
public sealed class ProcessSerialConnection : IDisposable {
    private readonly string portPath;
    private bool disposed = false;
    private bool isConfigured = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessSerialConnection"/> class.
    /// </summary>
    /// <param name="portPath">Path to the serial device (e.g., /dev/ttyUSB0).</param>
    public ProcessSerialConnection(string portPath) {
        this.portPath = portPath ?? throw new ArgumentNullException(nameof(portPath));
    }

    /// <summary>
    /// Gets a value indicating whether the connection is open.
    /// </summary>
    public bool IsOpen => this.isConfigured;

    /// <summary>
    /// Opens the serial connection using process-based configuration.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task OpenAsync() {
        if (this.disposed) {
            throw new ObjectDisposedException(nameof(ProcessSerialConnection));
        }

        if (this.IsOpen) {
            return;
        }

        // Configure serial port using stty (this approach works reliably)
        if (!this.portPath.StartsWith("/dev/")) {
            throw new ArgumentException("Invalid device path");
        }

        await this.ConfigureSerialPortAsync().ConfigureAwait(false);
        this.isConfigured = true;

        // Brief delay for device initialization
        await Task.Delay(100).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes data to the serial port using echo command.
    /// </summary>
    /// <param name="data">The data to write.</param>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task WriteAsync(string data) {
        if (!this.IsOpen) {
            throw new InvalidOperationException("Port is not open");
        }

        // Use echo to write to device (safer than FileStream)
        var process = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = "bash",
                Arguments = $"-c \"echo -ne '{EscapeForBash(data)}' > {this.portPath}\"",
                UseShellExecute = false,
                RedirectStandardError = true
            },
        };

        process.Start();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

        if (process.ExitCode != 0) {
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Failed to write to serial port: {error}");
        }
    }

    /// <summary>
    /// Reads available data from the port using timeout-controlled cat.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    /// <returns>Available data as string.</returns>
    public async Task<string> ReadWithTimeoutAsync(int timeoutMs = 1000) {
        if (!this.IsOpen) {
            return string.Empty;
        }

        try {
            var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "timeout",
                    Arguments = $"{timeoutMs / 1000.0:F1}s cat {this.portPath}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
            };

            process.Start();

            // Read output with overall timeout
            using var cts = new CancellationTokenSource(timeoutMs + 1000);
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var processTask = process.WaitForExitAsync(cts.Token);

            await Task.WhenAny(outputTask, processTask).ConfigureAwait(false);

            if (outputTask.IsCompleted) {
                return await outputTask.ConfigureAwait(false);
            }

            return string.Empty;
        }
        catch (OperationCanceledException) {
            return string.Empty;
        }
        catch (Exception) {
            return string.Empty;
        }
    }

    /// <summary>
    /// Closes the serial connection.
    /// </summary>
    public void Close() {
        this.isConfigured = false;
    }

    private async Task ConfigureSerialPortAsync() {
        try {
            // Configure serial port using standard settings similar to mpremote
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
    }

    private static string EscapeForBash(string input) {
        // Escape special characters for bash
        return input
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
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
