// Copyright (c) Belay.NET. All rights reserved.
// Licensed under the MIT License.

namespace Belay.Core.Execution {
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Method interception context for caching pipeline configuration in the simplified architecture.
    /// </summary>
    public class MethodInterceptionContext {
        /// <summary>
        /// Gets or sets the method being intercepted.
        /// </summary>
        public required MethodInfo Method { get; set; }

        /// <summary>
        /// Gets or sets the instance type (null for static methods).
        /// </summary>
        public Type? InstanceType { get; set; }

        /// <summary>
        /// Gets or sets the execution pipeline stages.
        /// Note: In simplified architecture, this is minimal compared to session-based approach.
        /// </summary>
        public required List<IPipelineStage> Pipeline { get; set; }

        /// <summary>
        /// Gets or sets cached metadata for the method.
        /// </summary>
        public Dictionary<string, object?> Metadata { get; set; } = new Dictionary<string, object?>();
    }

    /// <summary>
    /// Pipeline stage interface for method execution processing.
    /// </summary>
    public interface IPipelineStage {
        /// <summary>
        /// Gets the name of this pipeline stage.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the order priority of this stage (lower values execute first).
        /// </summary>
        int Order { get; }
    }

    /// <summary>
    /// Enhanced execution statistics for monitoring executor performance.
    /// </summary>
    public class EnhancedExecutionStatistics {
        /// <summary>
        /// Gets or sets the number of intercepted methods cached.
        /// </summary>
        public int InterceptedMethodCount { get; set; }

        /// <summary>
        /// Gets or sets the deployment cache hit count (simplified).
        /// </summary>
        public int CacheHitCount { get; set; }

        /// <summary>
        /// Gets or sets the number of specialized executors registered.
        /// </summary>
        public int SpecializedExecutorCount { get; set; }

        /// <summary>
        /// Gets or sets the number of pipeline stages configured.
        /// Note: In simplified architecture, this is typically 0 or minimal.
        /// </summary>
        public int PipelineStageCount { get; set; }
    }
}
