// Test Python code generation without device execution
using System.Reflection;
using Belay.Attributes;
using Belay.Core.Execution;
using Microsoft.Extensions.Logging;

Console.WriteLine("Testing Python code generation (no device execution)...");

try {
    // Create a simple test executor that exposes the protected methods
    var testExecutor = new TestExecutor();

    // Test methods from our test class
    var methods = typeof(TestMethods).GetMethods(BindingFlags.Public | BindingFlags.Static);
    
    foreach (var method in methods) {
        if (method.HasAttribute<TaskAttribute>()) {
            Console.WriteLine($"\n=== Testing method: {method.Name} ===");
            
            try {
                object?[]? parameters = method.Name switch {
                    nameof(TestMethods.GetTemperaturePythonCode) => new object?[] { 1 },
                    nameof(TestMethods.SimpleTaskMethod) => new object?[] { 42 },
                    nameof(TestMethods.ComplexParameterMethod) => new object?[] {
                        "test",
                        new int[] { 1, 2, 3 },
                        new Dictionary<string, object> { { "key", "value" } },
                        true
                    },
                    _ => null
                };
                
                // Test the Python code generation
                var pythonCode = testExecutor.TestGeneratePythonMethodCall(method, null, parameters);
                Console.WriteLine($"Generated Python code: {pythonCode}");
                
                // Test parameter conversion
                if (parameters != null) {
                    var paramList = testExecutor.TestGenerateParameterList(parameters);
                    Console.WriteLine($"Parameter list: [{paramList}]");
                    
                    // Test individual parameter conversions
                    Console.WriteLine("Individual parameter conversions:");
                    for (int i = 0; i < parameters.Length; i++) {
                        var converted = testExecutor.TestConvertToPythonValue(parameters[i]);
                        Console.WriteLine($"  [{i}] {parameters[i]?.GetType().Name ?? "null"}: {converted}");
                    }
                }
                
            } catch (Exception ex) {
                Console.WriteLine($"Code generation failed: {ex.Message}");
                if (ex.InnerException != null) {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }
    }

    Console.WriteLine("\n✓ Python code generation testing completed!");

} catch (Exception ex) {
    Console.WriteLine($"Test failed: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

public class TestMethods {
    [Task(Cache = true)]
    public static string GetTemperaturePythonCode(int sensorId) {
        // This method returns Python code to execute (Strategy 2)
        return $"print('Reading temperature from sensor {sensorId}'); 25.5";
    }

    [Task]
    public static float SimpleTaskMethod(int value) {
        // This method would be deployed to device (Strategy 3)  
        return value * 2.5f;
    }

    [Task]
    public static string ComplexParameterMethod(string name, int[] values, Dictionary<string, object> config, bool enabled) {
        // Test complex parameter marshaling
        return $"Processed {name} with {values.Length} values";
    }
}

// Test wrapper that exposes protected methods from BaseExecutor
public class TestExecutor : BaseExecutor {
    private static readonly Microsoft.Extensions.Logging.ILogger TestLogger = 
        Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    public TestExecutor() : base(
        device: null!, 
        sessionManager: null!, 
        logger: TestLogger, 
        errorMapper: null) {
    }

    // Expose the protected methods for testing
    public string TestGeneratePythonMethodCall(MethodInfo method, object? instance, object?[]? parameters) {
        return GeneratePythonMethodCall(method, instance, parameters);
    }

    public string TestGenerateParameterList(object?[]? parameters) {
        return GenerateParameterList(parameters);
    }

    public string TestConvertToPythonValue(object? value) {
        return ConvertToPythonValue(value);
    }

    // Required abstract implementations (not used in this test)
    public override Task<T> ApplyPoliciesAndExecuteAsync<T>(string pythonCode, System.Threading.CancellationToken cancellationToken = default, [System.Runtime.CompilerServices.CallerMemberName] string? callingMethod = null) {
        throw new NotImplementedException();
    }

    public override bool CanHandle(MethodInfo method) {
        return method?.HasAttribute<TaskAttribute>() == true;
    }
}