// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System;
    using System.Reflection;
    using Belay.Attributes;

    /// <summary>
    /// Provides method execution context without relying on stack trace inspection.
    /// This replaces the security-vulnerable stack frame reflection pattern.
    /// </summary>
    public interface IMethodExecutionContext {
        /// <summary>
        /// Gets the method being executed.
        /// </summary>
        MethodInfo? Method { get; }

        /// <summary>
        /// Gets the method name being executed.
        /// </summary>
        string? MethodName { get; }

        /// <summary>
        /// Gets the Task attribute if present.
        /// </summary>
        TaskAttribute? TaskAttribute { get; }

        /// <summary>
        /// Gets the Setup attribute if present.
        /// </summary>
        SetupAttribute? SetupAttribute { get; }

        /// <summary>
        /// Gets the Thread attribute if present.
        /// </summary>
        ThreadAttribute? ThreadAttribute { get; }

        /// <summary>
        /// Gets the Teardown attribute if present.
        /// </summary>
        TeardownAttribute? TeardownAttribute { get; }

        /// <summary>
        /// Gets the parameters passed to the method.
        /// </summary>
        object?[]? Parameters { get; }

        /// <summary>
        /// Gets the instance the method is being called on (null for static methods).
        /// </summary>
        object? Instance { get; }
    }

    /// <summary>
    /// Implementation of method execution context.
    /// </summary>
    public sealed class MethodExecutionContext : IMethodExecutionContext {
        /// <inheritdoc />
        public MethodInfo? Method { get; }

        /// <inheritdoc />
        public string? MethodName { get; }

        /// <inheritdoc />
        public TaskAttribute? TaskAttribute { get; }

        /// <inheritdoc />
        public SetupAttribute? SetupAttribute { get; }

        /// <inheritdoc />
        public ThreadAttribute? ThreadAttribute { get; }

        /// <inheritdoc />
        public TeardownAttribute? TeardownAttribute { get; }

        /// <inheritdoc />
        public object?[]? Parameters { get; }

        /// <inheritdoc />
        public object? Instance { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MethodExecutionContext"/> class.
        /// </summary>
        /// <param name="method">The method being executed.</param>
        /// <param name="instance">The instance the method is called on.</param>
        /// <param name="parameters">The parameters passed to the method.</param>
        /// <param name="methodName">Override for the method name if needed.</param>
        public MethodExecutionContext(MethodInfo? method, object? instance = null, object?[]? parameters = null, string? methodName = null) {
            this.Method = method;
            this.Instance = instance;
            this.Parameters = parameters;
            this.MethodName = methodName ?? method?.Name;

            // Extract attributes for fast access
            this.TaskAttribute = method?.GetAttribute<TaskAttribute>();
            this.SetupAttribute = method?.GetAttribute<SetupAttribute>();
            this.ThreadAttribute = method?.GetAttribute<ThreadAttribute>();
            this.TeardownAttribute = method?.GetAttribute<TeardownAttribute>();
        }

        /// <summary>
        /// Creates a context for a method call without reflection data.
        /// </summary>
        /// <param name="methodName">The name of the method being called.</param>
        /// <param name="parameters">The parameters passed to the method.</param>
        /// <returns>A method execution context.</returns>
        public static MethodExecutionContext ForMethodName(string methodName, object?[]? parameters = null) {
            return new MethodExecutionContext(null, null, parameters, methodName);
        }

        /// <summary>
        /// Creates a context with explicit attribute information.
        /// </summary>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="taskAttribute">Task attribute if present.</param>
        /// <param name="parameters">Method parameters.</param>
        /// <returns>A method execution context.</returns>
        public static IMethodExecutionContext WithTaskAttribute(string methodName, TaskAttribute taskAttribute, object?[]? parameters = null) {
            return new TaskMethodExecutionContext(methodName, taskAttribute, parameters);
        }
    }

    /// <summary>
    /// Specialized context for Task attribute methods.
    /// </summary>
    internal sealed class TaskMethodExecutionContext : IMethodExecutionContext {
        /// <inheritdoc/>
        public MethodInfo? Method => null;

        /// <inheritdoc/>
        public string? MethodName { get; }

        /// <inheritdoc/>
        public TaskAttribute? TaskAttribute { get; }

        /// <inheritdoc/>
        public SetupAttribute? SetupAttribute => null;

        /// <inheritdoc/>
        public ThreadAttribute? ThreadAttribute => null;

        /// <inheritdoc/>
        public TeardownAttribute? TeardownAttribute => null;

        /// <inheritdoc/>
        public object?[]? Parameters { get; }

        /// <inheritdoc/>
        public object? Instance => null;

        internal TaskMethodExecutionContext(string methodName, TaskAttribute taskAttribute, object?[]? parameters) {
            this.MethodName = methodName;
            this.TaskAttribute = taskAttribute;
            this.Parameters = parameters;
        }
    }
}
