// Copyright 2025 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Text.RegularExpressions;

namespace Belay.Sync
{
    /// <summary>
    /// Provides utilities for handling device file paths in a cross-platform manner.
    /// MicroPython/CircuitPython devices use Unix-style paths regardless of the host OS.
    /// </summary>
    public static class DevicePathUtil
    {
        /// <summary>
        /// The path separator used on MicroPython/CircuitPython devices (always forward slash).
        /// </summary>
        public const char DeviceSeparator = '/';

        /// <summary>
        /// The root directory path on devices.
        /// </summary>
        public const string DeviceRoot = "/";

        private static readonly Regex InvalidDevicePathChars = new(@"[<>:""|?*\x00-\x1f]", RegexOptions.Compiled);

        /// <summary>
        /// Normalizes a device path by ensuring it uses forward slashes and removing redundant separators.
        /// </summary>
        /// <param name="path">The path to normalize.</param>
        /// <returns>The normalized device path.</returns>
        /// <exception cref="ArgumentException">Thrown when the path is invalid.</exception>
        public static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return DeviceRoot;

            // Convert all separators to device separators
            var normalized = path.Replace('\\', DeviceSeparator);

            // Remove duplicate separators
            while (normalized.Contains("//"))
                normalized = normalized.Replace("//", "/");

            // Ensure it starts with a separator (absolute path)
            if (!normalized.StartsWith(DeviceSeparator))
                normalized = DeviceSeparator + normalized;

            // Remove trailing separator unless it's the root
            if (normalized.Length > 1 && normalized.EndsWith(DeviceSeparator))
                normalized = normalized.TrimEnd(DeviceSeparator);

            ValidatePath(normalized);
            return normalized;
        }

        /// <summary>
        /// Combines multiple path segments into a single device path.
        /// </summary>
        /// <param name="paths">The path segments to combine.</param>
        /// <returns>The combined device path.</returns>
        /// <exception cref="ArgumentException">Thrown when any path segment is invalid.</exception>
        public static string Combine(params string[] paths)
        {
            if (paths.Length == 0)
                return DeviceRoot;

            var combined = string.Join(DeviceSeparator.ToString(), paths);
            return NormalizePath(combined);
        }

        /// <summary>
        /// Gets the directory name of the specified path.
        /// </summary>
        /// <param name="path">The path to get the directory name for.</param>
        /// <returns>The directory name, or "/" for the root directory.</returns>
        public static string GetDirectoryName(string path)
        {
            var normalized = NormalizePath(path);
            
            if (normalized == DeviceRoot)
                return DeviceRoot;

            var lastSeparator = normalized.LastIndexOf(DeviceSeparator);
            if (lastSeparator <= 0)
                return DeviceRoot;

            var directory = normalized.Substring(0, lastSeparator);
            return directory.Length == 0 ? DeviceRoot : directory;
        }

        /// <summary>
        /// Gets the file name from the specified path.
        /// </summary>
        /// <param name="path">The path to get the file name from.</param>
        /// <returns>The file name, or empty string for the root directory.</returns>
        public static string GetFileName(string path)
        {
            var normalized = NormalizePath(path);
            
            if (normalized == DeviceRoot)
                return string.Empty;

            var lastSeparator = normalized.LastIndexOf(DeviceSeparator);
            return normalized.Substring(lastSeparator + 1);
        }

        /// <summary>
        /// Gets the file name without its extension.
        /// </summary>
        /// <param name="path">The path to get the file name from.</param>
        /// <returns>The file name without extension.</returns>
        public static string GetFileNameWithoutExtension(string path)
        {
            var fileName = GetFileName(path);
            var lastDot = fileName.LastIndexOf('.');
            
            if (lastDot <= 0) // No extension or starts with dot (hidden file)
                return fileName;
            
            return fileName.Substring(0, lastDot);
        }

        /// <summary>
        /// Gets the extension of the specified path.
        /// </summary>
        /// <param name="path">The path to get the extension from.</param>
        /// <returns>The file extension including the dot, or empty string if no extension.</returns>
        public static string GetExtension(string path)
        {
            var fileName = GetFileName(path);
            var lastDot = fileName.LastIndexOf('.');
            
            if (lastDot <= 0) // No extension or starts with dot (hidden file)
                return string.Empty;
            
            return fileName.Substring(lastDot);
        }

        /// <summary>
        /// Determines whether the specified path is a valid device path.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        /// <returns>True if the path is valid, false otherwise.</returns>
        public static bool IsValidPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                ValidatePath(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Converts a host file system path to a device path format.
        /// </summary>
        /// <param name="hostPath">The host file system path.</param>
        /// <param name="baseHostPath">The base host directory to make the path relative to.</param>
        /// <returns>The equivalent device path.</returns>
        public static string FromHostPath(string hostPath, string? baseHostPath = null)
        {
            if (string.IsNullOrEmpty(hostPath))
                return DeviceRoot;

            var normalized = hostPath;

            // Make relative to base path if provided
            if (!string.IsNullOrEmpty(baseHostPath))
            {
                var basePath = Path.GetFullPath(baseHostPath);
                var fullPath = Path.GetFullPath(hostPath);
                
                if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = fullPath.Substring(basePath.Length);
                    
                    // Remove leading separator
                    if (normalized.StartsWith(Path.DirectorySeparatorChar) || 
                        normalized.StartsWith(Path.AltDirectorySeparatorChar))
                    {
                        normalized = normalized.Substring(1);
                    }
                }
            }

            return NormalizePath(normalized);
        }

        /// <summary>
        /// Converts a device path to a host file system path format.
        /// </summary>
        /// <param name="devicePath">The device path.</param>
        /// <param name="baseHostPath">The base host directory to combine with the device path.</param>
        /// <returns>The equivalent host file system path.</returns>
        public static string ToHostPath(string devicePath, string baseHostPath)
        {
            var normalized = NormalizePath(devicePath);
            
            // Remove leading separator for combination
            if (normalized.StartsWith(DeviceSeparator) && normalized.Length > 1)
                normalized = normalized.Substring(1);
            else if (normalized == DeviceRoot)
                normalized = string.Empty;

            return Path.Combine(baseHostPath, normalized);
        }

        /// <summary>
        /// Determines if the specified path is under the given parent directory.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <param name="parentDirectory">The parent directory path.</param>
        /// <returns>True if the path is under the parent directory, false otherwise.</returns>
        public static bool IsUnderDirectory(string path, string parentDirectory)
        {
            var normalizedPath = NormalizePath(path);
            var normalizedParent = NormalizePath(parentDirectory);
            
            if (normalizedParent == DeviceRoot)
                return true; // Everything is under root
            
            return normalizedPath.StartsWith(normalizedParent + DeviceSeparator, StringComparison.Ordinal) ||
                   normalizedPath == normalizedParent;
        }

        /// <summary>
        /// Validates that the specified path contains only valid characters for device file systems.
        /// </summary>
        /// <param name="path">The path to validate.</param>
        /// <exception cref="ArgumentException">Thrown when the path contains invalid characters.</exception>
        private static void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            if (InvalidDevicePathChars.IsMatch(path))
                throw new ArgumentException($"Path contains invalid characters: {path}", nameof(path));

            // Check for reserved names that might cause issues
            var fileName = GetFileName(path);
            if (!string.IsNullOrEmpty(fileName))
            {
                var reserved = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
                var baseFileName = GetFileNameWithoutExtension(fileName).ToUpperInvariant();
                
                if (Array.Exists(reserved, name => name == baseFileName))
                    throw new ArgumentException($"Path contains reserved name: {fileName}", nameof(path));
            }
        }
    }
}