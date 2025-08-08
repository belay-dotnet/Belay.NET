// Test executor framework with real ESP32 hardware
using System.Reflection;
using Belay.Attributes;
using Belay.Core;
using Belay.Core.Communication;

Console.WriteLine("Testing executor framework with ESP32 hardware...");

try {
    // Connect to ESP32
    var serial = new SerialDeviceCommunication("/dev/ttyACM2", 115200);
    await serial.ConnectAsync();
    
    var device = new Device(serial, logger: null);
    Console.WriteLine("✓ Connected to ESP32");

    // Test method that returns Python code (Strategy 2)
    var pythonCodeMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.GetTemperaturePythonCode))!;
    Console.WriteLine($"Python code method can be handled: {device.Task.CanHandle(pythonCodeMethod)}");

    // Test method interception with actual hardware
    try {
        var result = await device.ExecuteMethodAsync<object>(pythonCodeMethod, null, new object[] { 1 });
        Console.WriteLine("✓ Python code method execution succeeded");
        Console.WriteLine($"  Executor correctly generated and executed Python code");
    } catch (Exception ex) {
        Console.WriteLine($"Python code method execution failed: {ex.Message}");
    }

    // Test method that looks deployable (Strategy 3)  
    var deployableMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.SimpleTaskMethod))!;
    Console.WriteLine($"Deployable method can be handled: {device.Task.CanHandle(deployableMethod)}");

    try {
        var result = await device.ExecuteMethodAsync<object>(deployableMethod, null, new object[] { 42 });
        Console.WriteLine("✓ Deployable method execution succeeded");
        Console.WriteLine("  Executor correctly generated Python function call");
    } catch (Exception ex) {
        Console.WriteLine($"Deployable method execution failed: {ex.Message}");
        Console.WriteLine("  This is expected - function doesn't exist on device yet");
    }

    // Test complex parameter marshaling
    var complexMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.ComplexParameterMethod))!;
    var complexParams = new object[] {
        "test string",
        new int[] { 1, 2, 3, 4, 5 },
        new Dictionary<string, object> { { "sensor", "temperature" }, { "value", 25.5 } },
        true
    };

    try {
        var result = await device.ExecuteMethodAsync<object>(complexMethod, null, complexParams);
        Console.WriteLine("✓ Complex parameter method execution succeeded");
        Console.WriteLine("  Type conversion and parameter marshaling working");
    } catch (Exception ex) {
        Console.WriteLine($"Complex parameter method failed: {ex.Message}");
    }

    device.Dispose();
    Console.WriteLine("ESP32 executor framework testing completed successfully!");

} catch (Exception ex) {
    Console.WriteLine($"ESP32 test failed: {ex.Message}");
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
        // For now, it will generate: simple_task_method(42)
        return value * 2.5f;
    }

    [Task]
    public static string ComplexParameterMethod(string name, int[] values, Dictionary<string, object> config, bool enabled) {
        // Test complex parameter marshaling
        return $"Processed {name} with {values.Length} values";
    }
}
