// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core;

using Microsoft.Extensions.Logging;

/// <summary>
/// Optimizes chunk sizes for file transfers based on performance metrics.
/// This is a stateful, non-thread-safe class designed for use by a single DeviceConnection.
/// Each DeviceConnection instance should create its own AdaptiveChunkOptimizer for sequential file transfers.
/// </summary>
/// <remarks>
/// This class follows the reference mpremote approach by avoiding threading complexity.
/// File transfers are inherently sequential operations, and the async/await pattern
/// provides sufficient concurrency for I/O-bound operations.
/// </remarks>
internal class AdaptiveChunkOptimizer {
    private readonly ILogger logger;
    private int currentChunkSize;
    private readonly int initialChunkSize;

    // Performance tracking - simple state without locking
    private double averageThroughput = 0.0; // bytes per second
    private int measurementCount = 0;
    private TimeSpan lastTransferDuration = TimeSpan.Zero;
    private int lastTransferSize = 0;

    // Optimization parameters
    private const int MINCHUNKSIZE = 64;      // Minimum chunk size (64 bytes)
    private const int MAXCHUNKSIZE = 4096;    // Maximum chunk size (4KB)
    private const double THROUGHPUTTHRESHOLD = 0.95; // 95% threshold for increasing chunk size
    private const int SAMPLESIZE = 5;          // Number of samples for stability

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveChunkOptimizer"/> class.
    /// </summary>
    /// <param name="initialChunkSize">The initial chunk size to start with.</param>
    /// <param name="logger">The logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    public AdaptiveChunkOptimizer(int initialChunkSize, ILogger logger) {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.initialChunkSize = Math.Max(MINCHUNKSIZE, Math.Min(initialChunkSize, MAXCHUNKSIZE));
        this.currentChunkSize = initialChunkSize;

        logger.LogDebug("Initialized adaptive chunk optimizer with initial size: {InitialSize} bytes", currentChunkSize);
    }

    /// <summary>
    /// Gets the current optimal chunk size for transfers.
    /// </summary>
    /// <returns>The optimal chunk size in bytes.</returns>
    /// <remarks>
    /// This method is not thread-safe. Each DeviceConnection should have its own
    /// AdaptiveChunkOptimizer instance for sequential file transfer operations.
    /// </remarks>
    public int GetOptimalChunkSize() {
        return currentChunkSize;
    }

    /// <summary>
    /// Records a transfer and updates the optimization algorithm.
    /// </summary>
    /// <param name="bytesTransferred">Number of bytes transferred.</param>
    /// <param name="duration">Time taken for the transfer.</param>
    /// <remarks>
    /// This method is not thread-safe. It should only be called sequentially
    /// during file transfer operations by a single DeviceConnection.
    /// </remarks>
    public void RecordTransfer(int bytesTransferred, TimeSpan duration) {
        if (bytesTransferred <= 0 || duration <= TimeSpan.Zero) {
            logger.LogTrace("Invalid transfer metrics, skipping optimization");
            return;
        }

        lastTransferSize = bytesTransferred;
        lastTransferDuration = duration;

        // Calculate current throughput (bytes per second)
        var currentThroughput = bytesTransferred / duration.TotalSeconds;

        // Update rolling average using exponential moving average
        if (measurementCount == 0) {
            averageThroughput = currentThroughput;
        }
        else {
            // Use alpha = 0.3 for exponential moving average (30% current, 70% historical)
            const double alpha = 0.3;
            averageThroughput = (alpha * currentThroughput) + ((1 - alpha) * averageThroughput);
        }

        measurementCount++;

        logger.LogTrace(
            "Transfer recorded: {Bytes} bytes in {Duration}ms, throughput: {Throughput:F1} bytes/s, avg: {AvgThroughput:F1} bytes/s",
            bytesTransferred, duration.TotalMilliseconds, currentThroughput, averageThroughput);

        // Only optimize after collecting enough samples for stability
        if (measurementCount >= SAMPLESIZE) {
            OptimizeChunkSize(currentThroughput);
        }
    }

    private void OptimizeChunkSize(double currentThroughput) {
        var oldChunkSize = currentChunkSize;

        // If current throughput is significantly better than average, try increasing chunk size
        if (currentThroughput > averageThroughput * THROUGHPUTTHRESHOLD &&
            currentChunkSize < MAXCHUNKSIZE) {
            // Increase chunk size by 25% or minimum 64 bytes
            var increase = Math.Max(64, currentChunkSize / 4);
            currentChunkSize = Math.Min(MAXCHUNKSIZE, currentChunkSize + increase);
        }

        // If performance is degrading and chunk size is larger than initial, try reducing
        else if (currentThroughput < averageThroughput * 0.8 &&
                 currentChunkSize > initialChunkSize) {
            // Decrease chunk size by 25% but not below initial size
            var decrease = Math.Max(32, currentChunkSize / 4);
            currentChunkSize = Math.Max(initialChunkSize, currentChunkSize - decrease);
        }

        // If performance is very poor, reset to initial size
        else if (currentThroughput < averageThroughput * 0.5) {
            currentChunkSize = initialChunkSize;
        }

        // Ensure chunk size stays within bounds
        currentChunkSize = Math.Max(MINCHUNKSIZE, Math.Min(MAXCHUNKSIZE, currentChunkSize));

        if (oldChunkSize != currentChunkSize) {
            logger.LogDebug(
                "Optimized chunk size: {OldSize} -> {NewSize} bytes (throughput: {Throughput:F1} bytes/s)",
                oldChunkSize, currentChunkSize, currentThroughput);
        }
    }

    /// <summary>
    /// Gets performance statistics for debugging and monitoring.
    /// </summary>
    /// <returns>Performance statistics snapshot.</returns>
    /// <remarks>
    /// This method is not thread-safe. It should only be called by the same
    /// DeviceConnection that owns this optimizer instance.
    /// </remarks>
    public ChunkOptimizerStats GetStats() {
        return new ChunkOptimizerStats {
            CurrentChunkSize = currentChunkSize,
            InitialChunkSize = initialChunkSize,
            AverageThroughput = averageThroughput,
            MeasurementCount = measurementCount,
            LastTransferSize = lastTransferSize,
            LastTransferDuration = lastTransferDuration,
        };
    }
}

/// <summary>
/// Performance statistics for the adaptive chunk optimizer.
/// </summary>
internal class ChunkOptimizerStats {
    /// <summary>Gets or sets the current optimized chunk size.</summary>
    public int CurrentChunkSize { get; set; }

    /// <summary>Gets or sets the initial chunk size.</summary>
    public int InitialChunkSize { get; set; }

    /// <summary>Gets or sets the average throughput in bytes per second.</summary>
    public double AverageThroughput { get; set; }

    /// <summary>Gets or sets the number of transfer measurements recorded.</summary>
    public int MeasurementCount { get; set; }

    /// <summary>Gets or sets the size of the last transfer.</summary>
    public int LastTransferSize { get; set; }

    /// <summary>Gets or sets the duration of the last transfer.</summary>
    public TimeSpan LastTransferDuration { get; set; }
}
