// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Sync {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Belay.Core;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Implements file system operations for MicroPython/CircuitPython devices.
    /// </summary>
    public sealed class DeviceFileSystem : IDeviceFileSystem {
        private readonly Device device;
        private readonly ILogger<DeviceFileSystem> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceFileSystem"/> class.
        /// </summary>
        /// <param name="device">The device to perform file system operations on.</param>
        /// <param name="logger">Optional logger for diagnostic information.</param>
        public DeviceFileSystem(Device device, ILogger<DeviceFileSystem>? logger = null) {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DeviceFileSystem>.Instance;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DeviceFileInfo>> ListAsync(
            string path = "/",
            bool recursive = false,
            CancellationToken cancellationToken = default) {
            var normalizedPath = DevicePathUtil.NormalizePath(path);
            logger.LogDebug("Listing directory: {Path} (recursive: {Recursive})", normalizedPath, recursive);

            var code = recursive
                ? GenerateRecursiveListCode(normalizedPath)
                : GenerateListCode(normalizedPath);

            try {
                var result = await device.ExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
                return ParseListResult(result);
            }
            catch (DeviceException ex) when (ex.Message.Contains("OSError") || ex.Message.Contains("ENOENT")) {
                throw new DirectoryNotFoundException($"Directory not found: {normalizedPath}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<DeviceFileInfo?> GetFileInfoAsync(
            string path,
            CancellationToken cancellationToken = default) {
            var normalizedPath = DevicePathUtil.NormalizePath(path);
            logger.LogDebug("Getting file info for: {Path}", normalizedPath);

            var escapedPath = normalizedPath.Replace("'", "\\'");
            var code = $@"
import os
try:
    stat = os.stat('{escapedPath}')
    is_dir = (stat[0] & 0x4000) != 0  # S_IFDIR
    size = stat[6] if not is_dir else None
    # Try to get modified time (may not be supported)
    try:
        mtime = stat[8] if len(stat) > 8 else None
    except:
        mtime = None
    print('{{""path"": ""{escapedPath}"", ""is_directory"": '+ str(is_dir).lower() +', ""size"": '+ str(size) +', ""modified"": '+ str(mtime) +'}}')
except OSError:
    print('null')
";

            try {
                var result = await device.ExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);

                if (result.Trim() == "null") {
                    return null;
                }

                return ParseFileInfo(result);
            }
            catch (Exception ex) {
                logger.LogWarning(ex, "Error getting file info for {Path}", normalizedPath);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<byte[]> ReadFileAsync(
            string path,
            CancellationToken cancellationToken = default) {
            var normalizedPath = DevicePathUtil.NormalizePath(path);
            logger.LogDebug("Reading file: {Path}", normalizedPath);

            // For large files, we'll need chunked reading to avoid memory issues
            // First, get the file size
            var fileInfo = await GetFileInfoAsync(normalizedPath, cancellationToken).ConfigureAwait(false);
            if (fileInfo == null) {
                throw new FileNotFoundException($"File not found: {normalizedPath}");
            }

            if (fileInfo.IsDirectory) {
                throw new UnauthorizedAccessException($"Path is a directory: {normalizedPath}");
            }

            var fileSize = fileInfo.Size ?? 0;

            // For small files, read directly
            if (fileSize <= 8192) {
                return await ReadFileDirectAsync(normalizedPath, cancellationToken).ConfigureAwait(false);
            }
            else {
                return await ReadFileChunkedAsync(normalizedPath, fileSize, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<string> ReadTextFileAsync(
            string path,
            CancellationToken cancellationToken = default) {
            var bytes = await ReadFileAsync(path, cancellationToken).ConfigureAwait(false);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <inheritdoc />
        public async Task WriteFileAsync(
            string path,
            byte[] content,
            CancellationToken cancellationToken = default) {
            var normalizedPath = DevicePathUtil.NormalizePath(path);
            logger.LogDebug("Writing file: {Path} ({Size} bytes)", normalizedPath, content.Length);

            if (content.Length <= 4096) {
                await WriteFileDirectAsync(normalizedPath, content, cancellationToken).ConfigureAwait(false);
            }
            else {
                await WriteFileChunkedAsync(normalizedPath, content, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task WriteTextFileAsync(
            string path,
            string content,
            CancellationToken cancellationToken = default) {
            var bytes = Encoding.UTF8.GetBytes(content);
            await WriteFileAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteFileAsync(
            string path,
            CancellationToken cancellationToken = default) {
            var normalizedPath = DevicePathUtil.NormalizePath(path);
            logger.LogDebug("Deleting file: {Path}", normalizedPath);

            var escapedPath = normalizedPath.Replace("'", "\\'");
            var code = $@"
import os
try:
    os.remove('{escapedPath}')
    print('success')
except OSError as e:
    if e.errno == 2:  # ENOENT
        print('not_found')
    else:
        print(f'error: {{{{e}}}}')
";

            try {
                var result = await device.ExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
                var trimmed = result.Trim();

                if (trimmed == "not_found") {
                    throw new FileNotFoundException($"File not found: {normalizedPath}");
                }
                else if (trimmed.StartsWith("error:")) {
                    throw new IOException($"Failed to delete file {normalizedPath}: {trimmed[6..]}");
                }
            }
            catch (DeviceException ex) when (!ex.Message.Contains("not_found")) {
                throw new IOException($"Failed to delete file {normalizedPath}", ex);
            }
        }

        /// <inheritdoc />
        public async Task CreateDirectoryAsync(
            string path,
            bool recursive = false,
            CancellationToken cancellationToken = default) {
            var normalizedPath = DevicePathUtil.NormalizePath(path);
            logger.LogDebug("Creating directory: {Path} (recursive: {Recursive})", normalizedPath, recursive);

            var code = recursive
                ? $@"
import os
def makedirs(path):
    parts = path.strip('/').split('/')
    current = ''
    for part in parts:
        current = current + '/' + part
        try:
            os.mkdir(current)
        except OSError:
            pass  # Directory might already exist
makedirs('{normalizedPath.Replace("'", "\\\'")}')
print('success')
"
                : $@"
import os
try:
    os.mkdir('{normalizedPath.Replace("'", "\\\'")}')
    print('success')
except OSError as e:
    if e.errno == 17:  # EEXIST
        print('exists')
    else:
        print(f'error: {{{{e}}}}')
";

            try {
                var result = await device.ExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
                var trimmed = result.Trim();

                if (trimmed.StartsWith("error:")) {
                    throw new IOException($"Failed to create directory {normalizedPath}: {trimmed[6..]}");
                }
            }
            catch (DeviceException ex) {
                throw new IOException($"Failed to create directory {normalizedPath}", ex);
            }
        }

        /// <inheritdoc />
        public async Task DeleteDirectoryAsync(
            string path,
            bool recursive = false,
            CancellationToken cancellationToken = default) {
            var normalizedPath = DevicePathUtil.NormalizePath(path);
            logger.LogDebug("Deleting directory: {Path} (recursive: {Recursive})", normalizedPath, recursive);

            var code = recursive
                ? $@"
import os
def rmtree(path):
    try:
        for entry in os.listdir(path):
            entry_path = path + '/' + entry
            try:
                stat = os.stat(entry_path)
                if (stat[0] & 0x4000) != 0:  # Directory
                    rmtree(entry_path)
                else:
                    os.remove(entry_path)
            except OSError:
                pass
        os.rmdir(path)
        print('success')
    except OSError as e:
        print(f'error: {{{{e}}}}')
rmtree('{normalizedPath.Replace("'", "\\\'")}')
"
                : $@"
import os
try:
    os.rmdir('{normalizedPath.Replace("'", "\\\'")}')
    print('success')
except OSError as e:
    if e.errno == 2:  # ENOENT
        print('not_found')
    elif e.errno == 66 or e.errno == 39:  # ENOTEMPTY
        print('not_empty')
    else:
        print(f'error: {{{{e}}}}')
";

            try {
                var result = await device.ExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
                var trimmed = result.Trim();

                if (trimmed == "not_found") {
                    throw new DirectoryNotFoundException($"Directory not found: {normalizedPath}");
                }
                else if (trimmed == "not_empty") {
                    throw new IOException($"Directory not empty: {normalizedPath}");
                }
                else if (trimmed.StartsWith("error:")) {
                    throw new IOException($"Failed to delete directory {normalizedPath}: {trimmed[6..]}");
                }
            }
            catch (DeviceException ex) {
                throw new IOException($"Failed to delete directory {normalizedPath}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(
            string path,
            CancellationToken cancellationToken = default) {
            var fileInfo = await GetFileInfoAsync(path, cancellationToken).ConfigureAwait(false);
            return fileInfo != null;
        }

        /// <inheritdoc />
        public async Task<string> CalculateChecksumAsync(
            string path,
            string algorithm = "md5",
            CancellationToken cancellationToken = default) {
            var normalizedPath = DevicePathUtil.NormalizePath(path);
            logger.LogDebug("Calculating {Algorithm} checksum for: {Path}", algorithm, normalizedPath);

            // Read the file content and calculate checksum on the host side
            // This is more reliable than trying to implement hash algorithms on the device
            var content = await ReadFileAsync(normalizedPath, cancellationToken).ConfigureAwait(false);

            using HashAlgorithm hashAlgorithm = algorithm.ToLowerInvariant() switch {
                "md5" => MD5.Create(),
                "sha1" => SHA1.Create(),
                "sha256" => SHA256.Create(),
                "sha512" => SHA512.Create(),
                _ => throw new NotSupportedException($"Checksum algorithm not supported: {algorithm}"),
            };

            var hash = hashAlgorithm.ComputeHash(content);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string GenerateListCode(string path) {
            var escapedPath = path.Replace("'", "\\'");
            return $@"
import os
import json
try:
    entries = []
    for name in os.listdir('{escapedPath}'):
        entry_path = '{escapedPath}' + ('/' if '{escapedPath}' != '/' else '') + name
        try:
            stat = os.stat(entry_path)
            is_dir = (stat[0] & 0x4000) != 0
            size = None if is_dir else stat[6]
            mtime = stat[8] if len(stat) > 8 else None
            entries.append({{""path"": entry_path, ""is_directory"": is_dir, ""size"": size, ""modified"": mtime}})
        except OSError:
            pass
    print(json.dumps(entries))
except OSError:
    print('[]')
";
        }

        private static string GenerateRecursiveListCode(string path) {
            var escapedPath = path.Replace("'", "\\'");
            return $@"
import os
import json
def list_recursive(base_path, current_path=''):
    entries = []
    full_path = base_path + current_path
    try:
        for name in os.listdir(full_path):
            entry_path = full_path + ('/' if full_path != '/' else '') + name
            try:
                stat = os.stat(entry_path)
                is_dir = (stat[0] & 0x4000) != 0
                size = None if is_dir else stat[6]
                mtime = stat[8] if len(stat) > 8 else None
                entries.append({{""path"": entry_path, ""is_directory"": is_dir, ""size"": size, ""modified"": mtime}})
                if is_dir:
                    entries.extend(list_recursive(base_path, current_path + ('/' if current_path else '') + name))
            except OSError:
                pass
    except OSError:
        pass
    return entries
print(json.dumps(list_recursive('{escapedPath}')))
";
        }

        private async Task<byte[]> ReadFileDirectAsync(string path, CancellationToken cancellationToken) {
            var escapedPath = path.Replace("'", "\\'");
            var code = $@"
import binascii
try:
    with open('{escapedPath}', 'rb') as f:
        data = f.read()
        print(binascii.hexlify(data).decode())
except OSError as e:
    if e.errno == 2:  # ENOENT
        print('not_found')
    else:
        print(f'error: {{{{e}}}}')
";

            var result = await device.ExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
            var trimmed = result.Trim();

            if (trimmed == "not_found") {
                throw new FileNotFoundException($"File not found: {path}");
            }
            else if (trimmed.StartsWith("error:")) {
                throw new IOException($"Failed to read file {path}: {trimmed[6..]}");
            }

            return Convert.FromHexString(trimmed);
        }

        private async Task<byte[]> ReadFileChunkedAsync(string path, long fileSize, CancellationToken cancellationToken) {
            const int chunkSize = 4096; // 4KB chunks
            var result = new byte[fileSize];
            var offset = 0;

            while (offset < fileSize) {
                var currentChunkSize = Math.Min(chunkSize, (int)(fileSize - offset));
                var escapedPath = path.Replace("'", "\\'");
                var code = $@"
import binascii
try:
    with open('{escapedPath}', 'rb') as f:
        f.seek({offset})
        data = f.read({currentChunkSize})
        print(binascii.hexlify(data).decode())
except OSError as e:
    print(f'error: {{{{e}}}}')
";

                var chunkResult = await device.ExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
                var trimmed = chunkResult.Trim();

                if (trimmed.StartsWith("error:")) {
                    throw new IOException($"Failed to read file chunk from {path}: {trimmed[6..]}");
                }

                var chunkData = Convert.FromHexString(trimmed);
                chunkData.CopyTo(result, offset);
                offset += chunkData.Length;
            }

            return result;
        }

        private async Task WriteFileDirectAsync(string path, byte[] content, CancellationToken cancellationToken) {
            var hexData = Convert.ToHexString(content);
            var escapedPath = path.Replace("'", "\\'");
            var code = $@"
import binascii
try:
    data = binascii.unhexlify('{hexData}')
    with open('{escapedPath}', 'wb') as f:
        f.write(data)
    print('success')
except OSError as e:
    print(f'error: {{{{e}}}}')
";

            var result = await device.ExecuteAsync<string>(code, cancellationToken).ConfigureAwait(false);
            var trimmed = result.Trim();

            if (trimmed.StartsWith("error:")) {
                throw new IOException($"Failed to write file {path}: {trimmed[6..]}");
            }
        }

        private async Task WriteFileChunkedAsync(string path, byte[] content, CancellationToken cancellationToken) {
            const int chunkSize = 4096; // 4KB chunks

            // First, create/truncate the file
            var escapedPath = path.Replace("'", "\\'");
            var initCode = $@"
try:
    with open('{escapedPath}', 'wb') as f:
        pass  # Just create/truncate the file
    print('success')
except OSError as e:
    print(f'error: {{{{e}}}}')
";

            var initResult = await device.ExecuteAsync<string>(initCode, cancellationToken).ConfigureAwait(false);
            if (initResult.Trim().StartsWith("error:")) {
                throw new IOException($"Failed to initialize file {path}: {initResult.Trim()[6..]}");
            }

            // Write chunks
            for (int offset = 0; offset < content.Length; offset += chunkSize) {
                var currentChunkSize = Math.Min(chunkSize, content.Length - offset);
                var chunk = new byte[currentChunkSize];
                Array.Copy(content, offset, chunk, 0, currentChunkSize);
                var hexData = Convert.ToHexString(chunk);

                var chunkCode = $@"
import binascii
try:
    data = binascii.unhexlify('{hexData}')
    with open('{escapedPath}', 'ab') as f:
        f.write(data)
    print('success')
except OSError as e:
    print(f'error: {{{{e}}}}')
";

                var chunkResult = await device.ExecuteAsync<string>(chunkCode, cancellationToken).ConfigureAwait(false);
                var trimmed = chunkResult.Trim();

                if (trimmed.StartsWith("error:")) {
                    throw new IOException($"Failed to write chunk to file {path}: {trimmed[6..]}");
                }
            }
        }

        private static List<DeviceFileInfo> ParseListResult(string jsonResult) {
            try {
                var entries = JsonSerializer.Deserialize<JsonElement[]>(jsonResult.Trim());
                var result = new List<DeviceFileInfo>();

                if (entries != null) {
                    foreach (var entry in entries) {
                        var path = entry.GetProperty("path").GetString() ?? string.Empty;
                        var isDirectory = entry.GetProperty("is_directory").GetBoolean();

                        long? size = null;
                        if (entry.TryGetProperty("size", out var sizeElement) && sizeElement.ValueKind == JsonValueKind.Number) {
                            size = sizeElement.GetInt64();
                        }

                        DateTime? modified = null;
                        if (entry.TryGetProperty("modified", out var modifiedElement) && modifiedElement.ValueKind == JsonValueKind.Number) {
                            var timestamp = modifiedElement.GetInt64();
                            modified = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                        }

                        result.Add(new DeviceFileInfo {
                            Path = path,
                            IsDirectory = isDirectory,
                            Size = size,
                            LastModified = modified,
                        });
                    }
                }

                return result;
            }
            catch (JsonException) {
                return new List<DeviceFileInfo>();
            }
        }

        private static DeviceFileInfo ParseFileInfo(string jsonResult) {
            var entry = JsonSerializer.Deserialize<JsonElement>(jsonResult.Trim());
            var path = entry.GetProperty("path").GetString() ?? string.Empty;
            var isDirectory = entry.GetProperty("is_directory").GetBoolean();

            long? size = null;
            if (entry.TryGetProperty("size", out var sizeElement) && sizeElement.ValueKind == JsonValueKind.Number) {
                size = sizeElement.GetInt64();
            }

            DateTime? modified = null;
            if (entry.TryGetProperty("modified", out var modifiedElement) && modifiedElement.ValueKind == JsonValueKind.Number) {
                var timestamp = modifiedElement.GetInt64();
                modified = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
            }

            return new DeviceFileInfo {
                Path = path,
                IsDirectory = isDirectory,
                Size = size,
                LastModified = modified,
            };
        }
    }
}
