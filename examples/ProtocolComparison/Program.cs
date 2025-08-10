// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using Belay.Core;
using System.Diagnostics;

Console.WriteLine("=== Raw REPL Protocol Comparison ===");
Console.WriteLine("Comparing subprocess vs hardware Raw REPL behavior");
Console.WriteLine();

if (args.Length < 2)
{
    Console.WriteLine("Usage: ProtocolComparison <subprocess_path> <hardware_connection>");
    Console.WriteLine("Example: ProtocolComparison ../../micropython/ports/unix/build-standard/micropython serial:/dev/ttyACM0");
    return;
}

var subprocessPath = args[0];
var hardwareConnection = args[1];

var testCases = new[]
{
    new { Name = "Simple Print", Code = "print('Hello World')", ExpectedOutput = "Hello World" },
    new { Name = "Math Expression", Code = "print(25 + 17)", ExpectedOutput = "42" },
    new { Name = "Variable Assignment", Code = "x = 100; print(x * 2)", ExpectedOutput = "200" },
    new { Name = "Multi-line Code", Code = "for i in range(3):\n    print(f'Line {i}')", ExpectedOutput = "Line 0\nLine 1\nLine 2" },
    new { Name = "Large Output", Code = "print('x' * 500)", ExpectedOutput = new string('x', 500) },
    new { Name = "JSON Output", Code = "import json; print(json.dumps({'test': 42, 'array': [1,2,3]}))", ExpectedOutput = "{\"test\": 42, \"array\": [1, 2, 3]}" }
};

Console.WriteLine("Running protocol comparison tests...\n");

var results = new List<ComparisonResult>();

foreach (var testCase in testCases)
{
    Console.WriteLine($"Testing: {testCase.Name}");
    
    var result = new ComparisonResult
    {
        TestName = testCase.Name,
        TestCode = testCase.Code,
        ExpectedOutput = testCase.ExpectedOutput
    };
    
    // Test subprocess
    try
    {
        using var subprocessDevice = Device.FromConnectionString($"subprocess:{subprocessPath}");
        
        var stopwatch = Stopwatch.StartNew();
        await subprocessDevice.ConnectAsync();
        var subprocessResult = await subprocessDevice.ExecuteAsync(testCase.Code);
        stopwatch.Stop();
        
        result.SubprocessOutput = subprocessResult.Trim();
        result.SubprocessTime = stopwatch.ElapsedMilliseconds;
        result.SubprocessSuccess = true;
        
        await subprocessDevice.DisconnectAsync();
    }
    catch (Exception ex)
    {
        result.SubprocessOutput = $"ERROR: {ex.Message}";
        result.SubprocessSuccess = false;
    }
    
    // Test hardware
    try
    {
        using var hardwareDevice = Device.FromConnectionString(hardwareConnection);
        
        var stopwatch = Stopwatch.StartNew();
        await hardwareDevice.ConnectAsync();
        var hardwareResult = await hardwareDevice.ExecuteAsync(testCase.Code);
        stopwatch.Stop();
        
        result.HardwareOutput = hardwareResult.Trim();
        result.HardwareTime = stopwatch.ElapsedMilliseconds;
        result.HardwareSuccess = true;
        
        await hardwareDevice.DisconnectAsync();
    }
    catch (Exception ex)
    {
        result.HardwareOutput = $"ERROR: {ex.Message}";
        result.HardwareSuccess = false;
    }
    
    results.Add(result);
    
    // Display immediate results
    Console.WriteLine($"  Subprocess: {(result.SubprocessSuccess ? "✓" : "✗")} ({result.SubprocessTime}ms)");
    Console.WriteLine($"  Hardware:   {(result.HardwareSuccess ? "✓" : "✗")} ({result.HardwareTime}ms)");
    Console.WriteLine();
}

// Generate comparison report
Console.WriteLine("=== PROTOCOL COMPARISON REPORT ===\n");

foreach (var result in results)
{
    Console.WriteLine($"## {result.TestName}");
    Console.WriteLine($"**Test Code:** `{result.TestCode}`");
    Console.WriteLine();
    
    Console.WriteLine("### Results:");
    Console.WriteLine($"- **Subprocess**: {(result.SubprocessSuccess ? "PASS" : "FAIL")} ({result.SubprocessTime}ms)");
    Console.WriteLine($"- **Hardware**: {(result.HardwareSuccess ? "PASS" : "FAIL")} ({result.HardwareTime}ms)");
    Console.WriteLine();
    
    if (result.SubprocessSuccess && result.HardwareSuccess)
    {
        var outputMatch = result.SubprocessOutput == result.HardwareOutput;
        Console.WriteLine($"### Output Comparison: {(outputMatch ? "IDENTICAL" : "DIFFERENT")}");
        
        if (!outputMatch)
        {
            Console.WriteLine($"**Subprocess Output:** `{result.SubprocessOutput}`");
            Console.WriteLine($"**Hardware Output:** `{result.HardwareOutput}`");
        }
        else
        {
            Console.WriteLine($"**Output:** `{result.SubprocessOutput}`");
        }
        
        var timeDiff = Math.Abs(result.HardwareTime - result.SubprocessTime);
        var percentDiff = result.SubprocessTime > 0 ? (timeDiff * 100.0 / result.SubprocessTime) : 0;
        Console.WriteLine($"**Performance:** Hardware {(result.HardwareTime > result.SubprocessTime ? "slower" : "faster")} by {timeDiff}ms ({percentDiff:F1}%)");
    }
    
    Console.WriteLine();
}

// Summary statistics
var successfulTests = results.Count(r => r.SubprocessSuccess && r.HardwareSuccess);
var identicalOutputs = results.Count(r => r.SubprocessSuccess && r.HardwareSuccess && r.SubprocessOutput == r.HardwareOutput);

var avgSubprocessTime = results.Where(r => r.SubprocessSuccess).Average(r => r.SubprocessTime);
var avgHardwareTime = results.Where(r => r.HardwareSuccess).Average(r => r.HardwareTime);

Console.WriteLine("=== SUMMARY ===");
Console.WriteLine($"Total Tests: {results.Count}");
Console.WriteLine($"Successful on Both: {successfulTests} ({successfulTests * 100.0 / results.Count:F1}%)");
Console.WriteLine($"Identical Outputs: {identicalOutputs} ({identicalOutputs * 100.0 / successfulTests:F1}%)");
Console.WriteLine($"Average Subprocess Time: {avgSubprocessTime:F1}ms");
Console.WriteLine($"Average Hardware Time: {avgHardwareTime:F1}ms");
Console.WriteLine($"Performance Ratio: {(avgHardwareTime / avgSubprocessTime):F2}x");

var protocolCompatibility = identicalOutputs == successfulTests;
Console.WriteLine($"\n**Protocol Compatibility: {(protocolCompatibility ? "FULL" : "PARTIAL")}**");

if (protocolCompatibility)
{
    Console.WriteLine("✅ Hardware and subprocess protocols are fully compatible");
}
else
{
    Console.WriteLine("⚠️  Some protocol differences detected - review individual test results");
}

public class ComparisonResult
{
    public string TestName { get; set; } = "";
    public string TestCode { get; set; } = "";
    public string ExpectedOutput { get; set; } = "";
    
    public string SubprocessOutput { get; set; } = "";
    public long SubprocessTime { get; set; }
    public bool SubprocessSuccess { get; set; }
    
    public string HardwareOutput { get; set; } = "";
    public long HardwareTime { get; set; }
    public bool HardwareSuccess { get; set; }
}