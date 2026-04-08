namespace HotChocolate.Execution.Profiling;

/// <summary>
/// Represents options for GraphQL execution profiling.
/// </summary>
public sealed class ExecutionProfilerOptions
{
    /// <summary>
    /// The default configuration section path.
    /// </summary>
    public const string DefaultConfigurationSectionPath = "HotChocolate:Execution:Profiler";

    /// <summary>
    /// Gets or sets a value indicating whether profiling is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the profiling detail level.
    /// </summary>
    public ExecutionProfilerDetailLevel DetailLevel { get; set; } = ExecutionProfilerDetailLevel.SlowFields;

    /// <summary>
    /// Gets or sets the minimum number of indexed path occurrences that triggers N+1 detection.
    /// </summary>
    public int NPlusOneListPatternThreshold { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether sliding-window aggregation is enabled.
    /// </summary>
    public bool AggregationEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of profiled requests retained in the aggregation window.
    /// </summary>
    public int SlidingWindowMaxRequests { get; set; } = 200;

    /// <summary>
    /// Gets or sets the time span retained in the aggregation window.
    /// </summary>
    public TimeSpan SlidingWindowDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets a value indicating whether profiler metrics should be emitted via OpenTelemetry meters.
    /// </summary>
    public bool OpenTelemetryEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether operation names should be included in OpenTelemetry metric tags.
    /// </summary>
    public bool OpenTelemetryIncludeOperationName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether slow request logging is enabled.
    /// </summary>
    public bool SlowRequestLoggingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the slow request threshold for warning logs.
    /// </summary>
    public TimeSpan SlowRequestThreshold { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets or sets the maximum number of slowest fields included in a slow request log entry.
    /// </summary>
    public int SlowRequestFieldLimit { get; set; } = 5;
}
