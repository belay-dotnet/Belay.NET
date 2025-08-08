#!/usr/bin/env python3
"""
ESP32 Integration Test for Belay.NET DI Infrastructure
Tests the newly implemented dependency injection infrastructure with real ESP32 hardware.
"""

import subprocess
import tempfile
import os
import json
from datetime import datetime

def create_test_program():
    """Create a C# test program that uses the new DI infrastructure with ESP32."""
    return """
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Belay.Extensions;
using Belay.Extensions.Factories;
using Belay.Core;

class Program 
{
    static async Task Main(string[] args) 
    {
        var results = new {
            timestamp = DateTime.UtcNow,
            test_name = "ESP32_DI_Integration_Test",
            device_port = args.Length > 0 ? args[0] : "/dev/ttyACM1",
            tests = new List<object>()
        };

        try 
        {
            // Test 1: DI Container Setup
            Console.WriteLine("=== Test 1: DI Container Setup ===");
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            
            // Configure Belay.NET with our new DI infrastructure
            services.AddBelay(config => {
                config.Device.DefaultConnectionTimeoutMs = 10000;
                config.Communication.Serial.DefaultBaudRate = 115200;
                config.Session.MaxConcurrentSessions = 5;
            });

            var serviceProvider = services.BuildServiceProvider();
            
            // Verify factories are registered
            var deviceFactory = serviceProvider.GetBelayDeviceFactory();
            var executorFactory = serviceProvider.GetBelayExecutorFactory();
            
            Console.WriteLine("✅ DI Container configured successfully");
            Console.WriteLine($"   DeviceFactory: {deviceFactory.GetType().Name}");
            Console.WriteLine($"   ExecutorFactory: {executorFactory.GetType().Name}");
            
            results.tests.Add(new {
                name = "DI_Container_Setup",
                status = "PASSED",
                details = "Successfully configured DI container with Belay.NET services"
            });

            // Test 2: Device Creation via Factory
            Console.WriteLine("\\n=== Test 2: Device Creation via Factory ===");
            using var device = deviceFactory.CreateSerialDevice(results.device_port);
            Console.WriteLine($"✅ Device created via factory: {device.GetType().Name}");
            Console.WriteLine($"   Target port: {results.device_port}");
            
            results.tests.Add(new {
                name = "Device_Factory_Creation",
                status = "PASSED", 
                details = $"Successfully created device for {results.device_port}"
            });

            // Test 3: ESP32 Connection and Basic Communication
            Console.WriteLine("\\n=== Test 3: ESP32 Connection and Communication ===");
            await device.ConnectAsync();
            Console.WriteLine("✅ Connected to ESP32");

            // Test simple command execution
            var result1 = await device.ExecuteAsync<int>("2 + 3");
            Console.WriteLine($"✅ Basic math: 2 + 3 = {result1}");
            
            // Test string operation
            var result2 = await device.ExecuteAsync<string>("'ESP32 ' + 'DI Test'");
            Console.WriteLine($"✅ String concat: {result2}");

            // Test device identification
            var platformInfo = await device.ExecuteAsync<string>("import sys; sys.platform");
            Console.WriteLine($"✅ Platform: {platformInfo}");

            results.tests.Add(new {
                name = "ESP32_Basic_Communication",
                status = "PASSED",
                details = $"Successfully executed commands on {platformInfo}"
            });

            // Test 4: Executor Factory Usage
            Console.WriteLine("\\n=== Test 4: Executor Factory Usage ===");
            using var taskExecutor = executorFactory.CreateTaskExecutor(device);
            
            // Test executor-mediated execution
            var result3 = await taskExecutor.ApplyPoliciesAndExecuteAsync<string>(
                "import time; time.sleep(0.1); 'Executor Test Complete'");
            Console.WriteLine($"✅ Executor result: {result3}");

            results.tests.Add(new {
                name = "Executor_Factory_Usage",
                status = "PASSED",
                details = "Successfully used executor factory for task execution"
            });

            // Test 5: Configuration Integration
            Console.WriteLine("\\n=== Test 5: Configuration Integration ===");
            using var scope = serviceProvider.CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Belay.Extensions.Configuration.BelayConfiguration>>();
            
            Console.WriteLine($"✅ Connection timeout: {options.Value.Device.DefaultConnectionTimeoutMs}ms");
            Console.WriteLine($"✅ Baud rate: {options.Value.Communication.Serial.DefaultBaudRate}");
            Console.WriteLine($"✅ Max sessions: {options.Value.Session.MaxConcurrentSessions}");

            results.tests.Add(new {
                name = "Configuration_Integration",
                status = "PASSED",
                details = "Successfully accessed configured values via Options pattern"
            });

            await device.DisconnectAsync();
            Console.WriteLine("✅ Disconnected from ESP32");

            // Final Summary
            Console.WriteLine("\\n=== INTEGRATION TEST SUMMARY ===");
            Console.WriteLine($"✅ All tests PASSED");
            Console.WriteLine($"✅ DI Infrastructure working with ESP32");
            Console.WriteLine($"✅ Device: {results.device_port} ({platformInfo})");
            Console.WriteLine($"✅ Tests completed at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

            // Output JSON results
            Console.WriteLine("\\n" + System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\\n❌ TEST FAILED: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            results.tests.Add(new {
                name = "Integration_Test_Failure", 
                status = "FAILED",
                error = ex.Message,
                stack_trace = ex.StackTrace
            });
            
            Console.WriteLine("\\n" + System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Environment.Exit(1);
        }
    }
}
"""

