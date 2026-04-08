namespace HotChocolate.Execution;

/// <summary>
/// Represents GraphQL request option overrides for the execution profiler.
/// </summary>
/// <param name="IsEnabled">
/// A value indicating whether execution profiling is enabled for the current request.
/// </param>
public sealed record ExecutionProfilerRequestOverrides(
    bool? IsEnabled = null);
