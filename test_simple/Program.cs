// Simple test of the key Python code generation logic
using System.Reflection;
using Belay.Attributes;

Console.WriteLine("Testing enhanced Python code generation logic...");

try {
    // Test the Strategy 2 method (returns Python code string)
    var codeGenMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.GetTemperaturePythonCode))!;
    Console.WriteLine($"\n=== Testing Strategy 2: Method returns Python code ===");
    Console.WriteLine($"Method: {codeGenMethod.Name}");
    Console.WriteLine($"Return type: {codeGenMethod.ReturnType}");
    Console.WriteLine($"Has [Task] attribute: {codeGenMethod.HasAttribute<TaskAttribute>()}");
    
    // Invoke the method to get Python code (Strategy 2)
    var result = codeGenMethod.Invoke(null, new object[] { 42 });
    Console.WriteLine($"Generated Python code: {result}");
    
    // Test the Strategy 3 method (deployable method)
    var deployableMethod = typeof(TestMethods).GetMethod(nameof(TestMethods.SimpleTaskMethod))!;
    Console.WriteLine($"\n=== Testing Strategy 3: Deployable method ===");
    Console.WriteLine($"Method: {deployableMethod.Name}");
    Console.WriteLine($"Return type: {deployableMethod.ReturnType}");
    Console.WriteLine($"Has [Task] attribute: {deployableMethod.HasAttribute<TaskAttribute>()}");
    
    // For deployable methods, we'd generate: method_name(parameters)
    var deviceMethodName = deployableMethod.GetDeviceMethodName();
    Console.WriteLine($"Device method name: {deviceMethodName}");
    
    // Test parameter conversion
    Console.WriteLine($"\n=== Testing Parameter Conversion ===");
    
    var testParams = new object?[] {
        null,
        true,
        false,
        42,
        3.14,
        "hello world",
        "string with 'quotes' and \"double quotes\"",
        new int[] { 1, 2, 3, 4 },
        new Dictionary<string, object> {
            { "name", "sensor1" },
            { "value", 25.5 },
            { "enabled", true }
        },
        new byte[] { 0x01, 0x02, 0xFF }
    };
    
    foreach (var param in testParams) {
        var converted = ConvertToPythonValue(param);
        Console.WriteLine($"{param?.GetType().Name ?? "null",-15} -> {converted}");
    }
    
    Console.WriteLine("\n✓ Python code generation logic validation completed!");

} catch (Exception ex) {
    Console.WriteLine($"Test failed: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
}

// Simplified version of the conversion logic from BaseExecutor
static string ConvertToPythonValue(object? value) {
    return value switch {
        null => "None",
        bool b => b ? "True" : "False",
        byte or sbyte or short or ushort or int or uint => value.ToString()!,
        long or ulong => value.ToString()!,
        float f => f.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
        double d => d.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
        decimal dec => dec.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
        string s => ConvertStringToPython(s),
        byte[] bytes => ConvertBytesToPython(bytes),
        System.Collections.IList list => ConvertListToPython(list),
        System.Collections.IDictionary dict => ConvertDictionaryToPython(dict),
        _ => $"'{value}'"
    };
}

static string ConvertStringToPython(string str) {
    var escaped = str
        .Replace("\\", "\\\\")  // Backslash first
        .Replace("'", "\\'")    // Single quotes
        .Replace("\"", "\\\"")  // Double quotes  
        .Replace("\n", "\\n")   // Newline
        .Replace("\r", "\\r")   // Carriage return
        .Replace("\t", "\\t");  // Tab
        
    return $"'{escaped}'";
}

static string ConvertBytesToPython(byte[] bytes) {
    var hex = Convert.ToHexString(bytes);
    return $"bytes.fromhex('{hex}')";
}

static string ConvertListToPython(System.Collections.IList list) {
    var items = new List<string>();
    foreach (var item in list) {
        items.Add(ConvertToPythonValue(item));
    }
    return $"[{string.Join(", ", items)}]";
}

static string ConvertDictionaryToPython(System.Collections.IDictionary dict) {
    var pairs = new List<string>();
    foreach (System.Collections.DictionaryEntry entry in dict) {
        var key = ConvertToPythonValue(entry.Key);
        var value = ConvertToPythonValue(entry.Value);
        pairs.Add($"{key}: {value}");
    }
    return $"{{{string.Join(", ", pairs)}}}";
}

public class TestMethods {
    [Task(Cache = true)]
    public static string GetTemperaturePythonCode(int sensorId) {
        // Strategy 2: Method returns Python code to execute
        return $"print('Reading temperature from sensor {sensorId}'); 25.5";
    }

    [Task]
    public static float SimpleTaskMethod(int value) {
        // Strategy 3: Method would be deployed to device  
        return value * 2.5f;
    }
}

// Extension methods from Belay.Attributes for testing
static class Extensions {
    public static bool HasAttribute<T>(this MethodInfo method) where T : Attribute {
        return method.GetCustomAttribute<T>() != null;
    }
    
    public static string GetDeviceMethodName(this MethodInfo method) {
        // Convert PascalCase to snake_case for Python
        var name = method.Name;
        var result = System.Text.RegularExpressions.Regex.Replace(name, 
            "([a-z0-9])([A-Z])", "$1_$2").ToLower();
        return result;
    }
}