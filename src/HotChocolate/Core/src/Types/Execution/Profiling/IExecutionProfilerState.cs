namespace HotChocolate.Execution.Profiling;

/// <summary>
/// Represents runtime state for the execution profiler.
/// </summary>
public interface IExecutionProfilerState
{
    /// <summary>
    /// Gets a value indicating whether the execution profiler is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Sets the profiler state.
    /// </summary>
    /// <param name="enabled">
    /// A value indicating whether profiling is enabled.
    /// </param>
    void SetEnabled(bool enabled);
}
