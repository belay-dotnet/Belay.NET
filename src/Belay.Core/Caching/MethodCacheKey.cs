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
using System.Security.Cryptography;
using System.Text;

namespace Belay.Core.Caching
{
    /// <summary>
    /// Represents a unique cache key for method deployment based on device and method characteristics.
    /// </summary>
    public sealed class MethodCacheKey : IEquatable<MethodCacheKey>
    {
        /// <summary>
        /// Gets the unique hash representing the method cache key.
        /// </summary>
        public string Hash { get; }

        /// <summary>
        /// Creates a new method cache key from device and method details.
        /// </summary>
        /// <param name="deviceId">Unique identifier for the device</param>
        /// <param name="firmwareVersion">Firmware version of the device</param>
        /// <param name="methodSignature">Unique method signature or content hash</param>
        public MethodCacheKey(string deviceId, string firmwareVersion, string methodSignature)
        {
            Hash = GenerateHash(deviceId, firmwareVersion, methodSignature);
        }

        /// <summary>
        /// Generates a deterministic SHA-256 hash for the cache key.
        /// </summary>
        private static string GenerateHash(string deviceId, string firmwareVersion, string methodSignature)
        {
            using var sha256 = SHA256.Create();
            var combinedInput = $"{deviceId}|{firmwareVersion}|{methodSignature}";
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedInput));
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Determines whether the current cache key is equal to another.
        /// </summary>
        public bool Equals(MethodCacheKey other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Hash == other.Hash;
        }

        /// <summary>
        /// Determines whether the current cache key is equal to another object.
        /// </summary>
        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || (obj is MethodCacheKey other && Equals(other));
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        public override int GetHashCode()
        {
            return Hash?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Provides a string representation of the cache key.
        /// </summary>
        public override string ToString() => Hash;

        /// <summary>
        /// Equality operator for cache keys.
        /// </summary>
        public static bool operator ==(MethodCacheKey left, MethodCacheKey right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Inequality operator for cache keys.
        /// </summary>
        public static bool operator !=(MethodCacheKey left, MethodCacheKey right)
        {
            return !Equals(left, right);
        }
    }
}