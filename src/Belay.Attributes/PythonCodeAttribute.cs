// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Attributes;

/// <summary>
/// Specifies Python code to be executed on the MicroPython device when a method is called.
/// This attribute allows embedding Python code directly in the method declaration
/// without requiring the method to have an implementation body.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="PythonCodeAttribute"/> enables direct embedding of Python code
/// that will be executed on the connected MicroPython device. This is useful for
/// simple operations that don't require complex C# logic.
/// </para>
/// <para>
/// The Python code can include parameter placeholders using C# string interpolation
/// syntax (e.g., {parameterName}) that will be replaced with actual parameter values
/// at runtime.
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Simple Python Code Execution</strong></para>
/// <code>
/// public interface ILEDController
/// {
///     [Task]
///     [PythonCode(@"
///         import machine
///         led = machine.Pin(2, machine.Pin.OUT)
///         led.value(1)
///     ")]
///     Task TurnOnLEDAsync();
///
///     [Task]
///     [PythonCode(@"
///         import machine
///         led = machine.Pin({pin}, machine.Pin.OUT)
///         led.value({state})
///     ")]
///     Task SetLEDAsync(int pin, int state);
/// }
/// </code>
/// <para><strong>Python Code with Return Value</strong></para>
/// <code>
/// public interface ISensorReader
/// {
///     [Task]
///     [PythonCode(@"
///         import machine
///         import time
///         adc = machine.ADC(machine.Pin(26))
///         reading = adc.read_u16()
///         voltage = reading * 3.3 / 65535
///         temperature = 27 - (voltage - 0.706) / 0.001721
///         temperature
///     ")]
///     Task&lt;float&gt; ReadTemperatureAsync();
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PythonCodeAttribute : Attribute {
    /// <summary>
    /// Initializes a new instance of the <see cref="PythonCodeAttribute"/> class.
    /// </summary>
    /// <param name="code">The Python code to execute on the device.</param>
    /// <exception cref="ArgumentNullException">Thrown when code is null.</exception>
    /// <exception cref="ArgumentException">Thrown when code is empty or whitespace.</exception>
    public PythonCodeAttribute(string code) {
        if (code == null) {
            throw new ArgumentNullException(nameof(code));
        }

        if (string.IsNullOrWhiteSpace(code)) {
            throw new ArgumentException("Python code cannot be empty or whitespace.", nameof(code));
        }

        this.Code = code.Trim();
    }

    /// <summary>
    /// Gets the Python code to be executed on the device.
    /// </summary>
    /// <value>
    /// The Python code string that will be sent to the MicroPython device for execution.
    /// </value>
    public string Code { get; }

    /// <summary>
    /// Gets or sets a value indicating whether parameter values should be automatically
    /// substituted into the Python code using string interpolation.
    /// </summary>
    /// <value>
    /// <c>true</c> if parameter substitution is enabled; otherwise, <c>false</c>.
    /// Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// <para>
    /// When enabled, parameter placeholders in the Python code (e.g., {parameterName})
    /// will be replaced with the actual parameter values passed to the method.
    /// </para>
    /// <para>
    /// Parameter values are converted to Python-compatible representations:
    /// <list type="bullet">
    /// <item><description>Numbers are converted directly</description></item>
    /// <item><description>Strings are properly quoted and escaped</description></item>
    /// <item><description>Booleans are converted to Python True/False</description></item>
    /// <item><description>Collections are converted to Python lists/dictionaries</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Task]
    /// [PythonCode("led = machine.Pin({pin}, machine.Pin.OUT); led.value({state})",
    ///             EnableParameterSubstitution = true)]
    /// Task SetLED(int pin, bool state);
    ///
    /// // When called as: SetLED(2, true)
    /// // Executes: led = machine.Pin(2, machine.Pin.OUT); led.value(True)
    /// </code>
    /// </example>
    public bool EnableParameterSubstitution { get; set; } = true;

    /// <summary>
    /// Returns a string that represents the current <see cref="PythonCodeAttribute"/>.
    /// </summary>
    /// <returns>A string representation of the Python code attribute.</returns>
    public override string ToString() {
        var preview = this.Code.Length > 50 ? this.Code.Substring(0, 47) + "..." : this.Code;
        return $"[PythonCode(\"{preview.Replace("\"", "\\\"")}\""
               + (this.EnableParameterSubstitution ? string.Empty : ", EnableParameterSubstitution=false") + ")]";
    }
}
