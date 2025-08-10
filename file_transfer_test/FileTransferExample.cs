// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using Belay.Core;
using Belay.Attributes;
using Belay.Sync;
using Microsoft.Extensions.Logging;

namespace FileTransferExample;

/// <summary>
/// Comprehensive example demonstrating file transfer capabilities with Task attributes.
/// This example shows how to combine file system operations with attribute-based method execution.
/// </summary>
public class DeviceFileManager {
    private readonly Device device;
    private readonly ILogger<DeviceFileManager> logger;

    public DeviceFileManager(Device device, ILogger<DeviceFileManager>? logger = null) {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
        this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DeviceFileManager>.Instance;
    }

    /// <summary>
    /// Demonstrates basic file operations using the FileSystem interface.
    /// </summary>
    public async Task DemonstrateBasicFileOperationsAsync() {
        this.logger.LogInformation("Starting basic file operations demonstration");

        // Create a test directory
        await this.device.FileSystem().CreateDirectoryAsync("/test_files", recursive: true);
        this.logger.LogInformation("Created directory: /test_files");

        // Write a test file
        string testContent = "Hello from Belay.NET!\nThis is a test file created from the host.";
        await this.device.FileSystem().WriteTextFileAsync("/test_files/hello.txt", testContent);
        this.logger.LogInformation("Created file: /test_files/hello.txt");

        // Read the file back
        string readContent = await this.device.FileSystem().ReadTextFileAsync("/test_files/hello.txt");
        this.logger.LogInformation("Read file content: {Content}", readContent);

        // Get file information
        var fileInfo = await this.device.FileSystem().GetFileInfoAsync("/test_files/hello.txt");
        if (fileInfo != null) {
            this.logger.LogInformation("File info - Size: {Size} bytes, Modified: {Modified}", 
                fileInfo.Size, fileInfo.LastModified);
        }

        // List directory contents
        var entries = await this.device.FileSystem().ListAsync("/test_files");
        this.logger.LogInformation("Directory contents:");
        foreach (var entry in entries) {
            this.logger.LogInformation("  {Path} ({Type})", entry.Path, entry.IsDirectory ? "DIR" : "FILE");
        }

        // Calculate checksum
        string checksum = await this.device.FileSystem().CalculateChecksumAsync("/test_files/hello.txt", "md5");
        this.logger.LogInformation("File checksum (MD5): {Checksum}", checksum);

        // Clean up
        await this.device.FileSystem().DeleteFileAsync("/test_files/hello.txt");
        await this.device.FileSystem().DeleteDirectoryAsync("/test_files");
        this.logger.LogInformation("Cleaned up test files");
    }

    /// <summary>
    /// Demonstrates large file transfer using chunked operations.
    /// </summary>
    public async Task DemonstrateLargeFileTransferAsync() {
        this.logger.LogInformation("Starting large file transfer demonstration");

        // Create a large test file (10KB)
        var largeData = new byte[10240];
        Random.Shared.NextBytes(largeData);

        await this.device.FileSystem().CreateDirectoryAsync("/large_files", recursive: true);
        
        // Write large file (will use chunked transfer automatically)
        await this.device.FileSystem().WriteFileAsync("/large_files/large_test.bin", largeData);
        this.logger.LogInformation("Transferred large file: {Size} bytes", largeData.Length);

        // Read large file back (will use chunked reading automatically)
        var readData = await this.device.FileSystem().ReadFileAsync("/large_files/large_test.bin");
        
        // Verify data integrity
        bool dataMatch = largeData.SequenceEqual(readData);
        this.logger.LogInformation("Large file transfer verification: {Status}", dataMatch ? "SUCCESS" : "FAILED");

        // Clean up
        await this.device.FileSystem().DeleteFileAsync("/large_files/large_test.bin");
        await this.device.FileSystem().DeleteDirectoryAsync("/large_files");
    }

    /// <summary>
    /// Task attribute method that deploys a Python function to the device via file transfer.
    /// This demonstrates how file transfer can be used for code deployment.
    /// </summary>
    [Task(Cache = true, Name = "deploy_and_run_script")]
    public async Task<string> DeployAndRunPythonScriptAsync() {
        this.logger.LogInformation("Deploying Python script to device");

        // Python script to deploy
        string pythonScript = @"
def calculate_fibonacci(n):
    """"""Calculate fibonacci number using iterative approach""""""
    if n <= 1:
        return n
    
    a, b = 0, 1
    for i in range(2, n + 1):
        a, b = b, a + b
    
    return b

def main():
    results = []
    for i in range(10):
        fib = calculate_fibonacci(i)
        results.append(f'fib({i}) = {fib}')
    
    return '\\n'.join(results)

# Execute and return result
result = main()
print(result)
";

        // Deploy script to device
        await this.device.FileSystem().WriteTextFileAsync("/deployed_script.py", pythonScript);
        this.logger.LogInformation("Script deployed to device");

        // Execute the deployed script
        return await this.device.ExecuteAsync<string>("exec(open('/deployed_script.py').read())");
    }