def main():
    """Run the ESP32 DI integration test."""
    print("🚀 Starting ESP32 DI Integration Test...")
    print(f"   Timestamp: {datetime.utcnow().isoformat()}Z")
    print(f"   Target Device: /dev/ttyACM1")
    
    # Create temporary directory for test
    with tempfile.TemporaryDirectory() as temp_dir:
        # Create test program
        program_path = os.path.join(temp_dir, "Program.cs")
        with open(program_path, 'w') as f:
            f.write(create_test_program())
        
        # Create project file
        csproj_content = """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  
  <ItemGroup>
    <ProjectReference Include="/home/corona/belay.net/src/Belay.Core/Belay.Core.csproj" />
    <ProjectReference Include="/home/corona/belay.net/src/Belay.Extensions/Belay.Extensions.csproj" />
  </ItemGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
  </ItemGroup>
</Project>"""
        
        csproj_path = os.path.join(temp_dir, "TestProject.csproj")
        with open(csproj_path, 'w') as f:
            f.write(csproj_content)
        
        print(f"📁 Test project created in: {temp_dir}")
        
        # Build the test program
        print("🔨 Building test program...")
        build_result = subprocess.run([
            "dotnet", "build", csproj_path, "--configuration", "Release", "--verbosity", "quiet"
        ], capture_output=True, text=True, cwd=temp_dir)
        
        if build_result.returncode != 0:
            print(f"❌ Build failed:")
            print(f"   stdout: {build_result.stdout}")
            print(f"   stderr: {build_result.stderr}")
            return False
        
        print("✅ Test program built successfully")
        
        # Run the test program
        print("▶️  Running ESP32 DI integration test...")
        test_result = subprocess.run([
            "dotnet", "run", "--project", csproj_path, "--configuration", "Release", 
            "--", "/dev/ttyACM1"
        ], capture_output=True, text=True, cwd=temp_dir, timeout=60)
        
        print("\n" + "="*60)
        print("TEST OUTPUT:")
        print("="*60)
        print(test_result.stdout)
        
        if test_result.stderr:
            print("\nSTDERR:")
            print(test_result.stderr)
        
        print("="*60)
        print(f"Return code: {test_result.returncode}")
        
        return test_result.returncode == 0

if __name__ == "__main__":
    success = main()
    if success:
        print("\n🎉 ESP32 DI Integration Test PASSED!")
    else:
        print("\n💥 ESP32 DI Integration Test FAILED!")
    exit(0 if success else 1)