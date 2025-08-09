using System;
using System.Threading.Tasks;
using Belay.Core;
using Belay.Core.Communication;
using Belay.Core.Sessions;
using Microsoft.Extensions.Logging;

class Program
{
    static async Task Main(string[] args)
    {
        // Set up logging
        using var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        
        var logger = loggerFactory.CreateLogger<Program>();
        
        try
        {
            logger.LogInformation("Starting session integration test...");
            
            // Set up subprocess communication with MicroPython Unix port
            var micropythonPath = "../micropython/ports/unix/build-standard/micropython";
            
            if (!System.IO.File.Exists(micropythonPath))
            {
                logger.LogError("MicroPython Unix port not found at {Path}", micropythonPath);
                return;
            }
            
            logger.LogInformation("Using MicroPython at {Path}", micropythonPath);
            
            // Create communication
            var commLogger = loggerFactory.CreateLogger<SubprocessDeviceCommunication>();
            var communication = new SubprocessDeviceCommunication(micropythonPath, logger: commLogger);
            
            // Create session manager
            var sessionManager = new DeviceSessionManager(loggerFactory);
            
            logger.LogInformation("Initial session manager state: {State}", sessionManager.State);
            
            // Create a session
            var session = await sessionManager.CreateSessionAsync(communication);
            logger.LogInformation("Created session {SessionId}, active: {IsActive}", session.SessionId, session.IsActive);
            logger.LogInformation("Session manager state: {State}", sessionManager.State);
            
            // Execute some basic MicroPython code through the session
            logger.LogInformation("Executing basic arithmetic...");
            var result1 = await communication.ExecuteAsync("2 + 3");
            logger.LogInformation("Result: {Result}", result1.Trim());
            
            // Test device capabilities
            if (sessionManager.Capabilities != null)
            {
                logger.LogInformation("Device capabilities detected:");
                logger.LogInformation("  Type: {Type}", sessionManager.Capabilities.DeviceType);
                logger.LogInformation("  Version: {Version}", sessionManager.Capabilities.FirmwareVersion);
                logger.LogInformation("  Features: {Features}", sessionManager.Capabilities.SupportedFeatures);
            }
            else
            {
                logger.LogInformation("Device capabilities not yet detected");
            }
            
            // Get session statistics
            var stats = await sessionManager.GetSessionStatsAsync();
            logger.LogInformation("Session stats: {ActiveSessions} active, {TotalSessions} total", 
                stats.ActiveSessionCount, stats.TotalSessionCount);
            
            // Test session execution wrapper
            logger.LogInformation("Testing session execution wrapper...");
            var result2 = await sessionManager.ExecuteInSessionAsync(communication, async session => 
            {
                logger.LogInformation("Inside session execution - Session ID: {SessionId}", session.SessionId);
                return await communication.ExecuteAsync("import sys; sys.version");
            });
            logger.LogInformation("MicroPython version: {Version}", result2.Trim());
            
            // Clean shutdown
            logger.LogInformation("Shutting down session manager...");
            await sessionManager.DisposeAsync();
            logger.LogInformation("Final session manager state: {State}", sessionManager.State);
            
            // Verify session is disposed
            logger.LogInformation("Session active after shutdown: {IsActive}", session.IsActive);
            
            logger.LogInformation("✅ Session integration test completed successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Session integration test failed");
            throw;
        }
    }
}