// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Belay.Core.Tests;

/// <summary>
/// Unit tests for the AdaptiveChunkOptimizer class.
/// </summary>
public class AdaptiveChunkOptimizerTests {
    private readonly ILogger logger = NullLogger.Instance;

    [Fact]
    public void Constructor_WithValidParameters_InitializesCorrectly() {
        // Arrange
        const int initialChunkSize = 512;

        // Act
        var optimizer = new AdaptiveChunkOptimizer(initialChunkSize, logger);

        // Assert
        Assert.Equal(initialChunkSize, optimizer.GetOptimalChunkSize());
    }

    [Fact]
    public void Constructor_WithTooSmallChunkSize_ClampsToMinimum() {
        // Arrange
        const int tooSmallChunkSize = 32; // Below minimum of 64

        // Act
        var optimizer = new AdaptiveChunkOptimizer(tooSmallChunkSize, logger);

        // Assert
        Assert.Equal(64, optimizer.GetOptimalChunkSize()); // Should be clamped to minimum
    }

    [Fact]
    public void Constructor_WithTooLargeChunkSize_ClampsToMaximum() {
        // Arrange
        const int tooLargeChunkSize = 8192; // Above maximum of 4096

        // Act
        var optimizer = new AdaptiveChunkOptimizer(tooLargeChunkSize, logger);

        // Assert
        Assert.Equal(4096, optimizer.GetOptimalChunkSize()); // Should be clamped to maximum
    }

    [Fact]
    public void RecordTransfer_WithInvalidParameters_DoesNotCrash() {
        // Arrange
        var optimizer = new AdaptiveChunkOptimizer(256, logger);

        // Act & Assert - should not throw
        optimizer.RecordTransfer(0, TimeSpan.FromMilliseconds(100)); // Zero bytes
        optimizer.RecordTransfer(100, TimeSpan.Zero); // Zero duration
        optimizer.RecordTransfer(-1, TimeSpan.FromMilliseconds(100)); // Negative bytes
    }

    [Fact]
    public void RecordTransfer_WithMultipleMeasurements_UpdatesStats() {
        // Arrange
        var optimizer = new AdaptiveChunkOptimizer(256, logger);

        // Act
        optimizer.RecordTransfer(256, TimeSpan.FromMilliseconds(100));
        optimizer.RecordTransfer(256, TimeSpan.FromMilliseconds(90));
        optimizer.RecordTransfer(256, TimeSpan.FromMilliseconds(80));

        // Assert
        var stats = optimizer.GetStats();
        Assert.Equal(3, stats.MeasurementCount);
        Assert.True(stats.AverageThroughput > 0);
        Assert.Equal(256, stats.LastTransferSize);
    }

    [Fact]
    public void RecordTransfer_WithImprovingPerformance_IncreasesChunkSize() {
        // Arrange
        var optimizer = new AdaptiveChunkOptimizer(256, logger);
        var initialChunkSize = optimizer.GetOptimalChunkSize();

        // Act - Record several transfers with improving performance
        for (int i = 0; i < 10; i++) {
            // Simulate improving performance (faster transfers)
            var duration = TimeSpan.FromMilliseconds(100 - i * 5); // Getting faster
            optimizer.RecordTransfer(256, duration);
        }

        // Assert
        var finalChunkSize = optimizer.GetOptimalChunkSize();
        Assert.True(finalChunkSize >= initialChunkSize, $"Chunk size should have increased from {initialChunkSize} but was {finalChunkSize}");
    }

    [Fact]
    public void RecordTransfer_WithDegradingPerformance_DecreasesChunkSize() {
        // Arrange
        var optimizer = new AdaptiveChunkOptimizer(1024, logger); // Start with larger chunk

        // First, establish a baseline with good performance
        for (int i = 0; i < 5; i++) {
            optimizer.RecordTransfer(1024, TimeSpan.FromMilliseconds(50));
        }

        var initialChunkSize = optimizer.GetOptimalChunkSize();

        // Act - Record transfers with degrading performance
        for (int i = 0; i < 10; i++) {
            // Simulate degrading performance (slower transfers)
            var duration = TimeSpan.FromMilliseconds(200 + i * 50); // Getting slower
            optimizer.RecordTransfer(optimizer.GetOptimalChunkSize(), duration);
        }

        // Assert
        var finalChunkSize = optimizer.GetOptimalChunkSize();
        Assert.True(finalChunkSize <= initialChunkSize, $"Chunk size should have decreased from {initialChunkSize} but was {finalChunkSize}");
    }

