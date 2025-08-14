// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;

namespace Belay.Core.Execution;

/// <summary>
/// Represents the execution context for a method call, containing all necessary
/// information for executors to process attribute-decorated methods.
/// </summary>
public sealed class ExecutionContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionContext"/> class.
    /// </summary>
    /// <param name="method">The method to execute.</param>
    /// <param name="arguments">The arguments for the method.</param>
    /// <param name="device">The device connection to execute on.</param>
    /// <param name="instance">The instance object if this is an instance method; null for static methods.</param>
    public ExecutionContext(
        MethodInfo method,
        object[] arguments,
        IDeviceConnection device,
        object? instance = null)
    {
        Method = method ?? throw new ArgumentNullException(nameof(method));
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        Device = device ?? throw new ArgumentNullException(nameof(device));
        Instance = instance;
        Properties = new Dictionary<string, object>();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the method to execute.
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// Gets the arguments for the method execution.
    /// </summary>
    public object[] Arguments { get; }

    /// <summary>
    /// Gets the device connection to execute on.
    /// </summary>
    public IDeviceConnection Device { get; }

    /// <summary>
    /// Gets the instance object for instance methods, or null for static methods.
    /// </summary>
    public object? Instance { get; }

    /// <summary>
    /// Gets a dictionary for storing additional properties during execution.
    /// This can be used by executors to pass context between execution stages.
    /// </summary>
    public Dictionary<string, object> Properties { get; }

    /// <summary>
    /// Gets the timestamp when this execution context was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the method name for convenience.
    /// </summary>
    public string MethodName => Method.Name;

    /// <summary>
    /// Gets the declaring type of the method for convenience.
    /// </summary>
    public Type? DeclaringType => Method.DeclaringType;

    /// <summary>
    /// Gets or sets the execution timeout for this context.
    /// Individual executors may override this based on attribute configuration.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Gets or sets whether caching should be used for this execution.
    /// Individual executors may override this based on attribute configuration.
    /// </summary>
    public bool UseCache { get; set; }

    /// <summary>
    /// Gets or sets whether this execution requires exclusive access to the device.
    /// Used by thread and setup/teardown executors.
    /// </summary>
    public bool RequiresExclusiveAccess { get; set; }
}