namespace Belay.Core;

/// <summary>
/// Simplified device connection interface that replaces the complex IDeviceCommunication hierarchy.
/// Focuses on essential operations without over-abstraction.
/// See ICD-002 for complete specification.
/// </summary>
public interface IDeviceConnection : IDisposable
{
    /// <summary>
    /// Executes Python code on the device and returns the raw string output.
    /// </summary>
    /// <param name="code">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The raw string output from the device.</returns>
    Task<string> ExecutePython(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes Python code on the device and parses the result as the specified type.
    /// </summary>
    /// <typeparam name="T">The type to parse the result as.</typeparam>
    /// <param name="code">The Python code to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The parsed result from the device.</returns>
    Task<T> ExecutePython<T>(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a file to the device.
    /// </summary>
    /// <param name="devicePath">The path on the device where to write the file.</param>
    /// <param name="data">The file data to write.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task WriteFile(string devicePath, byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a file from the device.
    /// </summary>
    /// <param name="devicePath">The path on the device to read from.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The file data from the device.</returns>
    Task<byte[]> ReadFile(string devicePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from the device.
    /// </summary>
    /// <param name="devicePath">The path on the device to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task DeleteFile(string devicePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists files in a directory on the device.
    /// </summary>
    /// <param name="devicePath">The directory path on the device. Defaults to root "/".</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Array of file names in the directory.</returns>
    Task<string[]> ListFiles(string devicePath = "/", CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects to the device.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    Task Connect(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the device.
    /// </summary>
    Task Disconnect();

    /// <summary>
    /// Gets a value indicating whether the device is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Gets information about the connected device.
    /// </summary>
    string DeviceInfo { get; }

    /// <summary>
    /// Gets the connection string used to connect to this device.
    /// </summary>
    string ConnectionString { get; }
}