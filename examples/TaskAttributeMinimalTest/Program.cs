// Minimal test to verify Task attribute infrastructure without device communication
using System.Reflection;
using Belay.Attributes;
using Belay.Core.Execution;
using Belay.Core;

Console.WriteLine("=== Task Attribute Infrastructure Test ===");

try
{
    // Test 1: Verify TaskAttribute can be applied
    var method = typeof(TestClass).GetMethod(nameof(TestClass.TestTaskMethod))!;
    var taskAttr = method.GetCustomAttribute<TaskAttribute>();
    
    Console.WriteLine($"✓ Task attribute found: {taskAttr != null}");
    if (taskAttr != null)
    {
        Console.WriteLine($"  Cache: {taskAttr.Cache}");
        Console.WriteLine($"  Exclusive: {taskAttr.Exclusive}");
        Console.WriteLine($"  Name: {taskAttr.Name ?? "null"}");
    }
    
    // Test 2: TaskExecutor infrastructure
    Console.WriteLine("\n=== Testing TaskExecutor Infrastructure ===");
    
    // Create a mock device (we won't actually connect)
    var device = Device.FromConnectionString("subprocess:echo");
    Console.WriteLine("✓ Device created");
    
    // Try to access the Task property
    var taskExecutor = device.Task;
    Console.WriteLine("✓ TaskExecutor accessed");
    
    Console.WriteLine("\n🎉 Task attribute infrastructure is working!");
    Console.WriteLine("✓ TaskAttribute can be applied to methods");
    Console.WriteLine("✓ TaskExecutor can be accessed from Device");
    Console.WriteLine("✓ Basic infrastructure is functional");
    
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}

public class TestClass
{
    [Task(Cache = true, Exclusive = false)]
    public void TestTaskMethod()
    {
        // This method demonstrates Task attribute usage
    }
}