    /// <summary>
    /// Task attribute method that manages a configuration file on the device.
    /// This shows how to combine file operations with cached Task execution.
    /// </summary>
    [Task(Cache = true, Exclusive = true, Name = "manage_config")]
    public async Task<Dictionary<string, object>> ManageDeviceConfigAsync(Dictionary<string, object> newConfig) {
        this.logger.LogInformation("Managing device configuration");

        const string configPath = "/device_config.json";

        // Check if config file exists
        bool configExists = await this.device.FileSystem().ExistsAsync(configPath);
        
        Dictionary<string, object> currentConfig = new();
        
        if (configExists) {
            // Read existing configuration
            string configJson = await this.device.FileSystem().ReadTextFileAsync(configPath);
            
            // Parse configuration on device and return it
            string parseCode = $@"
import json
config_str = '''{configJson}'''
config = json.loads(config_str)
print(json.dumps(config))
";
            string currentConfigJson = await this.device.ExecuteAsync<string>(parseCode);
            currentConfig = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(currentConfigJson) ?? new();
        }

        // Merge new configuration
        foreach (var kvp in newConfig) {
            currentConfig[kvp.Key] = kvp.Value;
        }

        // Serialize and save updated configuration
        string updatedConfigJson = System.Text.Json.JsonSerializer.Serialize(currentConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await this.device.FileSystem().WriteTextFileAsync(configPath, updatedConfigJson);

        this.logger.LogInformation("Configuration updated and saved");
        return currentConfig;
    }

    /// <summary>
    /// Demonstrates recursive directory operations.
    /// </summary>
    public async Task DemonstrateDirectoryOperationsAsync() {
        this.logger.LogInformation("Starting directory operations demonstration");

        // Create nested directory structure
        await this.device.FileSystem().CreateDirectoryAsync("/test_structure/sub1/sub2", recursive: true);
        await this.device.FileSystem().CreateDirectoryAsync("/test_structure/sub3", recursive: true);

        // Create files in different directories
        await this.device.FileSystem().WriteTextFileAsync("/test_structure/root_file.txt", "Root level file");
        await this.device.FileSystem().WriteTextFileAsync("/test_structure/sub1/sub1_file.txt", "Sub1 level file");
        await this.device.FileSystem().WriteTextFileAsync("/test_structure/sub1/sub2/deep_file.txt", "Deep level file");
        await this.device.FileSystem().WriteTextFileAsync("/test_structure/sub3/sub3_file.txt", "Sub3 level file");

        // List directory structure recursively
        var allEntries = await this.device.FileSystem().ListAsync("/test_structure", recursive: true);
        this.logger.LogInformation("Complete directory structure:");
        foreach (var entry in allEntries.OrderBy(e => e.Path)) {
            string entryType = entry.IsDirectory ? "DIR " : "FILE";
            string size = entry.Size.HasValue ? $"({entry.Size} bytes)" : "";
            this.logger.LogInformation("  {Type} {Path} {Size}", entryType, entry.Path, size);
        }

        // Clean up recursively
        await this.device.FileSystem().DeleteDirectoryAsync("/test_structure", recursive: true);
        this.logger.LogInformation("Cleaned up directory structure");
    }
}

/// <summary>
/// Main program demonstrating comprehensive file transfer capabilities.
/// </summary>
public class Program {
    public static async Task Main(string[] args) {
        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder => {
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
        });

        var logger = loggerFactory.CreateLogger<Program>();

        // Show usage if needed
        if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h")) {
            Console.WriteLine("File Transfer Example");
            Console.WriteLine("Usage: FileTransferExample [connection_string]");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  Subprocess: FileTransferExample");
            Console.WriteLine("  Pico:       FileTransferExample serial:/dev/ttyACM0");
            Console.WriteLine("  ESP32:      FileTransferExample serial:/dev/ttyUSB0");
            Console.WriteLine("  Windows:    FileTransferExample serial:COM3");
            return;
        }

        try {
            logger.LogInformation("Starting File Transfer Example");

            // Create device - can use subprocess, Pico, or ESP32
            string connectionString = args.Length > 0 ? args[0] : "subprocess:../micropython/ports/unix/build-standard/micropython";
            logger.LogInformation("Using connection: {ConnectionString}", connectionString);
            
            using var device = Device.FromConnectionString(connectionString, loggerFactory);
            await device.ConnectAsync();

            logger.LogInformation("Connected to device successfully");

            // Create file manager
            var fileManager = new DeviceFileManager(device, loggerFactory.CreateLogger<DeviceFileManager>());

            // Run basic file operations
            await fileManager.DemonstrateBasicFileOperationsAsync();
            
            // Run large file transfer test
            await fileManager.DemonstrateLargeFileTransferAsync();

            // Run directory operations
            await fileManager.DemonstrateDirectoryOperationsAsync();

            // Demonstrate Task attribute integration with file operations
            logger.LogInformation("=== Task Attribute Integration ===");

            // Deploy and run Python script using Task attribute
            string fibResult = await fileManager.DeployAndRunPythonScriptAsync();
            logger.LogInformation("Fibonacci calculation result:\n{Result}", fibResult);

            // Manage device configuration using Task attribute
            var configUpdate = new Dictionary<string, object> {
                ["version"] = "1.0.0",
                ["debug_mode"] = true,
                ["max_connections"] = 10,
                ["last_updated"] = DateTime.UtcNow.ToString("O")
            };

            var finalConfig = await fileManager.ManageDeviceConfigAsync(configUpdate);
            logger.LogInformation("Final device configuration: {Config}", 
                System.Text.Json.JsonSerializer.Serialize(finalConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            // Clean up deployed files
            if (await device.FileSystem().ExistsAsync("/deployed_script.py")) {
                await device.FileSystem().DeleteFileAsync("/deployed_script.py");
            }
            if (await device.FileSystem().ExistsAsync("/device_config.json")) {
                await device.FileSystem().DeleteFileAsync("/device_config.json");
            }

            logger.LogInformation("File Transfer Example completed successfully");
        }
        catch (Exception ex) {
            logger.LogError(ex, "File Transfer Example failed");
            Environment.Exit(1);
        }
    }
}