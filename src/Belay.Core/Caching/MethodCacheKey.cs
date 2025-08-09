// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Caching {
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Represents a unique cache key for method deployment based on device and method characteristics.
    /// </summary>
    public sealed class MethodCacheKey : IEquatable<MethodCacheKey> {
        /// <summary>
        /// Gets the unique hash representing the method cache key.
        /// </summary>
        public string Hash { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MethodCacheKey"/> class.
        /// Creates a new method cache key from device and method details.
        /// </summary>
        /// <param name="deviceId">Unique identifier for the device.</param>
        /// <param name="firmwareVersion">Firmware version of the device.</param>
        /// <param name="methodSignature">Unique method signature or content hash.</param>
        public MethodCacheKey(string deviceId, string firmwareVersion, string methodSignature) {
            this.Hash = GenerateHash(deviceId, firmwareVersion, methodSignature);
        }

        /// <summary>
        /// Generates a deterministic SHA-256 hash for the cache key.
        /// </summary>
        private static string GenerateHash(string deviceId, string firmwareVersion, string methodSignature) {
            using var sha256 = SHA256.Create();
            var combinedInput = $"{deviceId}|{firmwareVersion}|{methodSignature}";
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedInput));
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Determines whether the current cache key is equal to another.
        /// </summary>
        /// <returns></returns>
        public bool Equals(MethodCacheKey other) {
            if (ReferenceEquals(null, other)) {
                return false;
            }

            if (ReferenceEquals(this, other)) {
                return true;
            }

            return this.Hash == other.Hash;
        }

        /// <summary>
        /// Determines whether the current cache key is equal to another object.
        /// </summary>
        /// <returns></returns>
        public override bool Equals(object obj) {
            return ReferenceEquals(this, obj) || (obj is MethodCacheKey other && this.Equals(other));
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() {
            return this.Hash?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Provides a string representation of the cache key.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => this.Hash;

        /// <summary>
        /// Equality operator for cache keys.
        /// </summary>
        public static bool operator ==(MethodCacheKey left, MethodCacheKey right) {
            return Equals(left, right);
        }

        /// <summary>
        /// Inequality operator for cache keys.
        /// </summary>
        public static bool operator !=(MethodCacheKey left, MethodCacheKey right) {
            return !Equals(left, right);
        }
    }
}