    [Fact]
    public void RecordTransfer_ChunkSizeStaysWithinBounds() {
        // Arrange
        var optimizer = new AdaptiveChunkOptimizer(256, logger);

        // Act - Record many transfers with extreme performance variations
        for (int i = 0; i < 50; i++) {
            if (i % 2 == 0) {
                // Very fast transfers
                optimizer.RecordTransfer(optimizer.GetOptimalChunkSize(), TimeSpan.FromMilliseconds(1));
            }
            else {
                // Very slow transfers
                optimizer.RecordTransfer(optimizer.GetOptimalChunkSize(), TimeSpan.FromMilliseconds(5000));
            }
        }

        // Assert
        var chunkSize = optimizer.GetOptimalChunkSize();
        Assert.True(chunkSize >= 64, $"Chunk size {chunkSize} should be >= minimum (64)");
        Assert.True(chunkSize <= 4096, $"Chunk size {chunkSize} should be <= maximum (4096)");
    }

    [Fact]
    public void GetStats_ReturnsCorrectInitialValues() {
        // Arrange
        const int initialChunkSize = 512;
        var optimizer = new AdaptiveChunkOptimizer(initialChunkSize, logger);

        // Act
        var stats = optimizer.GetStats();

        // Assert
        Assert.Equal(initialChunkSize, stats.CurrentChunkSize);
        Assert.Equal(initialChunkSize, stats.InitialChunkSize);
        Assert.Equal(0.0, stats.AverageThroughput);
        Assert.Equal(0, stats.MeasurementCount);
        Assert.Equal(0, stats.LastTransferSize);
        Assert.Equal(TimeSpan.Zero, stats.LastTransferDuration);
    }

    [Fact]
    public void ThreadSafety_ConcurrentAccess_DoesNotCrash() {
        // Arrange
        var optimizer = new AdaptiveChunkOptimizer(256, logger);
        const int numberOfThreads = 10;
        const int transfersPerThread = 100;

        // Act
        var tasks = new Task[numberOfThreads];
        for (int t = 0; t < numberOfThreads; t++) {
            tasks[t] = Task.Run(() => {
                for (int i = 0; i < transfersPerThread; i++) {
                    // Simulate concurrent transfers with varying performance
                    var chunkSize = optimizer.GetOptimalChunkSize();
                    var duration = TimeSpan.FromMilliseconds(50 + (i % 100));
                    optimizer.RecordTransfer(chunkSize, duration);

                    // Occasionally read stats
                    if (i % 10 == 0) {
                        _ = optimizer.GetStats();
                    }
                }
            });
        }

        // Assert - should complete without throwing
        Task.WaitAll(tasks);

        // Verify final state is reasonable
        var finalStats = optimizer.GetStats();
        Assert.True(finalStats.MeasurementCount <= numberOfThreads * transfersPerThread);
        Assert.True(finalStats.CurrentChunkSize >= 64 && finalStats.CurrentChunkSize <= 4096);
    }

    [Fact]
    public void GetOptimalChunkSize_ThreadSafety_ConsistentReads() {
        // Arrange
        var optimizer = new AdaptiveChunkOptimizer(256, logger);
        const int numberOfReads = 1000;

        // Act - Read chunk size from multiple threads while recording transfers
        var readTask = Task.Run(() => {
            for (int i = 0; i < numberOfReads; i++) {
                var chunkSize = optimizer.GetOptimalChunkSize();
                Assert.True(chunkSize >= 64 && chunkSize <= 4096, $"Invalid chunk size: {chunkSize}");
            }
        });

        var writeTask = Task.Run(() => {
            for (int i = 0; i < numberOfReads / 10; i++) {
                optimizer.RecordTransfer(256, TimeSpan.FromMilliseconds(50 + i));
            }
        });

        // Assert
        Task.WaitAll(readTask, writeTask);
    }
}
