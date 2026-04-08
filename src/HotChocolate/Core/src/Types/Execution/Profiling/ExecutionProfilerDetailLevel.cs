namespace HotChocolate.Execution.Profiling;

/// <summary>
/// Represents the level of detail to collect for execution profiling.
/// </summary>
public enum ExecutionProfilerDetailLevel
{
    /// <summary>
    /// Collect only data related to slow fields.
    /// </summary>
    SlowFields = 0,

    /// <summary>
    /// Collect full profiling information.
    /// </summary>
    Full = 1,

    /// <summary>
    /// Collect only data for N+1 detection.
    /// </summary>
    NPlusOneOnly = 2
}
