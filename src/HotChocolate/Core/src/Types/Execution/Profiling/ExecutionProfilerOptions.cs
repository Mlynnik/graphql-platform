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
}
