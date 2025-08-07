// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for method executors that handle attribute-based method execution.
    /// Executors intercept method calls and apply attribute-specific policies.
    /// </summary>
    public interface IExecutor {
        /// <summary>
        /// Executes a method with attribute-specific policies applied.
        /// </summary>
        /// <typeparam name="T">The expected return type.</typeparam>
        /// <param name="method">The method to execute.</param>
        /// <param name="instance">The instance to invoke the method on (null for static methods).</param>
        /// <param name="parameters">The parameters to pass to the method.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
        /// <returns>The result of the method execution.</returns>
        Task<T> ExecuteAsync<T>(MethodInfo method, object? instance, object?[]? parameters = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a method without returning a value.
        /// </summary>
        /// <param name="method">The method to execute.</param>
        /// <param name="instance">The instance to invoke the method on (null for static methods).</param>
        /// <param name="parameters">The parameters to pass to the method.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the execution.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        Task ExecuteAsync(MethodInfo method, object? instance, object?[]? parameters = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that a method can be handled by this executor.
        /// </summary>
        /// <param name="method">The method to validate.</param>
        /// <returns>True if the method can be handled, false otherwise.</returns>
        bool CanHandle(MethodInfo method);
    }

    /// <summary>
    /// Extension methods for MethodInfo to support the executor framework.
    /// </summary>
    public static class MethodInfoExtensions {
        /// <summary>
        /// Gets the device method name for a method, using custom name if specified in attributes.
        /// </summary>
        /// <param name="method">The method to get the device name for.</param>
        /// <returns>The name to use on the device.</returns>
        public static string GetDeviceMethodName(this MethodInfo method) {
            // Check for custom names in attributes that support Name property
            var taskAttribute = method.GetCustomAttribute<Belay.Attributes.TaskAttribute>();
            if (!string.IsNullOrEmpty(taskAttribute?.Name)) {
                return taskAttribute.Name;
            }

            var threadAttribute = method.GetCustomAttribute<Belay.Attributes.ThreadAttribute>();
            if (!string.IsNullOrEmpty(threadAttribute?.Name)) {
                return threadAttribute.Name;
            }

            // Note: SetupAttribute and TeardownAttribute do not have Name properties
            // They use the default method name conversion

            // Default to method name converted to snake_case for Python convention
            return ToSnakeCase(method.Name);
        }

        /// <summary>
        /// Generates a simple hash of the method signature for caching.
        /// </summary>
        /// <param name="method">The method to hash.</param>
        /// <returns>A hash string representing the method signature.</returns>
        public static string GetSignatureHash(this MethodInfo method) {
            // Simple hash using method name and parameter types - much faster than SHA256
            var signature = $"{method.DeclaringType?.FullName}.{method.Name}({string.Join(",", method.GetParameters().Select(p => p.ParameterType.Name))}):{method.ReturnType.Name}";
            return signature.GetHashCode().ToString("X8");
        }

        /// <summary>
        /// Checks if a method has a specific attribute.
        /// </summary>
        /// <typeparam name="T">The attribute type to check for.</typeparam>
        /// <param name="method">The method to check.</param>
        /// <returns>True if the method has the attribute, false otherwise.</returns>
        public static bool HasAttribute<T>(this MethodInfo method)
            where T : Attribute
        {
            return method.GetCustomAttribute<T>() != null;
        }

        /// <summary>
        /// Gets a specific attribute from a method.
        /// </summary>
        /// <typeparam name="T">The attribute type to get.</typeparam>
        /// <param name="method">The method to get the attribute from.</param>
        /// <returns>The attribute instance, or null if not found.</returns>
        public static T? GetAttribute<T>(this MethodInfo method)
            where T : Attribute
        {
            return method.GetCustomAttribute<T>();
        }

        /// <summary>
        /// Converts a C# method name to Python snake_case convention.
        /// </summary>
        /// <param name="name">The method name to convert.</param>
        /// <returns>The name in snake_case format.</returns>
        private static string ToSnakeCase(string name) {
            if (string.IsNullOrEmpty(name)) {
                return name;
            }

            var result = new System.Text.StringBuilder();

            for (int i = 0; i < name.Length; i++) {
                if (i > 0 && char.IsUpper(name[i])) {
                    result.Append('_');
                }

                result.Append(char.ToLower(name[i]));
            }

            return result.ToString();
        }
    }
}
